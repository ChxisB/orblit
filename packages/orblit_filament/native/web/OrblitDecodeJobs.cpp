/* Decoding as jobs, for a browser: the one function a decoder worker runs.
 *
 * Compiled twice by build.sh: into the decoder module a Web Worker loads
 * (orblit_decoder_module.cpp), and into the renderer, where the same jobs run
 * on the page when no worker will take them. One definition in both is what
 * makes the page's fallback draw exactly what a worker would have: the same
 * readers (OrblitKtx2, stb through OrblitDecode.h, Basis Universal's
 * transcoder, OrblitHdrImage and cmgen's arithmetic in OrblitEnvironmentBake)
 * reached through the same code, linked from the same archives.
 *
 * Numbers in and numbers out, because a job crosses to a worker as a message:
 * a Float64Array of parameters one way, parts and a Float64Array of results
 * the other. A 64-bit hash does not fit in a double, so hashes travel as two
 * halves of 32 bits.
 *
 * Basis is transcoded here exactly as Filament's Ktx2Reader transcodes it
 * (libs/ktxreader/src/Ktx2Reader.cpp, transcodeImageLevel): the same target
 * chosen in the same order, the same flags, level by level from the largest
 * with one state carried between them. Not through Ktx2Reader itself, because
 * that makes the texture as it reads the header — a Filament engine this
 * module does not have — and copies the whole file and starts the transcoder
 * on the drawing thread before a worker is asked for anything.
 */

#include "OrblitDecode.h"
#include "OrblitKtx2.h"

#include <chrono>
#include <cstdlib>
#include <cstring>

#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Warray-bounds"
#include <basisu_transcoder.h>
#pragma clang diagnostic pop

namespace orblit {
namespace web {

namespace {

using Clock = std::chrono::steady_clock;

/// A part from a vector, copied into malloc's memory so it can be handed on.
bool partOf(const void *data, size_t bytes, DecodeAnswer &out) {
  auto *copy = static_cast<uint8_t *>(std::malloc(bytes == 0 ? 1 : bytes));
  if (copy == nullptr) return false;
  if (bytes > 0) std::memcpy(copy, data, bytes);
  out.parts.push_back({copy, bytes});
  return true;
}

void ktx2Levels(const uint8_t *data, size_t size, uint32_t skip,
                DecodeAnswer &out) {
  ktx2::Header header;
  std::string why = ktx2::read(data, size, header);
  if (!why.empty()) {
    out.note = why;
    return;
  }
  // Smallest first, as TextureQueue::decode reads them natively.
  for (uint32_t level = header.levels; level-- > skip;) {
    const uint64_t bytes = ktx2::levelBytes(header, level);
    auto *into = static_cast<uint8_t *>(std::malloc(size_t(bytes)));
    if (into == nullptr) {
      out.note = decoding::sentence("There was no memory for level %u.", level);
      out.release();
      return;
    }
    out.parts.push_back({into, size_t(bytes)});
    out.numbers.push_back(level);
    why = ktx2::readLevel(data, size, header, level, into, size_t(bytes));
    if (!why.empty()) {
      out.note = why;
      out.release();
      out.numbers.clear();
      return;
    }
  }
}

void picture(const uint8_t *data, size_t size, uint32_t halvings, bool srgb,
             DecodeAnswer &out) {
  DecodedPicture decoded;
  out.note = decodePicture(data, size, halvings, srgb, decoded);
  if (!out.note.empty()) return;
  if (decoded.fromStb) {
    // stb's memory is stb's to free, and the answer frees with free.
    const bool copied = partOf(decoded.pixels, decoded.size, out);
    stbi_image_free(decoded.pixels);
    if (!copied) {
      out.note = "There was no memory to hand it over.";
      return;
    }
  } else {
    out.parts.push_back({decoded.pixels, decoded.size});
  }
  out.numbers = {double(decoded.width), double(decoded.height)};
}

void basis(const uint8_t *data, size_t size, int32_t format, bool compressed,
           DecodeAnswer &out) {
  static const bool initialised = [] {
    basist::basisu_transcoder_init();
    return true;
  }();
  (void)initialised;

  const auto fail = [&out]() {
    out.note = "Its Basis data could not be transcoded.";
    out.release();
    out.numbers.clear();
  };
  basist::ktx2_transcoder transcoder;
  if (size > UINT32_MAX || !transcoder.init(data, uint32_t(size)) ||
      !transcoder.start_transcoding()) {
    fail();
    return;
  }
  const auto target = basist::transcoder_texture_format(format);
  const basisu::texture_format blockFormat =
      basist::basis_get_basisu_texture_format(target);
  basist::ktx2_transcoder_state state;
  state.clear();
  for (uint32_t level = 0; level < transcoder.get_levels(); level++) {
    basist::ktx2_image_level_info info;
    transcoder.get_image_level_info(info, level, 0, 0);
    size_t bytes = 0;
    uint32_t count = 0;
    if (compressed) {
      const uint32_t qwords = basisu::get_qwords_per_block(blockFormat);
      bytes = sizeof(uint64_t) * size_t(qwords) * size_t(info.m_total_blocks);
      if (qwords != 0 && info.m_total_blocks != 0 &&
          bytes / qwords / sizeof(uint64_t) != info.m_total_blocks) {
        fail();
        return;
      }
      count = info.m_total_blocks;
    } else {
      const uint32_t perPixel = basist::basis_get_bytes_per_block_or_pixel(target);
      bytes = size_t(perPixel) * size_t(info.m_orig_width) *
              size_t(info.m_orig_height);
      if (perPixel == 0 || (info.m_orig_width != 0 &&
                            bytes / perPixel / info.m_orig_width !=
                                info.m_orig_height)) {
        fail();
        return;
      }
      count = uint32_t(bytes / perPixel);
    }
    auto *into = static_cast<uint8_t *>(std::malloc(bytes == 0 ? 1 : bytes));
    if (into == nullptr) {
      fail();
      return;
    }
    out.parts.push_back({into, bytes});
    out.numbers.push_back(level);
    // Ktx2Reader's own arguments: no decode flags, the natural row pitch and
    // row count, channels nought and nought.
    if (!transcoder.transcode_image_level(level, 0, 0, into, count,
                                          target, 0, 0, 0, 0, 0, &state)) {
      fail();
      return;
    }
  }
}

// ---- Environments ----

constexpr size_t kEnvironmentFixed = 10;

uint32_t halfOf(uint64_t value, bool high) {
  return uint32_t(high ? value >> 32 : value);
}

void environment(const uint8_t *data, size_t size, const double *parameters,
                 size_t count, const std::string &name, DecodeAnswer &out) {
  if (count < kEnvironmentFixed) {
    out.note = "The environment job was asked wrongly.";
    return;
  }
  EnvironmentPictureRequest request;
  request.name = name;
  request.wantsLight = parameters[0] != 0;
  request.wantsSky = parameters[1] != 0;
  request.reflectionSize = uint32_t(parameters[2]);
  request.largestSkybox = uint32_t(parameters[3]);
  request.widestUpload = uint32_t(parameters[4]);
  request.onCpu = parameters[5] != 0;
  request.limits.longestSide = uint32_t(parameters[6]);
  request.limits.mostPixels =
      (uint64_t(parameters[7]) << 32) | uint64_t(parameters[8]);
  const size_t hashes = size_t(parameters[9]);
  if (count < kEnvironmentFixed + hashes * 2) {
    out.note = "The environment job was asked wrongly.";
    return;
  }
  for (size_t h = 0; h < hashes; h++) {
    request.alreadyFiltered.push_back(
        (uint64_t(parameters[kEnvironmentFixed + h * 2]) << 32) |
        uint64_t(parameters[kEnvironmentFixed + h * 2 + 1]));
  }

  EnvironmentPicture picture;
  prepareEnvironmentPicture(data, size, request, forEachInOrder, picture);

  // The numbers: note-independent facts first, then the harmonics, then the
  // timings; readEnvironmentAnswer reads them in this order.
  out.numbers = {double(halfOf(picture.hash, true)),
                 double(halfOf(picture.hash, false)),
                 picture.cached ? 1.0 : 0.0,
                 double(picture.skyboxSize),
                 double(picture.uploadWidth),
                 double(picture.uploadHeight),
                 double(picture.levels.size()),
                 picture.skyFaces.empty() ? 0.0 : 1.0,
                 picture.upload.empty() ? 0.0 : 1.0,
                 picture.hashMilliseconds,
                 picture.decodeMilliseconds,
                 picture.harmonicsMilliseconds,
                 picture.prepareMilliseconds};
  // The harmonics travel as their bits, the first part, so the lighting a
  // worker worked out is the lighting to the last bit.
  auto &bits = picture.harmonics;
  if (!partOf(bits.data(), sizeof(float) * bits.size(), out)) {
    out.note = "There was no memory to hand it over.";
    return;
  }
  out.note = picture.note;
  bool whole = true;
  if (!picture.upload.empty()) {
    whole = whole && partOf(picture.upload.data(),
                            picture.upload.size() * sizeof(uint16_t), out);
  }
  for (const std::vector<uint16_t> &level : picture.levels) {
    whole = whole && partOf(level.data(), level.size() * sizeof(uint16_t), out);
  }
  if (!picture.skyFaces.empty()) {
    whole = whole && partOf(picture.skyFaces.data(),
                            picture.skyFaces.size() * sizeof(uint16_t), out);
  }
  if (!whole) {
    out.note = "There was no memory to hand it over.";
    out.release();
  }
}

}  // namespace

void runDecodeJob(DecodeJob job, const uint8_t *data, size_t size,
                  const double *parameters, size_t parameterCount,
                  const std::string &name, DecodeAnswer &out) {
  const Clock::time_point started = Clock::now();
  out = DecodeAnswer();
  const auto parameter = [&](size_t i) {
    return i < parameterCount ? parameters[i] : 0.0;
  };
  switch (job) {
    case DecodeJob::ktx2Levels:
      ktx2Levels(data, size, uint32_t(parameter(0)), out);
      break;
    case DecodeJob::picture:
      picture(data, size, uint32_t(parameter(0)), parameter(1) != 0, out);
      break;
    case DecodeJob::basis:
      basis(data, size, int32_t(parameter(0)), parameter(1) != 0, out);
      break;
    case DecodeJob::environment:
      environment(data, size, parameters, parameterCount, name, out);
      break;
    default:
      out.note = "The decoder was asked for a job it does not know.";
      break;
  }
  out.milliseconds = decoding::millisecondsSince(started);
}

std::vector<double> environmentParameters(
    const EnvironmentPictureRequest &request) {
  std::vector<double> numbers = {
      request.wantsLight ? 1.0 : 0.0,
      request.wantsSky ? 1.0 : 0.0,
      double(request.reflectionSize),
      double(request.largestSkybox),
      double(request.widestUpload),
      request.onCpu ? 1.0 : 0.0,
      double(request.limits.longestSide),
      double(halfOf(request.limits.mostPixels, true)),
      double(halfOf(request.limits.mostPixels, false)),
      double(request.alreadyFiltered.size())};
  for (uint64_t hash : request.alreadyFiltered) {
    numbers.push_back(double(halfOf(hash, true)));
    numbers.push_back(double(halfOf(hash, false)));
  }
  return numbers;
}

bool readEnvironmentAnswer(DecodeAnswer &answer, EnvironmentPicture &out) {
  constexpr size_t kNumbers = 13;
  out = EnvironmentPicture();
  out.note = answer.note;
  if (answer.numbers.size() < kNumbers || answer.parts.empty() ||
      answer.parts[0].size != sizeof(float) * 27) {
    if (out.note.empty()) out.note = "The environment job answered wrongly.";
    return false;
  }
  const std::vector<double> &n = answer.numbers;
  out.hash = (uint64_t(n[0]) << 32) | uint64_t(n[1]);
  out.cached = n[2] != 0;
  out.skyboxSize = uint32_t(n[3]);
  out.uploadWidth = uint32_t(n[4]);
  out.uploadHeight = uint32_t(n[5]);
  const size_t levels = size_t(n[6]);
  const bool sky = n[7] != 0;
  const bool upload = n[8] != 0;
  out.hashMilliseconds = n[9];
  out.decodeMilliseconds = n[10];
  out.harmonicsMilliseconds = n[11];
  out.prepareMilliseconds = n[12];
  std::memcpy(out.harmonics.data(), answer.parts[0].bytes, sizeof(float) * 27);
  if (!out.note.empty()) return true;

  size_t at = 1;
  const auto halfFloats = [&](std::vector<uint16_t> &into) {
    if (at >= answer.parts.size()) return false;
    const DecodedPart &part = answer.parts[at++];
    into.resize(part.size / sizeof(uint16_t));
    std::memcpy(into.data(), part.bytes, into.size() * sizeof(uint16_t));
    return true;
  };
  bool whole = true;
  if (upload) whole = whole && halfFloats(out.upload);
  out.levels.resize(levels);
  for (std::vector<uint16_t> &level : out.levels) {
    whole = whole && halfFloats(level);
  }
  if (sky) whole = whole && halfFloats(out.skyFaces);
  if (!whole) out.note = "The environment job answered wrongly.";
  return whole;
}

// ---- Basis targets ----

namespace {

using basist::transcoder_texture_format;

struct BasisFamily {
  uint32_t vkFormat;
  transcoder_texture_format transcoderFormat;
  bool compressed;
  bool srgb;
};

/// The formats Filament's getFinalFormatInfo maps TextureQueue's Basis
/// targets to, by vkFormat.
constexpr BasisFamily kBasisFamilies[] = {
    {158, transcoder_texture_format::cTFASTC_4x4_RGBA, true, true},
    {157, transcoder_texture_format::cTFASTC_4x4_RGBA, true, false},
    {146, transcoder_texture_format::cTFBC7_RGBA, true, true},
    {145, transcoder_texture_format::cTFBC7_RGBA, true, false},
    {152, transcoder_texture_format::cTFETC2_RGBA, true, true},
    {151, transcoder_texture_format::cTFETC2_RGBA, true, false},
    {138, transcoder_texture_format::cTFBC3_RGBA, true, true},
    {137, transcoder_texture_format::cTFBC3_RGBA, true, false},
    {43, transcoder_texture_format::cTFRGBA32, false, true},
    {37, transcoder_texture_format::cTFRGBA32, false, false},
};

}  // namespace

std::string chooseBasisTarget(const uint8_t *data, size_t size, bool srgb,
                              const BasisCandidate *candidates,
                              size_t candidateCount, BasisTarget &out) {
  static const bool initialised = [] {
    basist::basisu_transcoder_init();
    return true;
  }();
  (void)initialised;
  constexpr const char *kRefused =
      "Filament's Basis reader would not take it: it may be a cubemap or an "
      "array, or transcode to nothing this device samples.";

  // init reads the header, the level index and the descriptor, and neither
  // copies the file nor decompresses anything.
  basist::ktx2_transcoder transcoder;
  if (size > UINT32_MAX || !transcoder.init(data, uint32_t(size))) {
    return kRefused;
  }
  const uint32_t transfer = transcoder.get_dfd_transfer_func();
  if ((transfer == basist::KTX2_KHR_DF_TRANSFER_LINEAR && srgb) ||
      (transfer == basist::KTX2_KHR_DF_TRANSFER_SRGB && !srgb)) {
    return decoding::sentence(
        "It is Basis marked %s, and is used where %s is needed; Basis cannot "
        "be read the other way.",
        srgb ? "linear" : "sRGB", srgb ? "sRGB" : "linear");
  }
  if (transcoder.get_faces() == 6 || transcoder.get_layers() > 1) {
    return kRefused;
  }
  for (size_t c = 0; c < candidateCount; c++) {
    if (!candidates[c].supported) continue;
    for (const BasisFamily &family : kBasisFamilies) {
      if (family.vkFormat != candidates[c].vkFormat) continue;
      if (family.srgb != srgb) break;
      if (!basist::basis_is_format_supported(family.transcoderFormat,
                                             transcoder.get_basis_tex_format())) {
        break;
      }
      out.vkFormat = family.vkFormat;
      out.transcoderFormat = int32_t(family.transcoderFormat);
      out.compressed = family.compressed;
      out.width = transcoder.get_width();
      out.height = transcoder.get_height();
      out.levels = transcoder.get_levels();
      return "";
    }
  }
  return kRefused;
}

}  // namespace web
}  // namespace orblit
