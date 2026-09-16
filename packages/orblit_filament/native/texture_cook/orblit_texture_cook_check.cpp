// The texture cooker's own checks, in C++ against OrblitTextureCook.h.
//
// What a cooked file is gets checked by reading it back with a KTX2 reader
// written here, from the specification, and sharing no code with the writer:
// the header, the level index, the descriptor field by field against what the
// Khronos Data Format Specification defines for that vkFormat, the key/value
// data, and every level inflated with zstd and decoded block by block. The
// renderer's loader has its own reader, written separately again; the two are
// meant to be run against each other's files. The block decoders are Basis
// Universal's (and, for ASTC, the Android decoder it carries), not the
// encoders that made the blocks.
//
// Fixtures are drawn here, in memory, every run, so the answer is known
// exactly: pixel art with transparent holes at an odd size, a colour ramp
// with fine detail, a cut-out of leaves, a normal map of bumps, a single
// channel of noise, a JPEG, and a Basis .ktx2 cooked from one of them.
//
//   orblit_texture_cook_check                 all checks, and 200 mutated
//                                             inputs
//   orblit_texture_cook_check --fuzz N        N mutated inputs, and nothing
//                                             else (build.sh fuzz runs this
//                                             under ASan and UBSan)
//   orblit_texture_cook_check --write-fixtures DIR
//                                             the fixtures as files, for
//                                             build.sh determinism
//   orblit_texture_cook_check --read FILE...  cooked .ktx2 files on disk,
//                                             through the reader here: every
//                                             level inflated and decoded
//   orblit_texture_cook_check --measure FILE [cook flags]
//                                             PSNR per family per level,
//                                             sizes and times for a real
//                                             texture, against Basis
//                                             Universal's direct ASTC encoder
//
// Real textures: ORBLIT_TEXTURE_SAMPLES names a directory; every .png, .jpg
// and .ktx2 in it (up to twelve) is cooked and read back, and every .png in
// it is also cooked losslessly and compared bit for bit with the PNG as a
// second, unrelated decoder reads it.
//
// Built by build.sh beside the cooker, and run by native/headless/build.sh
// test.

#include "OrblitTextureCook.h"

#include "encoder/basisu_enc.h"
#include "encoder/basisu_gpu_texture.h"
#include "transcoder/basisu_transcoder.h"
#include "transcoder/basisu_transcoder_uastc.h"
#include "zstd/zstd.h"

#if defined(__clang__)
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wmissing-field-initializers"
#pragma clang diagnostic ignored "-Wunused-function"
#elif defined(__GNUC__)
#pragma GCC diagnostic push
#pragma GCC diagnostic ignored "-Wmissing-field-initializers"
#endif
#define STB_IMAGE_WRITE_IMPLEMENTATION
#define STBI_WRITE_NO_STDIO
#include "stb_image_write.h"
#if defined(__clang__)
#pragma clang diagnostic pop
#elif defined(__GNUC__)
#pragma GCC diagnostic pop
#endif

#include <dirent.h>
#include <sys/stat.h>

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iterator>
#include <map>
#include <random>
#include <string>
#include <vector>

namespace {

using namespace orblit::texturecook;

int failures = 0;

void expect(bool holds, const std::string &what) {
  if (!holds) {
    std::fprintf(stderr, "FAIL: %s\n", what.c_str());
    failures++;
  }
}

// ---------------------------------------------------------------------------
// A KTX 2.0 reader, from the specification

uint32_t u32(const std::vector<uint8_t> &b, uint64_t at) {
  return uint32_t(b[at]) | uint32_t(b[at + 1]) << 8 | uint32_t(b[at + 2]) << 16 |
         uint32_t(b[at + 3]) << 24;
}

uint64_t u64(const std::vector<uint8_t> &b, uint64_t at) {
  return uint64_t(u32(b, at)) | uint64_t(u32(b, at + 4)) << 32;
}

struct Ktx2Level {
  uint64_t offset = 0;
  uint64_t length = 0;
  uint64_t uncompressed = 0;
  uint32_t width = 0;
  uint32_t height = 0;
  std::vector<uint8_t> data;  // inflated
};

struct Ktx2File {
  uint32_t vkFormat = 0;
  uint32_t width = 0;
  uint32_t height = 0;
  uint32_t scheme = 0;
  uint32_t model = 0;
  uint32_t transfer = 0;
  uint32_t uastcChannel = 0;
  std::map<std::string, std::string> keyValues;
  std::vector<Ktx2Level> levels;
};

/// What the Data Format Specification says a vkFormat's descriptor holds.
struct Expected {
  uint32_t model;
  uint32_t blockWidth, blockHeight;
  uint32_t bytesPlane0;
  bool srgb;
  // bitOffset, bitLength (as stored, n - 1), channelType (with qualifiers),
  // sampleUpper
  std::vector<std::vector<uint32_t>> samples;
  const char *name;
};

bool expectedFor(uint32_t vkFormat, Expected &e) {
  const uint32_t all = 0xFFFFFFFFu;
  switch (vkFormat) {
    case 37:
    case 43: {
      const bool srgb = vkFormat == 43;
      e = {1, 1, 1, 4, srgb,
           {{0, 7, 0, 255}, {8, 7, 1, 255}, {16, 7, 2, 255},
            {24, 7, srgb ? 0x1Fu : 0x0Fu, 255}},
           "R8G8B8A8"};
      return true;
    }
    case 139: e = {131, 4, 4, 8, false, {{0, 63, 0, all}}, "BC4"}; return true;
    case 141: e = {132, 4, 4, 16, false, {{0, 63, 0, all}, {64, 63, 1, all}}, "BC5"}; return true;
    case 145: e = {134, 4, 4, 16, false, {{0, 127, 0, all}}, "BC7"}; return true;
    case 146: e = {134, 4, 4, 16, true, {{0, 127, 0, all}}, "BC7 sRGB"}; return true;
    case 147: e = {161, 4, 4, 8, false, {{0, 63, 2, all}}, "ETC2 RGB8"}; return true;
    case 148: e = {161, 4, 4, 8, true, {{0, 63, 2, all}}, "ETC2 RGB8 sRGB"}; return true;
    case 151:
      e = {161, 4, 4, 16, false, {{0, 63, 15, all}, {64, 63, 2, all}}, "ETC2 RGBA8"};
      return true;
    case 152:
      e = {161, 4, 4, 16, true, {{0, 63, 0x1F, all}, {64, 63, 2, all}}, "ETC2 RGBA8 sRGB"};
      return true;
    case 153: e = {161, 4, 4, 8, false, {{0, 63, 0, all}}, "EAC R11"}; return true;
    case 155: e = {161, 4, 4, 16, false, {{0, 63, 0, all}, {64, 63, 1, all}}, "EAC RG11"}; return true;
    case 157: e = {162, 4, 4, 16, false, {{0, 127, 0, all}}, "ASTC 4x4"}; return true;
    case 158: e = {162, 4, 4, 16, true, {{0, 127, 0, all}}, "ASTC 4x4 sRGB"}; return true;
    default: return false;
  }
}

bool readKtx2(const std::vector<uint8_t> &b, Ktx2File &f, std::string &why) {
  static const uint8_t identifier[12] = {0xAB, 'K', 'T', 'X', ' ', '2', '0',
                                         0xBB, '\r', '\n', 0x1A, '\n'};
  if (b.size() < 80 || std::memcmp(b.data(), identifier, 12) != 0) {
    why = "no KTX 2.0 identifier";
    return false;
  }
  f = Ktx2File();
  f.vkFormat = u32(b, 12);
  const uint32_t typeSize = u32(b, 16);
  f.width = u32(b, 20);
  f.height = u32(b, 24);
  const uint32_t depth = u32(b, 28), layers = u32(b, 32), faces = u32(b, 36);
  const uint32_t levelCount = u32(b, 40);
  f.scheme = u32(b, 44);
  const uint32_t dfdOffset = u32(b, 48), dfdLength = u32(b, 52);
  const uint32_t kvdOffset = u32(b, 56), kvdLength = u32(b, 60);
  const uint64_t sgdOffset = u64(b, 64), sgdLength = u64(b, 72);

  if (typeSize != 1) { why = "typeSize is not 1"; return false; }
  if (f.width == 0 || f.height == 0 || f.width > 65536 || f.height > 65536) {
    why = "a size of " + std::to_string(f.width) + "x" + std::to_string(f.height);
    return false;
  }
  if (depth != 0 || layers != 0 || faces != 1) { why = "not a plain 2D texture"; return false; }
  uint32_t most = 1;
  for (uint32_t s = std::max(f.width, f.height); s > 1; s >>= 1) most++;
  if (levelCount == 0 || levelCount > most) {
    why = "levelCount " + std::to_string(levelCount) + " for " + std::to_string(f.width) +
          "x" + std::to_string(f.height);
    return false;
  }
  if (f.scheme != 2) { why = "not zstd supercompressed"; return false; }
  if (sgdOffset != 0 || sgdLength != 0) { why = "zstd has no global data"; return false; }

  const uint64_t indexEnd = 80 + 24 * uint64_t(levelCount);
  if (dfdOffset != indexEnd) { why = "the descriptor does not follow the level index"; return false; }
  if (dfdLength < 28 || uint64_t(dfdOffset) + dfdLength > b.size()) {
    why = "the descriptor runs past the file";
    return false;
  }
  if (u32(b, dfdOffset) != dfdLength) { why = "dfdTotalSize differs from dfdByteLength"; return false; }

  // The Basic descriptor block.
  const uint64_t d = dfdOffset + 4;
  const uint32_t vendorType = u32(b, d);
  const uint32_t versionSize = u32(b, d + 4);
  const uint32_t blockSize = versionSize >> 16;
  if (vendorType != 0 || (versionSize & 0xFFFF) != 2 || blockSize + 4 != dfdLength ||
      (blockSize - 24) % 16 != 0) {
    why = "not a single Basic descriptor block of version 2";
    return false;
  }
  f.model = b[d + 8];
  const uint32_t primaries = b[d + 9];
  f.transfer = b[d + 10];
  const uint32_t flags = b[d + 11];
  const uint32_t dims[4] = {b[d + 12], b[d + 13], b[d + 14], b[d + 15]};
  const uint32_t plane0 = b[d + 16];
  const uint32_t otherPlanes = u32(b, d + 16) >> 8 | u32(b, d + 20);
  const uint32_t sampleCount = (blockSize - 24) / 16;
  if (primaries != 1 || flags != 0 || otherPlanes != 0 || dims[2] != 0 || dims[3] != 0) {
    why = "descriptor primaries, flags or planes are not what a 2D colour texture has";
    return false;
  }
  if (f.transfer != 1 && f.transfer != 2) { why = "transfer is neither linear nor sRGB"; return false; }

  uint32_t blockWidth = 4, blockHeight = 4, blockBytes = 16;
  if (f.vkFormat == 0) {
    // UASTC: one sample holding 128 bits of a block.
    if (f.model != 166 || sampleCount != 1 || dims[0] != 3 || dims[1] != 3 || plane0 != 16) {
      why = "vkFormat 0 but not a UASTC LDR 4x4 descriptor";
      return false;
    }
    const uint32_t word = u32(b, d + 24);
    f.uastcChannel = word >> 24;
    if ((word & 0xFFFF) != 0 || ((word >> 16) & 0xFF) != 127 || u32(b, d + 28) != 0 ||
        u32(b, d + 32) != 0 || u32(b, d + 36) != 0xFFFFFFFFu ||
        (f.uastcChannel != 0 && f.uastcChannel != 3 && f.uastcChannel != 4)) {
      why = "UASTC sample is not RGB, RGBA or RRR over 128 bits";
      return false;
    }
  } else {
    Expected e;
    if (!expectedFor(f.vkFormat, e)) {
      why = "vkFormat " + std::to_string(f.vkFormat) + " is not one the cooker writes";
      return false;
    }
    if (f.model != e.model || dims[0] + 1 != e.blockWidth || dims[1] + 1 != e.blockHeight ||
        plane0 != e.bytesPlane0 || sampleCount != e.samples.size()) {
      why = std::string("descriptor model, block or planes do not match ") + e.name;
      return false;
    }
    // "If vkFormat is one of the *_SRGB formats, transferFunction must be
    // KHR_DF_TRANSFER_SRGB", and the unsuffixed ones should not be.
    if ((f.transfer == 2) != e.srgb) {
      why = std::string("transfer function does not match ") + e.name;
      return false;
    }
    for (uint32_t s = 0; s < sampleCount; s++) {
      const uint64_t at = d + 24 + 16 * s;
      const uint32_t word = u32(b, at);
      const std::vector<uint32_t> &want = e.samples[s];
      if ((word & 0xFFFF) != want[0] || ((word >> 16) & 0xFF) != want[1] ||
          (word >> 24) != want[2] || u32(b, at + 4) != 0 || u32(b, at + 8) != 0 ||
          u32(b, at + 12) != want[3]) {
        why = std::string("sample ") + std::to_string(s) + " does not match " + e.name;
        return false;
      }
    }
    blockWidth = e.blockWidth;
    blockHeight = e.blockHeight;
    blockBytes = e.bytesPlane0;
  }

  // Key/value data: sorted, each padded to four bytes, lengths adding up.
  uint64_t dataStart = uint64_t(dfdOffset) + dfdLength;
  if (kvdLength > 0) {
    if (kvdOffset != dataStart || uint64_t(kvdOffset) + kvdLength > b.size()) {
      why = "key/value data is not after the descriptor";
      return false;
    }
    uint64_t at = kvdOffset;
    const uint64_t end = uint64_t(kvdOffset) + kvdLength;
    std::string previous;
    while (at < end) {
      if (end - at < 4) { why = "key/value data ends inside a length"; return false; }
      const uint32_t length = u32(b, at);
      at += 4;
      if (length < 2 || length > end - at) { why = "a key/value length runs past its data"; return false; }
      const auto first = b.begin() + std::ptrdiff_t(at);
      const auto nul = std::find(first, first + length, uint8_t(0));
      if (nul == first + length || nul == first) { why = "a key is not NUL-terminated"; return false; }
      std::string key(first, nul);
      if (!previous.empty() && !(previous < key)) { why = "keys are not sorted and unique"; return false; }
      previous = key;
      f.keyValues[key] = std::string(nul + 1, first + length);
      at += length;
      const uint64_t padded = (at + 3) & ~uint64_t(3);
      if (padded > end) { why = "key/value padding runs past its data"; return false; }
      for (; at < padded; at++) {
        if (b[at] != 0) { why = "key/value padding is not zero"; return false; }
      }
    }
    dataStart = end;
  } else if (kvdOffset != 0) {
    why = "kvdByteOffset set with no key/value data";
    return false;
  }

  // Levels: largest first in the index, smallest first in the file, each an
  // independent zstd frame that inflates to exactly its blocks.
  f.levels.resize(levelCount);
  uint64_t expectAt = dataStart;
  for (uint32_t i = levelCount; i-- > 0;) {
    Ktx2Level &level = f.levels[i];
    const uint64_t at = indexEnd - 24 * uint64_t(levelCount - i);
    level.offset = u64(b, at);
    level.length = u64(b, at + 8);
    level.uncompressed = u64(b, at + 16);
    level.width = std::max<uint32_t>(1, f.width >> i);
    level.height = std::max<uint32_t>(1, f.height >> i);
    const uint64_t blocks = uint64_t((level.width + blockWidth - 1) / blockWidth) *
                            ((level.height + blockHeight - 1) / blockHeight);
    if (level.uncompressed != blocks * blockBytes) {
      why = "level " + std::to_string(i) + " is not the size its blocks are";
      return false;
    }
    if (level.offset != expectAt || level.length == 0 || level.length > b.size() ||
        level.offset > b.size() - level.length) {
      why = "level " + std::to_string(i) + " is not where the levels before it end";
      return false;
    }
    expectAt = level.offset + level.length;
    const uint8_t *frame = b.data() + level.offset;
    if (ZSTD_findFrameCompressedSize(frame, size_t(level.length)) != level.length ||
        ZSTD_getFrameContentSize(frame, size_t(level.length)) != level.uncompressed) {
      why = "level " + std::to_string(i) + " is not one zstd frame of its size";
      return false;
    }
    level.data.resize(size_t(level.uncompressed));
    const size_t inflated = ZSTD_decompress(level.data.data(), level.data.size(), frame,
                                            size_t(level.length));
    if (ZSTD_isError(inflated) || inflated != level.uncompressed) {
      why = "level " + std::to_string(i) + " does not inflate";
      return false;
    }
  }
  if (expectAt != b.size()) {
    why = "bytes after the last level";
    return false;
  }
  return true;
}

/// A level decoded to RGBA8, block by block.
bool decodeLevel(const Ktx2File &f, uint32_t index, Image &out) {
  const Ktx2Level &level = f.levels[index];
  out.width = level.width;
  out.height = level.height;
  out.rgba.assign(size_t(level.width) * level.height * 4, 0);
  if (f.vkFormat == 37 || f.vkFormat == 43) {
    out.rgba = level.data;
    return true;
  }
  basisu::texture_format format = basisu::texture_format::cInvalidTextureFormat;
  uint32_t bytes = 16;
  switch (f.vkFormat) {
    case 0: break;
    case 139: format = basisu::texture_format::cBC4; bytes = 8; break;
    case 141: format = basisu::texture_format::cBC5; break;
    case 145: case 146: format = basisu::texture_format::cBC7; break;
    case 147: case 148: format = basisu::texture_format::cETC2_RGB; bytes = 8; break;
    case 151: case 152: format = basisu::texture_format::cETC2_RGBA; break;
    case 153: format = basisu::texture_format::cETC2_R11_EAC; bytes = 8; break;
    case 155: format = basisu::texture_format::cETC2_RG11_EAC; break;
    case 157: case 158: format = basisu::texture_format::cASTC_LDR_4x4; break;
    default: return false;
  }
  const uint32_t across = (level.width + 3) / 4, down = (level.height + 3) / 4;
  for (uint32_t by = 0; by < down; by++) {
    for (uint32_t bx = 0; bx < across; bx++) {
      const uint8_t *block = level.data.data() + (size_t(by) * across + bx) * bytes;
      basisu::color_rgba pixels[16];
      for (auto &p : pixels) p.set(0, 0, 0, 255);
      if (f.vkFormat == 0) {
        basist::uastc_block uastc;
        std::memcpy(uastc.m_bytes, block, 16);
        basist::color32 decoded[16];
        if (!basist::unpack_uastc(uastc, decoded, false)) return false;
        for (int i = 0; i < 16; i++) {
          pixels[i].set(decoded[i].r, decoded[i].g, decoded[i].b, decoded[i].a);
        }
      } else if (!basisu::unpack_block(format, block, pixels, f.vkFormat == 158)) {
        return false;
      }
      for (uint32_t y = 0; y < 4 && by * 4 + y < level.height; y++) {
        for (uint32_t x = 0; x < 4 && bx * 4 + x < level.width; x++) {
          uint8_t *t = out.rgba.data() + (size_t(by * 4 + y) * level.width + bx * 4 + x) * 4;
          const basisu::color_rgba &p = pixels[y * 4 + x];
          t[0] = p.r;
          t[1] = p.g;
          t[2] = p.b;
          t[3] = p.a;
        }
      }
    }
  }
  return true;
}

/// Peak signal to noise over the channels in `mask` (bit 0 red .. bit 3
/// alpha); 99 when identical.
double psnr(const Image &a, const Image &b, uint32_t mask) {
  if (a.width != b.width || a.height != b.height) return -1.0;
  double sum = 0.0;
  size_t count = 0;
  for (size_t i = 0; i < a.rgba.size(); i += 4) {
    for (uint32_t c = 0; c < 4; c++) {
      if (!(mask & (1u << c))) continue;
      const double e = double(a.rgba[i + c]) - double(b.rgba[i + c]);
      sum += e * e;
      count++;
    }
  }
  if (count == 0) return -1.0;
  if (sum == 0.0) return 99.0;
  return 10.0 * std::log10(255.0 * 255.0 / (sum / double(count)));
}

uint32_t channelsCompared(Content content, uint32_t vkFormat, bool alpha) {
  switch (content) {
    case Content::kNormal:
      // BC5 and EAC RG11 hold X and Y; the shader rebuilds Z.
      return (vkFormat == 141 || vkFormat == 155) ? 0x3 : 0x7;
    case Content::kSingleChannel:
      return 0x1;
    case Content::kColour:
    default:
      return alpha ? 0xF : 0x7;
  }
}

// ---------------------------------------------------------------------------
// Fixtures

std::vector<uint8_t> pngOf(const Image &image) {
  std::vector<uint8_t> out;
  stbi_write_png_to_func(
      [](void *context, void *data, int size) {
        auto *bytes = static_cast<std::vector<uint8_t> *>(context);
        const auto *p = static_cast<uint8_t *>(data);
        bytes->insert(bytes->end(), p, p + size);
      },
      &out, int(image.width), int(image.height), 4, image.rgba.data(), int(image.width) * 4);
  return out;
}

std::vector<uint8_t> jpegOf(const Image &image) {
  std::vector<uint8_t> out;
  stbi_write_jpg_to_func(
      [](void *context, void *data, int size) {
        auto *bytes = static_cast<std::vector<uint8_t> *>(context);
        const auto *p = static_cast<uint8_t *>(data);
        bytes->insert(bytes->end(), p, p + size);
      },
      &out, int(image.width), int(image.height), 4, image.rgba.data(), 92);
  return out;
}

Image blank(uint32_t width, uint32_t height) {
  Image image;
  image.width = width;
  image.height = height;
  image.rgba.assign(size_t(width) * height * 4, 255);
  return image;
}

/// A small sprite: hard-edged shapes in a handful of colours, a transparent
/// background and a half-transparent shadow, at a size no block encoder
/// divides evenly.
Image pixelArt() {
  Image image = blank(61, 37);
  const uint8_t palette[6][4] = {{0, 0, 0, 0},       {34, 32, 52, 255},  {217, 87, 99, 255},
                                 {91, 110, 225, 255}, {251, 242, 54, 255}, {0, 0, 0, 128}};
  std::mt19937 random(3);
  for (uint32_t y = 0; y < image.height; y++) {
    for (uint32_t x = 0; x < image.width; x++) {
      int index = 0;
      const int dx = int(x) - 30, dy = int(y) - 18;
      if (dx * dx + dy * dy < 150) index = 2;
      if (dx * dx + dy * dy < 60) index = 4;
      if (x % 9 == 0 && y > 4 && y < 30) index = 3;
      if (y == 33 && x > 10 && x < 50) index = 5;
      if (dx * dx + dy * dy >= 150 && dx * dx + dy * dy < 170) index = 1;
      if (index == 0 && random() % 17 == 0) index = 1 + int(random() % 4);
      std::memcpy(image.rgba.data() + (size_t(y) * image.width + x) * 4, palette[index], 4);
    }
  }
  return image;
}

Image colourRamp() {
  Image image = blank(256, 256);
  for (uint32_t y = 0; y < 256; y++) {
    for (uint32_t x = 0; x < 256; x++) {
      uint8_t *t = image.rgba.data() + (size_t(y) * 256 + x) * 4;
      t[0] = uint8_t(x);
      t[1] = uint8_t(y);
      t[2] = uint8_t(((x / 8 + y / 8) % 2) ? 200 : 40);
      if (x > 128 && y > 128) {
        // Fine detail: a one-texel checkerboard, which a box filter turns to
        // mush and a windowed sinc keeps as grey without moiré.
        t[0] = t[1] = t[2] = ((x + y) % 2) ? 255 : 0;
      }
    }
  }
  return image;
}

/// Leaves: soft-edged ellipses of binary alpha on nothing, the way foliage
/// cards are painted.
Image leaves() {
  const uint32_t size = 512;
  Image image = blank(size, size);
  for (size_t i = 0; i < image.rgba.size(); i += 4) {
    image.rgba[i] = 20;
    image.rgba[i + 1] = 30;
    image.rgba[i + 2] = 10;
    image.rgba[i + 3] = 0;
  }
  std::mt19937 random(5);
  for (int leaf = 0; leaf < 90; leaf++) {
    const double cx = random() % size, cy = random() % size;
    const double length = 10 + random() % 30, width = 3 + random() % 6;
    const double angle = (random() % 628) / 100.0;
    const double c = std::cos(angle), s = std::sin(angle);
    const uint8_t green = uint8_t(90 + random() % 120);
    for (int y = int(cy - length); y <= int(cy + length); y++) {
      for (int x = int(cx - length); x <= int(cx + length); x++) {
        if (x < 0 || y < 0 || x >= int(size) || y >= int(size)) continue;
        const double u = ((x - cx) * c + (y - cy) * s) / length;
        const double v = (-(x - cx) * s + (y - cy) * c) / width;
        if (u * u + v * v <= 1.0) {
          uint8_t *t = image.rgba.data() + (size_t(y) * size + size_t(x)) * 4;
          t[0] = uint8_t(green / 3);
          t[1] = green;
          t[2] = uint8_t(green / 4);
          t[3] = 255;
        }
      }
    }
  }
  return image;
}

Image bumps() {
  const uint32_t size = 128;
  Image image = blank(size, size);
  const auto height = [](double x, double y) {
    return 0.5 * std::sin(x * 0.3) * std::cos(y * 0.23) + 0.2 * std::sin((x + y) * 0.9);
  };
  for (uint32_t y = 0; y < size; y++) {
    for (uint32_t x = 0; x < size; x++) {
      const double dx = height(x + 1, y) - height(x - 1, y);
      const double dy = height(x, y + 1) - height(x, y - 1);
      double n[3] = {-dx, -dy, 1.0};
      const double length = std::sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
      uint8_t *t = image.rgba.data() + (size_t(y) * size + x) * 4;
      for (int c = 0; c < 3; c++) {
        t[c] = uint8_t(std::lround((n[c] / length * 0.5 + 0.5) * 255.0));
      }
    }
  }
  return image;
}

Image noise() {
  Image image = blank(128, 100);
  std::mt19937 random(9);
  for (size_t i = 0; i < image.rgba.size(); i += 4) {
    const uint32_t x = uint32_t(i / 4 % 128), y = uint32_t(i / 4 / 128);
    image.rgba[i] = uint8_t((x * 2 + y + random() % 24) & 0xFF);
    image.rgba[i + 1] = uint8_t(random());  // ignored: one channel is red
    image.rgba[i + 2] = uint8_t(random());
  }
  return image;
}

struct Fixture {
  std::string name;  // file name as build.sh determinism reads it
  std::vector<uint8_t> bytes;
  Settings settings;
  Image drawn;  // what was drawn, before any file format
};

std::vector<Fixture> fixtures() {
  std::vector<Fixture> list;
  Settings base;
  base.threads = 2;

  Fixture pixel{"pixel.png", {}, base, pixelArt()};
  pixel.settings.lossless = true;
  pixel.bytes = pngOf(pixel.drawn);
  list.push_back(pixel);

  Fixture colour{"colour.png", {}, base, colourRamp()};
  colour.bytes = pngOf(colour.drawn);
  list.push_back(colour);

  Fixture cutout{"cutout.png", {}, base, leaves()};
  cutout.settings.cutout = 0.5f;
  cutout.bytes = pngOf(cutout.drawn);
  list.push_back(cutout);

  Fixture normal{"normal.png", {}, base, bumps()};
  normal.settings.content = Content::kNormal;
  normal.bytes = pngOf(normal.drawn);
  list.push_back(normal);

  Fixture single{"single.png", {}, base, noise()};
  single.settings.content = Content::kSingleChannel;
  single.bytes = pngOf(single.drawn);
  list.push_back(single);

  Fixture jpeg{"colour.jpg", {}, base, colourRamp()};
  jpeg.bytes = jpegOf(jpeg.drawn);
  list.push_back(jpeg);

  // A Basis .ktx2 as input, the way the Bistro's textures arrive: the
  // colour ramp cooked to UASTC with no mips, then cooked again from that.
  Settings basisOnly = base;
  basisOnly.families = kFamilyBasis;
  basisOnly.mips = 0;
  const Cooked intermediate = cook(colour.bytes.data(), colour.bytes.size(), basisOnly);
  if (intermediate.files.size() == 1) {
    Fixture ktx2{"colour_basis.ktx2", intermediate.files[0].bytes, base, colourRamp()};
    list.push_back(ktx2);
  } else {
    expect(false, "the colour ramp cooks to a Basis .ktx2 to use as input: " + intermediate.note);
  }
  return list;
}

// ---------------------------------------------------------------------------
// Checks

/// Reads every file a cook made back and checks it against the levels it
/// was made from: structure, formats for the content, every level decoded.
void checkRoundTrip(const std::string &name, const Cooked &cooked, double floor) {
  const Report &r = cooked.report;
  bool hasAlpha = false;
  for (size_t i = 3; i < cooked.levels.front().rgba.size(); i += 4) {
    hasAlpha = hasAlpha || cooked.levels.front().rgba[i] != 255;
  }
  for (const File &file : cooked.files) {
    const std::string what = name + file.suffix;
    Ktx2File k;
    std::string why;
    const std::vector<uint8_t> bytes(file.bytes);
    if (!readKtx2(bytes, k, why)) {
      expect(false, what + " reads back: " + why);
      continue;
    }
    expect(k.vkFormat == file.vkFormat, what + " has the vkFormat the cooker reported");
    expect(k.width == r.width && k.height == r.height, what + " is the cooked size");
    expect(k.levels.size() == cooked.levels.size(), what + " has every level");
    expect(k.keyValues.count("KTXwriter") == 1 &&
               k.keyValues["KTXorientation"] == std::string("rd", 3),
           what + " names its writer and orientation");
    expect((k.transfer == 2) == (r.srgb && k.vkFormat != 139 && k.vkFormat != 141 &&
                                 k.vkFormat != 153 && k.vkFormat != 155),
           what + " says sRGB exactly when it is colour in sRGB");

    // Which formats which content gets.
    const uint32_t v = k.vkFormat;
    switch (file.family) {
      case kFamilyBasis:
        expect(r.lossless ? (v == 37 || v == 43) : v == 0, what + " is UASTC, or RGBA8 when lossless");
        if (v == 0) {
          const uint32_t channel = r.content == Content::kSingleChannel ? 4 : hasAlpha ? 3 : 0;
          expect(k.uastcChannel == channel, what + "'s UASTC channels say RGB, RGBA or RRR as the texels are");
        }
        break;
      case kFamilyAstc:
        expect(v == 157 || v == 158, what + " is ASTC 4x4");
        break;
      case kFamilyBc:
        expect(r.content == Content::kNormal          ? v == 141
               : r.content == Content::kSingleChannel ? v == 139
                                                      : (v == 145 || v == 146),
               what + " is BC5 for normals, BC4 for one channel, BC7 for colour");
        break;
      case kFamilyEtc2:
        expect(r.content == Content::kNormal          ? v == 155
               : r.content == Content::kSingleChannel ? v == 153
               : hasAlpha                             ? (v == 151 || v == 152)
                                                      : (v == 147 || v == 148),
               what + " is EAC RG11, EAC R11, or ETC2 RGBA8/RGB8 as the texels need");
        break;
    }

    for (uint32_t i = 0; i < k.levels.size() && i < cooked.levels.size(); i++) {
      Image decoded;
      if (!decodeLevel(k, i, decoded)) {
        expect(false, what + " level " + std::to_string(i) + " decodes");
        continue;
      }
      const Image &source = cooked.levels[i];
      expect(decoded.width == source.width && decoded.height == source.height,
             what + " level " + std::to_string(i) + " is the size of its source level");
      const double p = psnr(decoded, source, channelsCompared(r.content, v, hasAlpha));
      if (r.lossless) {
        expect(decoded.rgba == source.rgba, what + " level " + std::to_string(i) + " is lossless");
      } else if (source.width * source.height >= 4096) {
        // Floors that catch a wrong block layout, channel order or
        // descriptor — which come out below 15 dB — rather than grade the
        // encoders; --measure is for grading. ETC1-based blocks are held to
        // less: one hue per half-block is all they have.
        const bool etc1 = v == 147 || v == 148 || v == 151 || v == 152;
        const double want = etc1 ? 18.0 : floor;
        char text[160];
        std::snprintf(text, sizeof(text), "%s level %u decodes to %.1f dB of its source (want %.0f)",
                      what.c_str(), i, p, want);
        expect(p >= want, text);
      }
    }
  }
}

void checkFixtures(const std::vector<Fixture> &list) {
  for (const Fixture &fixture : list) {
    const Cooked cooked = cook(fixture.bytes.data(), fixture.bytes.size(), fixture.settings);
    if (cooked.files.empty()) {
      expect(false, fixture.name + " cooks: " + cooked.note);
      continue;
    }
    const size_t expectedFiles = fixture.settings.lossless ? 1 : 4;
    expect(cooked.files.size() == expectedFiles,
           fixture.name + " cooks to " + std::to_string(expectedFiles) + " file(s)");
    uint32_t most = 1;
    for (uint32_t s = std::max(cooked.report.width, cooked.report.height); s > 1; s >>= 1) most++;
    expect(cooked.report.levels == (fixture.settings.lossless ? 1 : most),
           fixture.name + " has a full mip chain, or one level when lossless");
    // JPEG and a Basis re-cook start from lossy pixels; their PSNR is still
    // against the level they were encoded from.
    checkRoundTrip(fixture.name, cooked, fixture.settings.content == Content::kNormal ? 30.0 : 28.0);

    // Determinism in one process: one thread against seven.
    Settings one = fixture.settings, seven = fixture.settings;
    one.threads = 1;
    seven.threads = 7;
    const Cooked a = cook(fixture.bytes.data(), fixture.bytes.size(), one);
    const Cooked b = cook(fixture.bytes.data(), fixture.bytes.size(), seven);
    bool same = a.files.size() == b.files.size() && a.files.size() == cooked.files.size();
    for (size_t i = 0; same && i < a.files.size(); i++) {
      same = a.files[i].bytes == b.files[i].bytes && a.files[i].bytes == cooked.files[i].bytes;
    }
    expect(same, fixture.name + " cooks to the same bytes at one, two and seven threads");
  }
}

/// Pixel art: the drawn pixels, written as a PNG, cooked losslessly and read
/// back, are the drawn pixels — and the same as a second PNG decoder, Basis
/// Universal's, reads the file.
void checkLossless() {
  const Image drawn = pixelArt();
  const std::vector<uint8_t> png = pngOf(drawn);
  Settings settings;
  settings.lossless = true;
  const Cooked cooked = cook(png.data(), png.size(), settings);
  if (cooked.files.size() != 1) {
    expect(false, "pixel art cooks losslessly: " + cooked.note);
    return;
  }
  Ktx2File k;
  std::string why;
  if (!readKtx2(cooked.files[0].bytes, k, why)) {
    expect(false, "the lossless file reads back: " + why);
    return;
  }
  Image decoded;
  expect(decodeLevel(k, 0, decoded) && decoded.rgba == drawn.rgba,
         "lossless pixel art is the drawn pixels, bit for bit");
  expect(k.vkFormat == 43 && k.levels.size() == 1 && cooked.files[0].suffix == ".ktx2",
         "lossless pixel art is R8G8B8A8_SRGB, one level, x.ktx2 alone");

  basisu::image other;
  expect(basisu::load_png(png.data(), png.size(), other) && other.get_width() == decoded.width &&
             std::memcmp(other.get_ptr(), decoded.rgba.data(), decoded.rgba.size()) == 0,
         "lossless pixel art matches the PNG as Basis Universal's own PNG reader reads it");
  size_t differing = 0;
  for (size_t i = 0; i < decoded.rgba.size(); i++) differing += decoded.rgba[i] != drawn.rgba[i];
  std::printf("lossless: %ux%u pixel art, %zu of %zu bytes differ from the PNG\n", drawn.width,
              drawn.height, differing, drawn.rgba.size());
}

/// A plain 2x2 box average of alpha — the mip chain a cooker with no idea
/// about coverage makes — for comparison.
std::vector<double> boxCoverage(const Image &top, float cutoff) {
  std::vector<double> coverage;
  Image level = top;
  coverage.push_back(coverageOf(level, cutoff));
  while (level.width > 1 || level.height > 1) {
    Image next = blank(std::max<uint32_t>(1, level.width / 2), std::max<uint32_t>(1, level.height / 2));
    for (uint32_t y = 0; y < next.height; y++) {
      for (uint32_t x = 0; x < next.width; x++) {
        uint32_t sum = 0, n = 0;
        for (uint32_t j = 0; j < 2; j++) {
          for (uint32_t i = 0; i < 2; i++) {
            const uint32_t sx = std::min(level.width - 1, x * 2 + i);
            const uint32_t sy = std::min(level.height - 1, y * 2 + j);
            sum += level.rgba[(size_t(sy) * level.width + sx) * 4 + 3];
            n++;
          }
        }
        next.rgba[(size_t(y) * next.width + x) * 4 + 3] = uint8_t((sum + n / 2) / n);
      }
    }
    level = next;
    coverage.push_back(coverageOf(level, cutoff));
  }
  return coverage;
}

void checkCoverage() {
  const Image drawn = leaves();
  const std::vector<uint8_t> png = pngOf(drawn);
  Settings settings;
  settings.cutout = 0.5f;
  const Cooked cooked = cook(png.data(), png.size(), settings);
  if (cooked.files.empty()) {
    expect(false, "the leaves cook: " + cooked.note);
    return;
  }
  const Report &r = cooked.report;
  const std::vector<double> box = boxCoverage(drawn, 0.5f);
  std::printf("coverage of a %ux%u cut-out at 0.5, level 0 %.4f:\n", drawn.width, drawn.height,
              r.targetCoverage);
  std::printf("  level  texels   kept    kaiser  box    ");
  for (const File &file : cooked.files) std::printf(" %-9s", file.format.c_str());
  std::printf("\n");

  std::vector<Image> decodedLevels[4];
  for (size_t f = 0; f < cooked.files.size() && f < 4; f++) {
    Ktx2File k;
    std::string why;
    if (readKtx2(cooked.files[f].bytes, k, why)) {
      for (uint32_t i = 0; i < k.levels.size(); i++) {
        Image image;
        decodeLevel(k, i, image);
        decodedLevels[f].push_back(image);
      }
    }
  }
  double worstKept = 0.0, worstKaiser = 0.0, worstBox = 0.0;
  for (size_t i = 0; i < r.coverage.size(); i++) {
    const double texels = double(cooked.levels[i].width) * cooked.levels[i].height;
    const double kept = r.coverage[i];
    std::printf("  %5zu %7.0f  %.4f  %.4f  %.4f ", i, texels, kept, r.naiveCoverage[i],
                i < box.size() ? box[i] : -1.0);
    for (size_t f = 0; f < cooked.files.size() && f < 4; f++) {
      std::printf(" %.4f   ",
                  i < decodedLevels[f].size() ? coverageOf(decodedLevels[f][i], 0.5f) : -1.0);
    }
    std::printf("\n");
    // Held to within one texel of level 0's fraction, however small the
    // level — the nearest the fraction can be.
    expect(std::fabs(kept - r.targetCoverage) * texels <= 1.0 + 1e-9,
           "level " + std::to_string(i) + " keeps level 0's coverage to within a texel");
    if (texels >= 1024) {
      worstKept = std::max(worstKept, std::fabs(kept - r.targetCoverage));
      worstKaiser = std::max(worstKaiser, std::fabs(r.naiveCoverage[i] - r.targetCoverage));
      if (i < box.size()) worstBox = std::max(worstBox, std::fabs(box[i] - r.targetCoverage));
    }
  }
  std::printf("  largest drift over levels of 1024 texels or more: kept %.4f, kaiser alone "
              "%.4f, box %.4f\n",
              worstKept, worstKaiser, worstBox);
}

void checkRefusals(const std::vector<Fixture> &list) {
  const auto refused = [](const std::vector<uint8_t> &bytes, const Settings &settings,
                          const std::string &what) {
    const Cooked cooked = cook(bytes.data(), bytes.size(), settings);
    expect(cooked.files.empty() && !cooked.note.empty(), what + " is refused with a reason");
  };
  Settings plain;
  plain.threads = 2;
  refused({}, plain, "nothing at all");
  refused({1, 2, 3, 4, 5, 6, 7, 8, 9, 10}, plain, "ten bytes of nothing");
  const std::vector<uint8_t> &png = list[1].bytes;
  refused(std::vector<uint8_t>(png.begin(), png.begin() + 40), plain, "a PNG cut short");

  // A PNG whose header says 100000 wide: refused before it is inflated.
  std::vector<uint8_t> wide = png;
  wide[16] = 0x00;
  wide[17] = 0x01;
  wide[18] = 0x86;
  wide[19] = 0xA0;
  refused(wide, plain, "a PNG claiming to be 100000 texels wide");

  // An already-cooked GPU file as input.
  const Cooked cooked = cook(png.data(), png.size(), plain);
  for (const File &file : cooked.files) {
    if (file.family == kFamilyBc) refused(file.bytes, plain, "a BC7 .ktx2 as input");
  }

  Settings s = plain;
  s.lossless = true;
  s.content = Content::kNormal;
  refused(png, s, "a lossless normal map");
  s = plain;
  s.lossless = true;
  s.families = kFamilyBc;
  refused(png, s, "a lossless cook with no x.ktx2");
  s = plain;
  s.lossless = true;
  s.maxSize = 64;
  refused(png, s, "a lossless cook asked to shrink");
  s = plain;
  s.content = Content::kNormal;
  s.transfer = Transfer::kSrgb;
  refused(png, s, "an sRGB normal map");
  s = plain;
  s.cutout = 1.5f;
  refused(png, s, "a cut-out threshold of 1.5");
  s = plain;
  s.cutout = 0.5f;
  s.content = Content::kSingleChannel;
  refused(png, s, "a single-channel cut-out");
  s = plain;
  s.zstdLevel = 40;
  refused(png, s, "zstd level 40");
  s = plain;
  s.families = 0;
  refused(png, s, "no families");

  // Basis .ktx2 marked sRGB, cooked as a normal map without saying linear.
  for (const Fixture &fixture : list) {
    if (fixture.name == "colour_basis.ktx2") {
      s = plain;
      s.content = Content::kNormal;
      refused(fixture.bytes, s, "a .ktx2 that says sRGB, cooked as a normal map");
      s.transfer = Transfer::kLinear;
      expect(!cook(fixture.bytes.data(), fixture.bytes.size(), s).files.empty(),
             "the same, told --linear, cooks");
    }
  }

  // --max-size: the top levels go, and what is left is the same chain.
  s = plain;
  s.maxSize = 64;
  const Cooked small = cook(png.data(), png.size(), s);
  expect(small.report.width == 64 && small.report.levels == 7 &&
             small.levels.front().rgba == cooked.levels[2].rgba,
         "--max-size 64 drops a 256 texture's top two levels and keeps the rest as they were");
  s.mips = 0;
  const Cooked one = cook(png.data(), png.size(), s);
  expect(one.report.width == 64 && one.report.levels == 1,
         "--max-size 64 --no-mips is one 64 level");
}

// ---------------------------------------------------------------------------
// Fuzzing

struct FuzzCounts {
  uint32_t cooked = 0;
  uint32_t refused = 0;
  uint32_t readRefused = 0;
  uint32_t read = 0;
};

void mutate(std::vector<uint8_t> &bytes, std::mt19937 &random) {
  if (bytes.empty()) return;
  const uint32_t kind = random() % 6;
  const uint32_t edits = 1 + random() % 8;
  for (uint32_t e = 0; e < edits && !bytes.empty(); e++) {
    // Early bytes more often: headers are where the lengths and sizes are.
    size_t at = (random() % 2) ? random() % std::min<size_t>(bytes.size(), 256)
                               : random() % bytes.size();
    switch (kind) {
      case 0:
      case 1:
        bytes[at] = uint8_t(random());
        break;
      case 2:
        bytes[at] ^= uint8_t(1u << (random() % 8));
        break;
      case 3: {
        static const uint32_t interesting[] = {0, 1, 0x7F, 0x80, 0xFF, 0xFFFF, 0x10000,
                                               0x7FFFFFFF, 0x80000000, 0xFFFFFFFF, 4096,
                                               16385};
        const uint32_t v = interesting[random() % (sizeof(interesting) / sizeof(interesting[0]))];
        at &= ~size_t(3);
        for (int i = 0; i < 4 && at + i < bytes.size(); i++) bytes[at + i] = uint8_t(v >> (8 * i));
        break;
      }
      case 4:
        bytes.resize(1 + random() % bytes.size());
        break;
      case 5:
        bytes.insert(bytes.begin() + std::ptrdiff_t(at), bytes.begin(),
                     bytes.begin() + std::ptrdiff_t(std::min<size_t>(bytes.size() - at, 1 + random() % 64)));
        break;
    }
  }
}

FuzzCounts fuzz(uint32_t count, const std::vector<Fixture> &list) {
  FuzzCounts counts;
  // Small inputs, so thousands of cooks take seconds rather than hours, and
  // the cooked files of each, to scribble on for the reader.
  std::vector<std::vector<uint8_t>> inputs;
  std::vector<std::vector<uint8_t>> outputs;
  Settings quick;
  quick.threads = 1;
  quick.uastcLevel = 0;
  quick.zstdLevel = 1;
  for (const Fixture &fixture : list) {
    Image small = fixture.drawn;
    if (small.width > 64) {
      // The top-left corner of it.
      Image corner = blank(48, 40);
      for (uint32_t y = 0; y < 40; y++) {
        std::memcpy(corner.rgba.data() + size_t(y) * 48 * 4,
                    small.rgba.data() + size_t(y) * small.width * 4, 48 * 4);
      }
      small = corner;
    }
    if (fixture.name.find(".jpg") != std::string::npos) {
      inputs.push_back(jpegOf(small));
    } else if (fixture.name.find(".ktx2") != std::string::npos) {
      Settings basis = quick;
      basis.families = kFamilyBasis;
      const std::vector<uint8_t> png = pngOf(small);
      const Cooked c = cook(png.data(), png.size(), basis);
      if (!c.files.empty()) inputs.push_back(c.files[0].bytes);
    } else {
      inputs.push_back(pngOf(small));
    }
    Settings settings = fixture.settings;
    settings.threads = 1;
    settings.uastcLevel = 0;
    settings.zstdLevel = 1;
    const Cooked c = cook(inputs.back().data(), inputs.back().size(), settings);
    for (const File &file : c.files) outputs.push_back(file.bytes);
  }

  std::mt19937 random(20260916);
  for (uint32_t i = 0; i < count; i++) {
    const bool reader = (i % 3) == 2 && !outputs.empty();
    std::vector<uint8_t> bytes = reader ? outputs[random() % outputs.size()]
                                        : inputs[random() % inputs.size()];
    mutate(bytes, random);
    if (reader) {
      Ktx2File k;
      std::string why;
      if (readKtx2(bytes, k, why)) {
        counts.read++;
        Image image;
        for (uint32_t l = 0; l < k.levels.size(); l++) decodeLevel(k, l, image);
      } else {
        counts.readRefused++;
      }
      // The cooker is handed the same bytes: a GPU-format KTX2 is refused,
      // and a damaged one must be refused just as cleanly.
    }
    Settings settings = quick;
    settings.content = Content(random() % 3);
    settings.cutout = settings.content == Content::kColour && random() % 2 ? 0.5f : -1.0f;
    settings.lossless = settings.content == Content::kColour && random() % 5 == 0;
    settings.transfer = settings.content == Content::kColour ? Transfer::kInfer : Transfer::kLinear;
    const Cooked cooked = cook(bytes.data(), bytes.size(), settings);
    if (cooked.files.empty()) {
      counts.refused++;
    } else {
      counts.cooked++;
    }
  }
  return counts;
}

// ---------------------------------------------------------------------------
// Real textures

bool readFile(const std::string &path, std::vector<uint8_t> &bytes) {
  std::ifstream file(path, std::ios::binary);
  if (!file) return false;
  bytes.assign(std::istreambuf_iterator<char>(file), std::istreambuf_iterator<char>());
  return !file.bad();
}

bool endsWith(const std::string &text, const std::string &end) {
  return text.size() >= end.size() &&
         text.compare(text.size() - end.size(), end.size(), end) == 0;
}

void checkSamples(const std::string &directory) {
  if (directory.empty()) {
    std::printf("real textures: skipped (set ORBLIT_TEXTURE_SAMPLES to a directory of "
                ".png, .jpg or .ktx2)\n");
    return;
  }
  DIR *dir = opendir(directory.c_str());
  if (dir == nullptr) {
    expect(false, "ORBLIT_TEXTURE_SAMPLES names a directory: " + directory);
    return;
  }
  std::vector<std::string> names;
  while (dirent *entry = readdir(dir)) {
    const std::string name = entry->d_name;
    if (endsWith(name, ".png") || endsWith(name, ".jpg") || endsWith(name, ".JPG") ||
        (endsWith(name, ".ktx2") && name.find('.') == name.size() - 5)) {
      names.push_back(name);
    }
  }
  closedir(dir);
  std::sort(names.begin(), names.end());
  if (names.size() > 12) names.resize(12);
  for (const std::string &name : names) {
    std::vector<uint8_t> bytes;
    if (!readFile(directory + "/" + name, bytes)) continue;
    Settings settings;
    if (name.find("Normal") != std::string::npos) settings.content = Content::kNormal;
    Cooked cooked = cook(bytes.data(), bytes.size(), settings);
    if (cooked.files.empty()) {
      expect(false, name + " cooks: " + cooked.note);
      continue;
    }
    checkRoundTrip(name, cooked, 24.0);
    std::printf("real texture %s: %ux%u, %u levels, cooked and read back\n", name.c_str(),
                cooked.report.width, cooked.report.height, cooked.report.levels);
    if (endsWith(name, ".png")) {
      settings = Settings();
      settings.lossless = true;
      cooked = cook(bytes.data(), bytes.size(), settings);
      if (cooked.files.empty()) {
        std::printf("  (not lossless: %s)\n", cooked.note.c_str());
        continue;
      }
      Ktx2File k;
      std::string why;
      Image decoded;
      basisu::image other;
      expect(readKtx2(cooked.files[0].bytes, k, why) && decodeLevel(k, 0, decoded) &&
                 basisu::load_png(bytes.data(), bytes.size(), other) &&
                 other.get_width() == decoded.width && other.get_height() == decoded.height &&
                 std::memcmp(other.get_ptr(), decoded.rgba.data(), decoded.rgba.size()) == 0,
             name + " cooked losslessly is the PNG bit for bit, as another decoder reads it");
    }
  }
}

// ---------------------------------------------------------------------------
// Measuring

int measure(int argc, char **argv) {
  if (argc < 3) {
    std::fprintf(stderr, "usage: orblit_texture_cook_check --measure FILE [--normal] "
                         "[--single-channel] [--cutout T] [--linear] [--threads N] "
                         "[--uastc L] [--zstd L]\n");
    return 2;
  }
  const std::string path = argv[2];
  std::vector<uint8_t> bytes;
  if (!readFile(path, bytes)) {
    std::fprintf(stderr, "cannot read %s\n", path.c_str());
    return 1;
  }
  Settings settings;
  for (int i = 3; i < argc; i++) {
    const std::string name = argv[i];
    if (name == "--normal") settings.content = Content::kNormal;
    else if (name == "--single-channel") settings.content = Content::kSingleChannel;
    else if (name == "--linear") settings.transfer = Transfer::kLinear;
    else if (name == "--cutout" && i + 1 < argc) settings.cutout = float(std::atof(argv[++i]));
    else if (name == "--threads" && i + 1 < argc) settings.threads = uint32_t(std::atoi(argv[++i]));
    else if (name == "--uastc" && i + 1 < argc) settings.uastcLevel = std::atoi(argv[++i]);
    else if (name == "--zstd" && i + 1 < argc) settings.zstdLevel = std::atoi(argv[++i]);
    else {
      std::fprintf(stderr, "%s?\n", name.c_str());
      return 2;
    }
  }

  const auto run = [&](const Settings &s, const char *label) {
    const auto from = std::chrono::steady_clock::now();
    const Cooked cooked = cook(bytes.data(), bytes.size(), s);
    const double seconds =
        std::chrono::duration<double>(std::chrono::steady_clock::now() - from).count();
    if (cooked.files.empty()) {
      std::printf("%s: %s\n", label, cooked.note.c_str());
      return;
    }
    const Report &r = cooked.report;
    bool hasAlpha = false;
    for (size_t i = 3; i < cooked.levels.front().rgba.size(); i += 4) {
      hasAlpha = hasAlpha || cooked.levels.front().rgba[i] != 255;
    }
    const uint32_t middle = uint32_t(cooked.levels.size() / 2);
    std::printf("%s: %s, %ux%u, %u levels, %s, %u threads, %.2f s (decode %.2f, mips %.2f, "
                "UASTC %.2f, families %.2f, zstd %.2f)\n",
                label, path.c_str(), r.width, r.height, r.levels,
                r.content == Content::kNormal ? "normal" : r.content == Content::kSingleChannel ? "single channel" : hasAlpha ? "colour with alpha" : "colour",
                r.threads, seconds, r.decodeSeconds, r.mipSeconds, r.uastcSeconds,
                r.familySeconds, r.compressSeconds);
    std::printf("  %-12s %10s %9s %13s %13s %13s\n", "file", "format", "MB",
                "PSNR level 0", "level", "alpha lvl 0");
    for (const File &file : cooked.files) {
      Ktx2File k;
      std::string why;
      if (!readKtx2(file.bytes, k, why)) {
        std::printf("  %s: %s\n", file.suffix.c_str(), why.c_str());
        continue;
      }
      Image top, mid;
      decodeLevel(k, 0, top);
      decodeLevel(k, middle, mid);
      const uint32_t channels = channelsCompared(r.content, k.vkFormat, false);
      char middleText[32];
      std::snprintf(middleText, sizeof(middleText), "%u: %.2f", middle,
                    psnr(mid, cooked.levels[middle], channels));
      char alphaText[32] = "-";
      if (hasAlpha && r.content == Content::kColour) {
        std::snprintf(alphaText, sizeof(alphaText), "%.2f", psnr(top, cooked.levels[0], 0x8));
      }
      std::printf("  %-12s %10s %9.3f %13.2f %13s %13s\n", file.suffix.c_str(),
                  file.format.c_str(), double(file.bytes.size()) / (1024.0 * 1024.0),
                  psnr(top, cooked.levels[0], channels), middleText, alphaText);
    }
    if (r.cutout > 0.0f) {
      std::printf("  coverage at %.2f, level 0 %.4f; kept per level:", double(r.cutout),
                  r.targetCoverage);
      for (double c : r.coverage) std::printf(" %.4f", c);
      std::printf("\n  plain filter:");
      for (double c : r.naiveCoverage) std::printf(" %.4f", c);
      std::printf("\n");
      // And as each family's blocks hold it, which is what the GPU tests.
      for (const File &file : cooked.files) {
        Ktx2File k;
        std::string why;
        if (!readKtx2(file.bytes, k, why)) continue;
        std::printf("  %-12s decoded:", file.suffix.c_str());
        for (uint32_t i = 0; i < k.levels.size(); i++) {
          Image level;
          decodeLevel(k, i, level);
          std::printf(" %.4f", coverageOf(level, r.cutout));
        }
        std::printf("\n");
      }
    }
  };

  Settings transcoded = settings;
  transcoded.astc = AstcRoute::kTranscoded;
  run(transcoded, "UASTC, then transcoded");
  Settings direct = settings;
  direct.astc = AstcRoute::kDirect;
  direct.families = kFamilyAstc;
  run(direct, "ASTC encoded directly");
  return failures > 0 ? 1 : 0;
}

/// Reads cooked files from disk the way the renderer's loader would be
/// checked against: the same reader and decoders the round trip uses.
int readFiles(int argc, char **argv) {
  int bad = 0;
  uint64_t bytes = 0;
  for (int i = 2; i < argc; i++) {
    std::vector<uint8_t> data;
    Ktx2File k;
    std::string why;
    if (!readFile(argv[i], data)) {
      why = "cannot read the file";
    } else if (readKtx2(data, k, why)) {
      Image image;
      for (uint32_t l = 0; l < k.levels.size() && why.empty(); l++) {
        if (!decodeLevel(k, l, image)) why = "level " + std::to_string(l) + " does not decode";
      }
    }
    if (!why.empty()) {
      std::printf("BAD  %s: %s\n", argv[i], why.c_str());
      bad++;
      continue;
    }
    bytes += data.size();
    Expected e;
    const char *name = k.vkFormat == 0 ? "UASTC" : expectedFor(k.vkFormat, e) ? e.name : "?";
    std::printf("ok   %s: %s, %ux%u, %zu levels, %s, %.3f MB\n", argv[i], name, k.width, k.height,
                k.levels.size(), k.transfer == 2 ? "sRGB" : "linear",
                double(data.size()) / (1024.0 * 1024.0));
  }
  std::printf("%d of %d files read and decoded, %.1f MB\n", argc - 2 - bad, argc - 2,
              double(bytes) / (1024.0 * 1024.0));
  return bad > 0 ? 1 : 0;
}

bool writeFixtures(const std::string &directory) {
  for (const Fixture &fixture : fixtures()) {
    const std::string path = directory + "/" + fixture.name;
    std::ofstream file(path, std::ios::binary);
    file.write(reinterpret_cast<const char *>(fixture.bytes.data()),
               std::streamsize(fixture.bytes.size()));
    if (!file.good()) {
      std::fprintf(stderr, "cannot write %s\n", path.c_str());
      return false;
    }
  }
  return true;
}

}  // namespace

int main(int argc, char **argv) {
  const std::string mode = argc > 1 ? argv[1] : "";
  if (mode == "--measure") return measure(argc, argv);
  if (mode == "--read") return readFiles(argc, argv);
  if (mode == "--write-fixtures") {
    if (argc < 3) return 2;
    return writeFixtures(argv[2]) ? 0 : 1;
  }

  const std::vector<Fixture> list = fixtures();
  if (mode == "--fuzz") {
    const uint32_t count = argc > 2 ? uint32_t(std::strtoul(argv[2], nullptr, 10)) : 3000;
    const auto from = std::chrono::steady_clock::now();
    const FuzzCounts counts = fuzz(count, list);
    std::printf("fuzz: %u mutated inputs in %.1f s: the cooker refused %u and cooked %u; the "
                "reader refused %u and read %u; nothing crashed\n",
                count,
                std::chrono::duration<double>(std::chrono::steady_clock::now() - from).count(),
                counts.refused, counts.cooked, counts.readRefused, counts.read);
    return failures > 0 ? 1 : 0;
  }
  if (!mode.empty()) {
    std::fprintf(stderr, "usage: orblit_texture_cook_check [--fuzz N | --write-fixtures DIR | "
                         "--read FILE... | --measure FILE [flags]]\n");
    return 2;
  }

  checkFixtures(list);
  checkLossless();
  checkCoverage();
  checkRefusals(list);
  const FuzzCounts counts = fuzz(200, list);
  std::printf("fuzz: 200 mutated inputs: the cooker refused %u and cooked %u; the reader "
              "refused %u and read %u\n",
              counts.refused, counts.cooked, counts.readRefused, counts.read);
  const char *samples = std::getenv("ORBLIT_TEXTURE_SAMPLES");
  checkSamples(samples ? samples : "");

  if (failures > 0) {
    std::fprintf(stderr, "%d texture cook check(s) failed\n", failures);
    return 1;
  }
  std::printf("the texture cooker holds\n");
  return 0;
}
