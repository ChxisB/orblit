#pragma once

// Decoding that needs nothing but bytes, shared by whatever thread does it.
//
// Two jobs the renderer used to do inline where it queued them: a PNG or JPEG
// into pixels, halved until it fits, for OrblitTextures.cpp; and an .hdr or
// .exr into what an environment is built from — its harmonics, and the
// picture or cube the GPU is handed — for OrblitEnvironment.cpp. Natively
// both run on worker threads. In a browser without threads they run on a Web
// Worker, inside a small WebAssembly module of their own
// (native/web/orblit_decoder_module.cpp), and on the page only when no
// worker will answer. Written once, here, so those three places run the same
// arithmetic on the same bytes and draw the same pixels.
//
// Header-only on purpose. Everything here was already compiled into
// OrblitTextures.cpp and OrblitEnvironment.cpp, which every platform's build
// lists; moving it into a header they include changes no platform's source
// list, and the decoder module includes it like any other file.
//
// Like OrblitKtx2 and OrblitHdrImage beneath it: no Filament type, no
// renderer, no file system, no threads. The last section, the browser's
// half, is declared only for a browser build and defined in native/web.

#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <cstdarg>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <functional>
#include <string>
#include <utility>
#include <vector>

#include "OrblitEnvironmentBake.h"
#include "OrblitHdrImage.h"

// stb_image's own declarations, as OrblitPlatform.cpp has them and for the
// same reason: Filament's release links it into libstb.a on every platform
// and ships no header for it.
extern "C" {
unsigned char *stbi_load_from_memory(const unsigned char *buffer, int length,
                                     int *width, int *height, int *channels,
                                     int desiredChannels);
void stbi_image_free(void *pixels);
}

namespace orblit {

namespace decoding {

/// Milliseconds since `from`, on a clock of this file's own, so the header
/// needs nothing of OrblitPlatform's.
inline double millisecondsSince(std::chrono::steady_clock::time_point from) {
  return std::chrono::duration<double, std::milli>(
             std::chrono::steady_clock::now() - from)
      .count();
}

/// printf into a string, for notes.
inline std::string sentence(const char *format, ...) {
  char buffer[512];
  va_list arguments;
  va_start(arguments, format);
  std::vsnprintf(buffer, sizeof buffer, format, arguments);
  va_end(arguments);
  return buffer;
}

inline uint32_t largestPowerOfTwoAtMost(uint32_t value) {
  uint32_t power = 1;
  while (power <= value / 2) power *= 2;
  return power;
}

}  // namespace decoding

// ---- Pictures ----

/// A level of an sRGB picture halved, averaging in linear light so that a
/// texture that has had its largest level left out is not darker than the
/// one that has not. Alpha is averaged as it is.
inline std::vector<uint8_t> halvedPicture(const uint8_t *rgba, uint32_t width,
                                          uint32_t height, bool srgb,
                                          uint32_t *outWidth,
                                          uint32_t *outHeight) {
  static const auto linear = [] {
    std::array<float, 256> table{};
    for (int i = 0; i < 256; i++) {
      const float c = float(i) / 255.0f;
      table[size_t(i)] = c <= 0.04045f ? c / 12.92f
                                       : std::pow((c + 0.055f) / 1.055f, 2.4f);
    }
    return table;
  }();
  static const auto encoded = [] {
    std::array<uint8_t, 4096> table{};
    for (int i = 0; i < 4096; i++) {
      const float c = float(i) / 4095.0f;
      const float s = c <= 0.0031308f
                          ? c * 12.92f
                          : 1.055f * std::pow(c, 1.0f / 2.4f) - 0.055f;
      table[size_t(i)] = uint8_t(std::lround(std::clamp(s, 0.0f, 1.0f) * 255));
    }
    return table;
  }();

  const uint32_t w = std::max<uint32_t>(1, width / 2);
  const uint32_t h = std::max<uint32_t>(1, height / 2);
  std::vector<uint8_t> out(size_t(w) * h * 4);
  for (uint32_t y = 0; y < h; y++) {
    const uint32_t y0 = std::min(y * 2, height - 1);
    const uint32_t y1 = std::min(y * 2 + 1, height - 1);
    for (uint32_t x = 0; x < w; x++) {
      const uint32_t x0 = std::min(x * 2, width - 1);
      const uint32_t x1 = std::min(x * 2 + 1, width - 1);
      const uint8_t *taps[4] = {
          rgba + (size_t(y0) * width + x0) * 4,
          rgba + (size_t(y0) * width + x1) * 4,
          rgba + (size_t(y1) * width + x0) * 4,
          rgba + (size_t(y1) * width + x1) * 4};
      uint8_t *to = out.data() + (size_t(y) * w + x) * 4;
      for (int c = 0; c < 4; c++) {
        if (srgb && c < 3) {
          float sum = 0;
          for (const uint8_t *tap : taps) sum += linear[tap[c]];
          to[c] = encoded[size_t(std::lround(sum * 0.25f * 4095.0f))];
        } else {
          unsigned sum = 0;
          for (const uint8_t *tap : taps) sum += tap[c];
          to[c] = uint8_t((sum + 2) / 4);
        }
      }
    }
  }
  *outWidth = w;
  *outHeight = h;
  return out;
}

/// A picture decoded: RGBA, eight bits a channel, the top row first.
struct DecodedPicture {
  /// stb's memory when `fromStb`, malloc's otherwise; the caller frees it
  /// with stbi_image_free or free.
  uint8_t *pixels = nullptr;
  size_t size = 0;
  uint32_t width = 0;
  uint32_t height = 0;
  bool fromStb = false;
};

/// Decodes a PNG or JPEG and halves it `halvings` times. Empty on success;
/// otherwise why not, with nothing left to free.
inline std::string decodePicture(const uint8_t *data, size_t size,
                                 uint32_t halvings, bool srgb,
                                 DecodedPicture &out) {
  out = DecodedPicture();
  int wide = 0;
  int tall = 0;
  int channels = 0;
  // stb_image measures its input in an int.
  uint8_t *pixels =
      size > size_t(INT32_MAX)
          ? nullptr
          : stbi_load_from_memory(data, int(size), &wide, &tall, &channels, 4);
  if (pixels == nullptr) return "It could not be decoded.";
  out.pixels = pixels;
  out.fromStb = true;
  out.size = size_t(wide) * size_t(tall) * 4;
  out.width = uint32_t(wide);
  out.height = uint32_t(tall);
  for (uint32_t i = 0; i < halvings; i++) {
    uint32_t width = 0;
    uint32_t height = 0;
    std::vector<uint8_t> smaller = halvedPicture(
        out.pixels, out.width, out.height, srgb, &width, &height);
    auto *copy = static_cast<uint8_t *>(std::malloc(smaller.size()));
    if (out.fromStb) {
      stbi_image_free(out.pixels);
    } else {
      std::free(out.pixels);
    }
    out.pixels = nullptr;
    if (copy == nullptr) {
      out = DecodedPicture();
      return "There was no memory to make it smaller.";
    }
    std::memcpy(copy, smaller.data(), smaller.size());
    out.pixels = copy;
    out.fromStb = false;
    out.size = smaller.size();
    out.width = width;
    out.height = height;
  }
  return "";
}

// ---- Pictures of light ----

/// What an environment picture is to be made into, decided on the drawing
/// thread from the device (OrblitEnvironment.cpp's planFor).
struct EnvironmentPictureRequest {
  /// The file's name, for notes.
  std::string name{};
  bool wantsLight = false;
  bool wantsSky = false;
  uint32_t reflectionSize = 256;
  uint32_t largestSkybox = 1024;
  uint32_t widestUpload = 4096;
  /// Filtered by cmgen's arithmetic here rather than on the GPU.
  bool onCpu = false;
  HdrLimits limits{};
  /// The hashes already filtered under this plan: a picture among them is
  /// not decoded again.
  std::vector<uint64_t> alreadyFiltered{};
};

/// What it was made into.
struct EnvironmentPicture {
  /// Empty unless it could not be used, and then why.
  std::string note{};
  uint64_t hash = 0;
  /// Already filtered, under this hash: nothing else was done.
  bool cached = false;
  Harmonics harmonics{};
  uint32_t skyboxSize = 0;
  /// The GPU route's picture, as RGBA half floats.
  uint32_t uploadWidth = 0;
  uint32_t uploadHeight = 0;
  std::vector<uint16_t> upload{};
  /// The CPU route's reflections, a level each, and its backdrop's faces.
  std::vector<std::vector<uint16_t>> levels{};
  std::vector<uint16_t> skyFaces{};
  double hashMilliseconds = 0;
  double decodeMilliseconds = 0;
  double harmonicsMilliseconds = 0;
  double prepareMilliseconds = 0;
};

/// Everything a picture needs before the GPU sees it: hash, decode, cmgen's
/// cube and harmonics, then either the picture brought down to size and
/// packed as half floats, or — on the CPU route — cmgen's whole filter.
/// `doneWithBytes`, when given, is called as soon as the file is no longer
/// read, so its memory can go before the long part starts.
inline void prepareEnvironmentPicture(
    const uint8_t *bytes, size_t size, const EnvironmentPictureRequest &request,
    const ForEach &each, EnvironmentPicture &out,
    const std::function<void()> &doneWithBytes = {}) {
  using decoding::millisecondsSince;
  using Clock = std::chrono::steady_clock;
  const char *name = request.name.c_str();

  Clock::time_point started = Clock::now();
  out.hash = hashBytes(bytes, size);
  out.hashMilliseconds = millisecondsSince(started);
  if (std::find(request.alreadyFiltered.begin(), request.alreadyFiltered.end(),
                out.hash) != request.alreadyFiltered.end()) {
    out.cached = true;
    if (doneWithBytes) doneWithBytes();
    return;
  }

  started = Clock::now();
  HdrDecoded decoded = decodeHdrImage(bytes, size, request.limits);
  if (doneWithBytes) doneWithBytes();
  out.decodeMilliseconds = millisecondsSince(started);
  if (!decoded.note.empty()) {
    out.note = request.name + ": " + decoded.note;
    return;
  }
  HdrImage picture = std::move(decoded.image);
  // cmgen's own test for an equirectangular picture, and the one
  // EquirectangularToCubemap assumes.
  if (picture.width != picture.height * 2) {
    out.note = decoding::sentence(
        "%s is %u by %u. An environment picture is equirectangular: exactly "
        "twice as wide as it is tall.",
        name, picture.width, picture.height);
    return;
  }

  const uint32_t reflections = request.reflectionSize;
  out.skyboxSize = std::max<uint32_t>(
      16, std::min(request.largestSkybox,
                   decoding::largestPowerOfTwoAtMost(picture.width / 4)));

  started = Clock::now();
  CpuCubemap cube;
  if (request.wantsLight) {
    cube = mirroredCubemap(cubemapFromEquirectangular(picture, reflections, each),
                           each);
    out.harmonics = irradianceHarmonics(cube, each);
  }
  out.harmonicsMilliseconds = millisecondsSince(started);

  // Samples a texel for the CPU filter's first two levels, doubling after:
  // cmgen's.
  constexpr uint32_t kCpuSamples = 1024;

  started = Clock::now();
  if (request.onCpu) {
    if (request.wantsLight) {
      std::vector<CpuCubemap> mips;
      makeSeamless(cube);
      mips.push_back(std::move(cube));
      while (mips.back().size > 1) mips.push_back(halvedCubemap(mips.back()));
      for (const CpuCubemap &level : roughnessPrefilter(mips, kCpuSamples, each)) {
        out.levels.push_back(halfFloatRgba(level));
      }
    }
    if (request.wantsSky) {
      out.skyFaces = halfFloatRgba(mirroredCubemap(
          cubemapFromEquirectangular(picture, out.skyboxSize, each), each));
    }
  } else {
    uint32_t widest = 0;
    if (request.wantsLight) widest = std::max(widest, reflections * 8);
    if (request.wantsSky) widest = std::max(widest, out.skyboxSize * 4);
    widest = std::min(widest, request.widestUpload);
    while (picture.width > widest && picture.width >= 4) {
      HdrImage half = halvedImage(picture);
      if (half.empty()) break;
      picture = std::move(half);
    }
    out.uploadWidth = picture.width;
    out.uploadHeight = picture.height;
    out.upload = halfFloatRgba(picture.rgb.get(),
                               size_t(picture.width) * picture.height);
  }
  out.prepareMilliseconds = millisecondsSince(started);
}

#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)

// ---- In a browser ----
//
// Decoding as jobs a Web Worker can run: a kind, the bytes, a few numbers and
// a name in; bytes and numbers out. Numbers because that is what crosses to
// a worker and back without a schema, and because the same function then
// runs in the decoder module on a worker and in the renderer on the page —
// which is how the fallback draws what the worker would have drawn. Defined
// in native/web/OrblitDecodeJobs.cpp and native/web/OrblitDecodersWeb.cpp.

namespace web {

enum class DecodeJob : int32_t {
  /// A GPU-ready KTX 2 file's levels, decompressed. In: the levels to leave
  /// out. Out: a part per level kept, smallest first; a number per part, its
  /// level in the file.
  ktx2Levels = 1,
  /// A PNG or JPEG. In: halvings, sRGB. Out: one part of RGBA; width, height.
  picture = 2,
  /// A Basis file transcoded. In: the transcoder_texture_format, whether it
  /// is a block format. Out: a part per level, largest first as the file
  /// orders them; a number per part, its level.
  basis = 3,
  /// An environment picture prepared. In: see environmentParameters. Out:
  /// see readEnvironmentAnswer.
  environment = 4,
};

/// Memory from malloc, handed over with the answer.
struct DecodedPart {
  uint8_t *bytes = nullptr;
  size_t size = 0;
};

struct DecodeAnswer {
  /// Empty unless the job failed, and then why, as a sentence.
  std::string note{};
  std::vector<DecodedPart> parts{};
  std::vector<double> numbers{};
  /// What the job took where it ran.
  double milliseconds = 0;

  DecodeAnswer() = default;
  DecodeAnswer(const DecodeAnswer &) = delete;
  DecodeAnswer &operator=(const DecodeAnswer &) = delete;
  DecodeAnswer(DecodeAnswer &&other) noexcept { *this = std::move(other); }
  DecodeAnswer &operator=(DecodeAnswer &&other) noexcept {
    if (this != &other) {
      release();
      note = std::move(other.note);
      parts = std::move(other.parts);
      numbers = std::move(other.numbers);
      milliseconds = other.milliseconds;
      other.parts.clear();
    }
    return *this;
  }
  ~DecodeAnswer() { release(); }

  /// Takes part `i`'s memory; the caller frees it.
  uint8_t *takePart(size_t i) {
    uint8_t *bytes = parts[i].bytes;
    parts[i].bytes = nullptr;
    return bytes;
  }

  void release() {
    for (DecodedPart &part : parts) std::free(part.bytes);
    parts.clear();
  }
};

/// Runs a job where it stands. Any thread, any module.
void runDecodeJob(DecodeJob job, const uint8_t *data, size_t size,
                  const double *parameters, size_t parameterCount,
                  const std::string &name, DecodeAnswer &out);

/// An environment request as a job's numbers, and an answer back into what
/// the renderer reads.
std::vector<double> environmentParameters(
    const EnvironmentPictureRequest &request);
bool readEnvironmentAnswer(DecodeAnswer &answer, EnvironmentPicture &out);

/// A Basis file's target, chosen on the drawing thread exactly as
/// Filament's Ktx2Reader chooses one.
struct BasisTarget {
  /// The vkFormat of what it becomes (OrblitKtx2.h names them).
  uint32_t vkFormat = 0;
  /// basist::transcoder_texture_format, as its number.
  int32_t transcoderFormat = 0;
  bool compressed = false;
  uint32_t width = 0;
  uint32_t height = 0;
  uint32_t levels = 0;
};

/// One format a Basis file may become, in the order asked for.
struct BasisCandidate {
  /// As a vkFormat: one of ASTC 4×4, BC7, ETC2 RGBA8, BC3 and RGBA8, in
  /// either transfer function.
  uint32_t vkFormat;
  /// Whether the device samples it.
  bool supported;
};

/// Reads a Basis file's header with Basis Universal's own transcoder and
/// picks the first candidate it can become. Empty on success; otherwise why
/// not, as the page-thread path would have said it.
std::string chooseBasisTarget(const uint8_t *data, size_t size, bool srgb,
                              const BasisCandidate *candidates,
                              size_t candidateCount, BasisTarget &out);

/// The decoder workers, as the drawing thread sees them: native/web's
/// orblit_decoder_workers.js, reached from OrblitDecodersWeb.cpp.
///
/// A job is handed over with submit, its bytes copied out of the module's
/// memory and transferred to a worker, and its answer copied back in with
/// take. A worker that has not picked a job up within
/// kDecodeStartPatienceSeconds, or has not finished it within
/// kDecodeRunPatienceSeconds, is given up on, and the job decoded here.
namespace decoders {

/// How long a job may wait to be picked up before it is decoded on the page.
/// Workers are only handed a job when one is idle, so a job not picked up is
/// a worker that is not running: not yet scheduled, stopped by the browser,
/// or under a headless browser's virtual clock. The splat sorter's half
/// second, for the same reasons (OrblitSplatSorterWeb.cpp). Counted only
/// while the page is free to hear back — a frame at a time, at most a tenth
/// of a second for each — because a worker's answer cannot arrive while the
/// page is busy with something else.
constexpr double kDecodeStartPatienceSeconds = 0.5;

/// How long a job a worker has picked up may take. Longer by far than any
/// job measured — the slowest, a 2048² PNG with three others decoding
/// beside it, took 250 ms on an M4 Pro in Chrome, and a 2K .hdr prepared for
/// lighting 200 ms — because giving up on a worker that is merely slow means
/// doing its work a second time on the page, and a phone is several times
/// slower. What this catches is a worker that will never answer.
constexpr double kDecodeRunPatienceSeconds = 10.0;

enum class State : int32_t {
  /// Handed over; no worker has said it started.
  waiting = 0,
  started = 1,
  done = 2,
  /// Failed, given up on, or never known: decode it here.
  failed = 3,
};

/// How many jobs workers could start now, or -1 when none will be used: no
/// workers can be made here, the decoder module would not start, or a job
/// was given up on unstarted and no worker has answered since. Jobs that
/// meet -1 are decoded on the page; jobs that meet nought wait their turn.
int32_t capacity();

/// Hands a job over. Nought when it could not be.
int32_t submit(DecodeJob job, const uint8_t *data, size_t size,
               const std::vector<double> &parameters, const std::string &name);

/// Where the job stands, after giving it up if it has taken too long.
State poll(int32_t id);

/// The finished answer, its parts copied into this module's memory. False
/// when there is none. Either way the job is forgotten.
bool take(int32_t id, DecodeAnswer &out);

/// Forgets a job whose answer is no longer wanted.
void cancel(int32_t id);

}  // namespace decoders

}  // namespace web

#endif

}  // namespace orblit
