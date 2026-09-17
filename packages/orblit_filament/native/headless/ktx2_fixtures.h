// KTX 2 files written in memory, for the texture checks.
//
// Nothing binary is committed: every fixture is a solid colour in a block
// format simple enough to write by hand, so the colour a GPU samples is known
// exactly — an ASTC void-extent block, a BC7 mode-6 block whose endpoints are
// the same, a BC1 block with both endpoints equal, an ETC2 block in individual
// mode — wrapped in a KTX 2 file with or without zstd on each level.
#pragma once

#include <cstdint>
#include <cstring>
#include <functional>
#include <string>
#include <vector>

#include "third_party/zstd/zstd.h"

namespace fixtures {

struct Rgba {
  uint8_t r, g, b, a;
};

inline size_t blocksIn(uint32_t width, uint32_t height, uint32_t blockWidth,
                       uint32_t blockHeight) {
  return size_t((width + blockWidth - 1) / blockWidth) *
         size_t((height + blockHeight - 1) / blockHeight);
}

/// ASTC's void-extent block: one colour, sixteen bits a channel.
inline void astcBlock(uint8_t *to, Rgba c) {
  constexpr uint8_t kHead[8] = {0xFC, 0xFD, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF};
  memcpy(to, kHead, 8);
  const uint8_t channels[4] = {c.r, c.g, c.b, c.a};
  for (int i = 0; i < 4; i++) {
    const uint16_t wide = uint16_t(channels[i] * 257);
    to[8 + 2 * i] = uint8_t(wide);
    to[9 + 2 * i] = uint8_t(wide >> 8);
  }
}

/// BC7 mode 6 with both endpoints `c`. Each endpoint channel is seven bits
/// and a p-bit shared by the whole endpoint, so a colour is exact when all
/// four of its channels are odd or all are even.
inline void bc7Block(uint8_t *to, Rgba c) {
  memset(to, 0, 16);
  uint32_t at = 0;
  const auto put = [&](uint32_t value, uint32_t bits) {
    for (uint32_t i = 0; i < bits; i++, at++) {
      if ((value >> i) & 1) to[at / 8] |= uint8_t(1 << (at % 8));
    }
  };
  put(1 << 6, 7);
  const uint8_t channels[4] = {c.r, c.g, c.b, c.a};
  for (uint8_t channel : channels) {
    put(channel >> 1, 7);
    put(channel >> 1, 7);
  }
  put(c.r & 1, 1);
  put(c.r & 1, 1);
  // Indices, all nought: endpoint zero everywhere.
}

/// BC1 with both endpoints the colour as RGB565.
inline void bc1Block(uint8_t *to, Rgba c) {
  const uint16_t packed =
      uint16_t((c.r >> 3) << 11 | (c.g >> 2) << 5 | (c.b >> 3));
  to[0] = to[2] = uint8_t(packed);
  to[1] = to[3] = uint8_t(packed >> 8);
  memset(to + 4, 0, 4);
}

/// ETC2 in individual mode: four bits of base a channel, expanded by
/// seventeen, plus two from table nought's first modifier. So the colour
/// drawn is `nibble * 17 + 2`, clamped.
inline void etc2Block(uint8_t *to, Rgba c) {
  const uint8_t r = c.r / 17;
  const uint8_t g = c.g / 17;
  const uint8_t b = c.b / 17;
  to[0] = uint8_t(r << 4 | r);
  to[1] = uint8_t(g << 4 | g);
  to[2] = uint8_t(b << 4 | b);
  to[3] = 0;
  memset(to + 4, 0, 4);
}

/// ETC2 RGBA8: an EAC alpha block of the alpha, with a multiplier of nought,
/// then the colour as etc2Block.
inline void etc2RgbaBlock(uint8_t *to, Rgba c) {
  memset(to, 0, 8);
  to[0] = c.a;
  etc2Block(to + 8, c);
}

/// What etc2Block's colour draws as.
inline uint8_t etc2Drawn(uint8_t value) {
  const int drawn = (value / 17) * 17 + 2;
  return uint8_t(drawn > 255 ? 255 : drawn);
}

struct Kind {
  uint32_t vkFormat;
  uint32_t blockWidth;
  uint32_t blockHeight;
  uint32_t bytesPerBlock;
  uint32_t typeSize;
  uint8_t colourModel;
  std::function<void(uint8_t *, Rgba)> block;
};

inline Kind astc4x4(bool srgb) {
  return {srgb ? 158u : 157u, 4, 4, 16, 1, 162, astcBlock};
}
inline Kind bc7(bool srgb) {
  return {srgb ? 146u : 145u, 4, 4, 16, 1, 134, bc7Block};
}
inline Kind bc1(bool srgb) {
  return {srgb ? 132u : 131u, 4, 4, 8, 1, 128, bc1Block};
}
inline Kind etc2(bool srgb) {
  return {srgb ? 148u : 147u, 4, 4, 8, 1, 161, etc2Block};
}
inline Kind etc2Rgba(bool srgb) {
  return {srgb ? 152u : 151u, 4, 4, 16, 1, 161, etc2RgbaBlock};
}
inline Kind rgba8(bool srgb) {
  return {srgb ? 43u : 37u, 1, 1, 4, 1, 1, [](uint8_t *to, Rgba c) {
            to[0] = c.r;
            to[1] = c.g;
            to[2] = c.b;
            to[3] = c.a;
          }};
}
/// BC3: an alpha block of the alpha, then a BC1 block of the colour.
inline Kind bc3(bool srgb) {
  return {srgb ? 138u : 137u, 4, 4, 16, 1, 130, [](uint8_t *to, Rgba c) {
            to[0] = to[1] = c.a;
            memset(to + 2, 0, 6);
            bc1Block(to + 8, c);
          }};
}
/// BC4 and BC5: both end values the channel, every index nought.
inline Kind bc4() {
  return {139u, 4, 4, 8, 1, 131, [](uint8_t *to, Rgba c) {
            to[0] = to[1] = c.r;
            memset(to + 2, 0, 6);
          }};
}
inline Kind bc5() {
  return {141u, 4, 4, 16, 1, 132, [](uint8_t *to, Rgba c) {
            to[0] = to[1] = c.r;
            memset(to + 2, 0, 6);
            to[8] = to[9] = c.g;
            memset(to + 10, 0, 6);
          }};
}
/// EAC R11 and RG11: the base value, a multiplier of nought.
inline Kind eacR11() {
  return {153u, 4, 4, 8, 1, 161, [](uint8_t *to, Rgba c) {
            memset(to, 0, 8);
            to[0] = c.r;
          }};
}
inline Kind eacRg11() {
  return {155u, 4, 4, 16, 1, 161, [](uint8_t *to, Rgba c) {
            memset(to, 0, 16);
            to[0] = c.r;
            to[8] = c.g;
          }};
}
/// Three channels, which Metal has no texture format for.
inline Kind rgb8() {
  return {23u, 1, 1, 3, 1, 1, [](uint8_t *to, Rgba c) {
            to[0] = c.r;
            to[1] = c.g;
            to[2] = c.b;
          }};
}

/// One level of `kind`, every block `colour`.
inline std::vector<uint8_t> solidLevel(const Kind &kind, uint32_t width,
                                       uint32_t height, Rgba colour) {
  const size_t blocks =
      blocksIn(width, height, kind.blockWidth, kind.blockHeight);
  std::vector<uint8_t> level(blocks * kind.bytesPerBlock);
  for (size_t i = 0; i < blocks; i++) {
    kind.block(level.data() + i * kind.bytesPerBlock, colour);
  }
  return level;
}

struct File {
  Kind kind{};
  uint32_t width = 1;
  uint32_t height = 1;
  /// Largest first.
  std::vector<std::vector<uint8_t>> levels;
  bool zstd = false;
  /// The descriptor's transfer function: 1 linear, 2 sRGB.
  uint8_t transfer = 2;
  bool premultiplied = false;
  /// Six for a cubemap, whose levels then hold every face end to end.
  uint32_t faces = 1;
};

/// A file whose levels are each one solid colour, `colourOf(level)`.
inline File solid(const Kind &kind, uint32_t width, uint32_t height,
                  uint32_t levels, bool zstd,
                  const std::function<Rgba(uint32_t)> &colourOf) {
  File file;
  file.kind = kind;
  file.width = width;
  file.height = height;
  file.zstd = zstd;
  const bool srgbFormat = kind.vkFormat == 158 || kind.vkFormat == 146 ||
                          kind.vkFormat == 132 || kind.vkFormat == 148 ||
                          kind.vkFormat == 152 || kind.vkFormat == 43;
  file.transfer = srgbFormat ? 2 : 1;
  for (uint32_t level = 0; level < levels; level++) {
    const uint32_t w = width >> level ? width >> level : 1;
    const uint32_t h = height >> level ? height >> level : 1;
    file.levels.push_back(solidLevel(kind, w, h, colourOf(level)));
  }
  return file;
}

inline void put32(std::vector<uint8_t> &out, size_t at, uint32_t value) {
  for (int i = 0; i < 4; i++) out[at + i] = uint8_t(value >> (8 * i));
}

inline void put64(std::vector<uint8_t> &out, size_t at, uint64_t value) {
  for (int i = 0; i < 8; i++) out[at + i] = uint8_t(value >> (8 * i));
}

/// The file as KTX 2: header, level index, a basic data format descriptor,
/// and the levels smallest first, each squeezed with zstd if asked.
inline std::vector<uint8_t> write(const File &file) {
  const uint32_t levels = uint32_t(file.levels.size());
  const size_t indexAt = 80;
  const size_t dfdAt = indexAt + 24 * levels;
  constexpr uint32_t kDfdBytes = 44;
  std::vector<uint8_t> out(dfdAt + kDfdBytes, 0);
  constexpr uint8_t kIdentifier[12] = {0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32,
                                       0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A};
  memcpy(out.data(), kIdentifier, 12);
  put32(out, 12, file.kind.vkFormat);
  put32(out, 16, file.kind.typeSize);
  put32(out, 20, file.width);
  put32(out, 24, file.height);
  put32(out, 36, file.faces);
  put32(out, 40, levels);
  put32(out, 44, file.zstd ? 2 : 0);
  put32(out, 48, uint32_t(dfdAt));
  put32(out, 52, kDfdBytes);

  put32(out, dfdAt, kDfdBytes);
  put32(out, dfdAt + 4, 0);                   // Khronos, basic descriptor
  put32(out, dfdAt + 8, 2 | (40u << 16));     // version 1.3, 40 bytes
  out[dfdAt + 12] = file.kind.colourModel;
  out[dfdAt + 13] = 1;                        // BT.709 primaries
  out[dfdAt + 14] = file.transfer;
  out[dfdAt + 15] = file.premultiplied ? 1 : 0;
  out[dfdAt + 16] = uint8_t(file.kind.blockWidth - 1);
  out[dfdAt + 17] = uint8_t(file.kind.blockHeight - 1);
  out[dfdAt + 20] = uint8_t(file.kind.bytesPerBlock);

  for (uint32_t level = levels; level-- > 0;) {
    const std::vector<uint8_t> &raw = file.levels[level];
    std::vector<uint8_t> stored = raw;
    if (file.zstd) {
      stored.resize(ZSTD_compressBound(raw.size()));
      const size_t size = ZSTD_compress(stored.data(), stored.size(),
                                        raw.data(), raw.size(), 3);
      stored.resize(ZSTD_isError(size) ? 0 : size);
    } else {
      while (out.size() % 8 != 0) out.push_back(0);
    }
    const size_t offset = out.size();
    out.insert(out.end(), stored.begin(), stored.end());
    const size_t entry = indexAt + 24 * level;
    put64(out, entry, offset);
    put64(out, entry + 8, stored.size());
    put64(out, entry + 16, raw.size());
  }
  return out;
}

}  // namespace fixtures
