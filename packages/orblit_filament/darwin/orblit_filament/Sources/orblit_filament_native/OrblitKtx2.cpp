#include "OrblitKtx2.h"

#include <algorithm>
#include <cctype>
#include <cinttypes>
#include <cstdarg>
#include <cstdio>
#include <cstring>

// zstd's public header, vendored beside this file (third_party/zstd, from
// zstd 1.5.7, the version Filament builds libzstd.a from on every platform it
// ships). Only the stable API is used — ZSTD_STATIC_LINKING_ONLY stays
// undefined — so a Filament that moves to a later zstd links unchanged.
#include "third_party/zstd/zstd.h"

namespace orblit {
namespace ktx2 {

namespace {

/// KTX 2's identifier: «KTX 20», then the line-ending traps.
constexpr uint8_t kIdentifier[12] = {0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32,
                                     0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A};

/// KTX 1's, so a file of the older kind is told apart from a broken one.
constexpr uint8_t kVersionOneIdentifier[12] = {
    0xAB, 0x4B, 0x54, 0x58, 0x20, 0x31, 0x31, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A};

constexpr size_t kHeaderBytes = 80;
constexpr size_t kLevelIndexEntryBytes = 24;

/// The widest texture any device this renderer starts on can hold. A header
/// claiming more is either broken or aimed at nothing this draws on.
constexpr uint32_t kLargestSide = 16384;

/// The most one file may unpack into. Two gigabytes is past the whole texture
/// memory of every phone and most desktops, so a file asking for more is
/// refused before a byte of it is allocated.
constexpr uint64_t kLargestDecompressed = uint64_t(1) << 31;

/// The data format descriptor's colour models for the two Basis codecs.
constexpr uint8_t kModelEtc1s = 163;
constexpr uint8_t kModelUastc = 166;

using F = Family;

/// Every format read, by vkFormat. The numbers are Vulkan's own and never
/// change; the families are which cooked sibling each belongs in.
constexpr Format kFormats[] = {
    // Uncompressed: what a lossless cook of pixel art is, and what floats
    // for lighting data are.
    {9, "R8_UNORM", 1, 1, 1, 1, false, F::uncompressed},
    {16, "R8G8_UNORM", 1, 1, 2, 1, false, F::uncompressed},
    {23, "R8G8B8_UNORM", 1, 1, 3, 1, false, F::uncompressed},
    {29, "R8G8B8_SRGB", 1, 1, 3, 1, true, F::uncompressed},
    {37, "R8G8B8A8_UNORM", 1, 1, 4, 1, false, F::uncompressed},
    {43, "R8G8B8A8_SRGB", 1, 1, 4, 1, true, F::uncompressed},
    {90, "R16G16B16_SFLOAT", 1, 1, 6, 2, false, F::uncompressed},
    {97, "R16G16B16A16_SFLOAT", 1, 1, 8, 2, false, F::uncompressed},
    {106, "R32G32B32_SFLOAT", 1, 1, 12, 4, false, F::uncompressed},
    {109, "R32G32B32A32_SFLOAT", 1, 1, 16, 4, false, F::uncompressed},
    {122, "B10G11R11_UFLOAT_PACK32", 1, 1, 4, 4, false, F::uncompressed},

    // BC1 to BC7.
    {131, "BC1_RGB_UNORM_BLOCK", 4, 4, 8, 1, false, F::bc},
    {132, "BC1_RGB_SRGB_BLOCK", 4, 4, 8, 1, true, F::bc},
    {133, "BC1_RGBA_UNORM_BLOCK", 4, 4, 8, 1, false, F::bc},
    {134, "BC1_RGBA_SRGB_BLOCK", 4, 4, 8, 1, true, F::bc},
    {135, "BC2_UNORM_BLOCK", 4, 4, 16, 1, false, F::bc},
    {136, "BC2_SRGB_BLOCK", 4, 4, 16, 1, true, F::bc},
    {137, "BC3_UNORM_BLOCK", 4, 4, 16, 1, false, F::bc},
    {138, "BC3_SRGB_BLOCK", 4, 4, 16, 1, true, F::bc},
    {139, "BC4_UNORM_BLOCK", 4, 4, 8, 1, false, F::bc},
    {140, "BC4_SNORM_BLOCK", 4, 4, 8, 1, false, F::bc},
    {141, "BC5_UNORM_BLOCK", 4, 4, 16, 1, false, F::bc},
    {142, "BC5_SNORM_BLOCK", 4, 4, 16, 1, false, F::bc},
    {143, "BC6H_UFLOAT_BLOCK", 4, 4, 16, 1, false, F::bc},
    {144, "BC6H_SFLOAT_BLOCK", 4, 4, 16, 1, false, F::bc},
    {145, "BC7_UNORM_BLOCK", 4, 4, 16, 1, false, F::bc},
    {146, "BC7_SRGB_BLOCK", 4, 4, 16, 1, true, F::bc},

    // ETC2 and EAC.
    {147, "ETC2_R8G8B8_UNORM_BLOCK", 4, 4, 8, 1, false, F::etc2},
    {148, "ETC2_R8G8B8_SRGB_BLOCK", 4, 4, 8, 1, true, F::etc2},
    {149, "ETC2_R8G8B8A1_UNORM_BLOCK", 4, 4, 8, 1, false, F::etc2},
    {150, "ETC2_R8G8B8A1_SRGB_BLOCK", 4, 4, 8, 1, true, F::etc2},
    {151, "ETC2_R8G8B8A8_UNORM_BLOCK", 4, 4, 16, 1, false, F::etc2},
    {152, "ETC2_R8G8B8A8_SRGB_BLOCK", 4, 4, 16, 1, true, F::etc2},
    {153, "EAC_R11_UNORM_BLOCK", 4, 4, 8, 1, false, F::etc2},
    {154, "EAC_R11_SNORM_BLOCK", 4, 4, 8, 1, false, F::etc2},
    {155, "EAC_R11G11_UNORM_BLOCK", 4, 4, 16, 1, false, F::etc2},
    {156, "EAC_R11G11_SNORM_BLOCK", 4, 4, 16, 1, false, F::etc2},

    // ASTC, low dynamic range, every block size.
    {157, "ASTC_4x4_UNORM_BLOCK", 4, 4, 16, 1, false, F::astc},
    {158, "ASTC_4x4_SRGB_BLOCK", 4, 4, 16, 1, true, F::astc},
    {159, "ASTC_5x4_UNORM_BLOCK", 5, 4, 16, 1, false, F::astc},
    {160, "ASTC_5x4_SRGB_BLOCK", 5, 4, 16, 1, true, F::astc},
    {161, "ASTC_5x5_UNORM_BLOCK", 5, 5, 16, 1, false, F::astc},
    {162, "ASTC_5x5_SRGB_BLOCK", 5, 5, 16, 1, true, F::astc},
    {163, "ASTC_6x5_UNORM_BLOCK", 6, 5, 16, 1, false, F::astc},
    {164, "ASTC_6x5_SRGB_BLOCK", 6, 5, 16, 1, true, F::astc},
    {165, "ASTC_6x6_UNORM_BLOCK", 6, 6, 16, 1, false, F::astc},
    {166, "ASTC_6x6_SRGB_BLOCK", 6, 6, 16, 1, true, F::astc},
    {167, "ASTC_8x5_UNORM_BLOCK", 8, 5, 16, 1, false, F::astc},
    {168, "ASTC_8x5_SRGB_BLOCK", 8, 5, 16, 1, true, F::astc},
    {169, "ASTC_8x6_UNORM_BLOCK", 8, 6, 16, 1, false, F::astc},
    {170, "ASTC_8x6_SRGB_BLOCK", 8, 6, 16, 1, true, F::astc},
    {171, "ASTC_8x8_UNORM_BLOCK", 8, 8, 16, 1, false, F::astc},
    {172, "ASTC_8x8_SRGB_BLOCK", 8, 8, 16, 1, true, F::astc},
    {173, "ASTC_10x5_UNORM_BLOCK", 10, 5, 16, 1, false, F::astc},
    {174, "ASTC_10x5_SRGB_BLOCK", 10, 5, 16, 1, true, F::astc},
    {175, "ASTC_10x6_UNORM_BLOCK", 10, 6, 16, 1, false, F::astc},
    {176, "ASTC_10x6_SRGB_BLOCK", 10, 6, 16, 1, true, F::astc},
    {177, "ASTC_10x8_UNORM_BLOCK", 10, 8, 16, 1, false, F::astc},
    {178, "ASTC_10x8_SRGB_BLOCK", 10, 8, 16, 1, true, F::astc},
    {179, "ASTC_10x10_UNORM_BLOCK", 10, 10, 16, 1, false, F::astc},
    {180, "ASTC_10x10_SRGB_BLOCK", 10, 10, 16, 1, true, F::astc},
    {181, "ASTC_12x10_UNORM_BLOCK", 12, 10, 16, 1, false, F::astc},
    {182, "ASTC_12x10_SRGB_BLOCK", 12, 10, 16, 1, true, F::astc},
    {183, "ASTC_12x12_UNORM_BLOCK", 12, 12, 16, 1, false, F::astc},
    {184, "ASTC_12x12_SRGB_BLOCK", 12, 12, 16, 1, true, F::astc},
};

constexpr size_t kFormatCount = sizeof(kFormats) / sizeof(kFormats[0]);

/// The linear and sRGB twins, by vkFormat. Every pair differs only in how the
/// sampler decodes the same bytes.
constexpr uint32_t kTwins[][2] = {
    {23, 29},   {37, 43},   {131, 132}, {133, 134}, {135, 136}, {137, 138},
    {145, 146}, {147, 148}, {149, 150}, {151, 152}, {157, 158}, {159, 160},
    {161, 162}, {163, 164}, {165, 166}, {167, 168}, {169, 170}, {171, 172},
    {173, 174}, {175, 176}, {177, 178}, {179, 180}, {181, 182}, {183, 184},
};

uint32_t u32(const uint8_t *at) {
  return uint32_t(at[0]) | uint32_t(at[1]) << 8 | uint32_t(at[2]) << 16 |
         uint32_t(at[3]) << 24;
}

uint64_t u64(const uint8_t *at) {
  return uint64_t(u32(at)) | uint64_t(u32(at + 4)) << 32;
}

void put32(uint8_t *at, uint32_t value) {
  for (int i = 0; i < 4; i++) at[i] = uint8_t(value >> (8 * i));
}

void put64(uint8_t *at, uint64_t value) {
  for (int i = 0; i < 8; i++) at[i] = uint8_t(value >> (8 * i));
}

/// Whether [offset, offset + length) lies inside `size` bytes, without the
/// addition that overflows when a broken header says a length near 2⁶⁴.
bool inside(uint64_t offset, uint64_t length, size_t size) {
  return offset <= size && length <= uint64_t(size) - offset;
}

/// printf into a string, for refusals.
std::string say(const char *format, ...) {
  char buffer[512];
  va_list arguments;
  va_start(arguments, format);
  vsnprintf(buffer, sizeof buffer, format, arguments);
  va_end(arguments);
  return buffer;
}

uint32_t mostLevels(uint32_t width, uint32_t height) {
  uint32_t side = std::max(width, height);
  uint32_t levels = 1;
  while (side > 1) {
    side >>= 1;
    levels++;
  }
  return levels;
}

bool endsWithIgnoringCase(const std::string &text, const char *suffix) {
  const size_t length = strlen(suffix);
  if (text.size() < length) return false;
  for (size_t i = 0; i < length; i++) {
    const char a = char(tolower(uint8_t(text[text.size() - length + i])));
    if (a != suffix[i]) return false;
  }
  return true;
}

}  // namespace

const Format *formatOf(uint32_t vkFormat) {
  for (const Format &format : kFormats) {
    if (format.vkFormat == vkFormat) return &format;
  }
  return nullptr;
}

const Format *withTransfer(const Format &format, bool srgb) {
  if (format.srgb == srgb) return &format;
  for (const auto &twin : kTwins) {
    if (twin[0] == format.vkFormat || twin[1] == format.vkFormat) {
      return formatOf(srgb ? twin[1] : twin[0]);
    }
  }
  return nullptr;
}

const Format *allFormats(size_t *count) {
  if (count != nullptr) *count = kFormatCount;
  return kFormats;
}

bool isKtx2(const uint8_t *data, size_t size) {
  return data != nullptr && size >= sizeof kIdentifier &&
         memcmp(data, kIdentifier, sizeof kIdentifier) == 0;
}

uint32_t levelWidth(const Header &header, uint32_t level) {
  return std::max<uint32_t>(1, level < 32 ? header.width >> level : 0);
}

uint32_t levelHeight(const Header &header, uint32_t level) {
  return std::max<uint32_t>(1, level < 32 ? header.height >> level : 0);
}

uint64_t levelBytes(const Header &header, uint32_t level) {
  const Format *format = header.format;
  if (format == nullptr) return 0;
  const uint64_t across =
      (uint64_t(levelWidth(header, level)) + format->blockWidth - 1) /
      format->blockWidth;
  const uint64_t down =
      (uint64_t(levelHeight(header, level)) + format->blockHeight - 1) /
      format->blockHeight;
  return across * down * format->bytesPerBlock * header.faces;
}

std::string read(const uint8_t *data, size_t size, Header &out) {
  out = Header{};
  if (data == nullptr || size < kHeaderBytes) {
    return say("It is too short to be a KTX 2 file: %zu bytes, where the "
               "header alone is 80.",
               data == nullptr ? size_t(0) : size);
  }
  if (memcmp(data, kVersionOneIdentifier, sizeof kVersionOneIdentifier) == 0) {
    return "It is a KTX 1 file, and only KTX 2 is read as a texture.";
  }
  if (!isKtx2(data, size)) {
    return "It does not begin with the KTX 2 identifier.";
  }

  out.vkFormat = u32(data + 12);
  out.typeSize = u32(data + 16);
  out.width = u32(data + 20);
  out.height = u32(data + 24);
  const uint32_t depth = u32(data + 28);
  const uint32_t layers = u32(data + 32);
  out.faces = u32(data + 36);
  const uint32_t levelCount = u32(data + 40);
  const uint32_t scheme = u32(data + 44);
  const uint32_t dfdOffset = u32(data + 48);
  const uint32_t dfdLength = u32(data + 52);
  const uint32_t kvdOffset = u32(data + 56);
  const uint32_t kvdLength = u32(data + 60);
  const uint64_t sgdOffset = u64(data + 64);
  const uint64_t sgdLength = u64(data + 72);

  if (out.width == 0 || out.height == 0) {
    return say("It says it is %" PRIu32 " by %" PRIu32
               " texels; one-dimensional textures are not read.",
               out.width, out.height);
  }
  if (out.width > kLargestSide || out.height > kLargestSide) {
    return say("It is %" PRIu32 " by %" PRIu32
               " texels, wider than the %" PRIu32
               " any device this draws on can hold.",
               out.width, out.height, kLargestSide);
  }
  if (depth != 0) return "It is a 3D texture, and those are not read.";
  if (layers > 1) {
    return say("It is an array of %" PRIu32
               " textures, and arrays are not read.",
               layers);
  }
  if (out.faces != 1 && out.faces != 6) {
    return say("It says it has %" PRIu32 " faces; a texture has one and a "
               "cubemap six.",
               out.faces);
  }
  if (out.faces == 6 && out.width != out.height) {
    return "It is a cubemap whose faces are not square.";
  }

  out.generateMipmaps = levelCount == 0;
  out.levels = std::max<uint32_t>(levelCount, 1);
  if (out.levels > mostLevels(out.width, out.height)) {
    return say("It says it has %" PRIu32 " levels, more than a %" PRIu32
               " by %" PRIu32 " texture can.",
               out.levels, out.width, out.height);
  }

  switch (scheme) {
    case uint32_t(Supercompression::none):
    case uint32_t(Supercompression::basisLZ):
    case uint32_t(Supercompression::zstd):
      out.scheme = Supercompression(scheme);
      break;
    case uint32_t(Supercompression::zlib):
      return "Its levels are squeezed with zlib, which is not read; cook it "
             "with zstd.";
    default:
      return say("Its supercompression scheme, %" PRIu32
                 ", is not one KTX 2 defines.",
                 scheme);
  }

  if (!inside(kHeaderBytes, uint64_t(out.levels) * kLevelIndexEntryBytes,
              size)) {
    return "It ends inside its own level index.";
  }
  out.index.resize(out.levels);
  for (uint32_t i = 0; i < out.levels; i++) {
    const uint8_t *entry = data + kHeaderBytes + i * kLevelIndexEntryBytes;
    Level &level = out.index[i];
    level.offset = u64(entry);
    level.length = u64(entry + 8);
    level.uncompressedLength = u64(entry + 16);
    if (level.length == 0 || !inside(level.offset, level.length, size)) {
      return say("Level %" PRIu32 " lies outside the file, which is %zu "
                 "bytes: it is truncated or its index is wrong.",
                 i, size);
    }
  }

  if (kvdLength != 0 && !inside(kvdOffset, kvdLength, size)) {
    return "Its key/value data lies outside the file.";
  }
  if (sgdLength != 0 && !inside(sgdOffset, sgdLength, size)) {
    return "Its supercompression data lies outside the file.";
  }

  // The data format descriptor: its first block, if it is the basic one
  // Khronos defines, says the colour model, the transfer function and whether
  // alpha is premultiplied. A file without one is out of spec but still
  // readable when its vkFormat says everything the descriptor would.
  if (dfdLength != 0) {
    if (!inside(dfdOffset, dfdLength, size)) {
      return "Its data format descriptor lies outside the file.";
    }
    const uint8_t *dfd = data + dfdOffset;
    if (dfdLength >= 4 + 24) {
      const uint32_t word0 = u32(dfd + 4);
      const uint32_t word1 = u32(dfd + 8);
      const uint32_t vendor = word0 & 0x1FFFF;
      const uint32_t type = (word0 >> 17) & 0x7FFF;
      const uint32_t blockSize = word1 >> 16;
      if (vendor == 0 && type == 0 && blockSize >= 24 &&
          uint64_t(blockSize) + 4 <= dfdLength) {
        out.colourModel = dfd[4 + 8];
        const uint8_t transfer = dfd[4 + 10];
        out.transfer = transfer == 2   ? Transfer::srgb
                       : transfer == 1 ? Transfer::linear
                                       : Transfer::unspecified;
        out.premultiplied = (dfd[4 + 11] & 1) != 0;
      }
    }
  }

  if (out.vkFormat == 0) {
    if (out.scheme == Supercompression::basisLZ ||
        out.colourModel == kModelUastc || out.colourModel == kModelEtc1s) {
      out.basis = true;
      return {};
    }
    return "Its vkFormat is undefined and it is not Basis, so nothing says "
           "what its blocks are.";
  }
  if (out.scheme == Supercompression::basisLZ) {
    return "It says it is BasisLZ-supercompressed but names a GPU format; "
           "only ETC1S is squeezed that way.";
  }

  out.format = formatOf(out.vkFormat);
  if (out.format == nullptr) {
    return say("Its vkFormat, %" PRIu32 ", is not one this renderer reads.",
               out.vkFormat);
  }
  if (out.typeSize != out.format->typeSize) {
    return say("It says each %s value is %" PRIu32 " bytes, where it is %u.",
               out.format->name, out.typeSize,
               unsigned(out.format->typeSize));
  }
  if (out.transfer == Transfer::unspecified) {
    out.transfer = out.format->srgb ? Transfer::srgb : Transfer::linear;
  }

  uint64_t total = 0;
  for (uint32_t i = 0; i < out.levels; i++) {
    const uint64_t expected = levelBytes(out, i);
    const Level &level = out.index[i];
    total += expected;
    if (level.uncompressedLength != expected) {
      return say("Level %" PRIu32 " says it unpacks to %" PRIu64
                 " bytes, where a %" PRIu32 " by %" PRIu32 " %s level "
                 "holds %" PRIu64 ".",
                 i, level.uncompressedLength, levelWidth(out, i),
                 levelHeight(out, i), out.format->name, expected);
    }
    if (out.scheme == Supercompression::none && level.length != expected) {
      return say("Level %" PRIu32 " is %" PRIu64 " bytes, where a %" PRIu32
                 " by %" PRIu32 " %s level is %" PRIu64 ".",
                 i, level.length, levelWidth(out, i), levelHeight(out, i),
                 out.format->name, expected);
    }
  }
  if (total > kLargestDecompressed) {
    return say("It unpacks to %" PRIu64 " MB, more than any device this "
               "draws on could hold.",
               total >> 20);
  }
  return {};
}

std::string readLevel(const uint8_t *data, size_t size, const Header &header,
                      uint32_t level, uint8_t *out, size_t room) {
  if (header.basis || header.format == nullptr) {
    return "A Basis file's levels are transcoded, not read.";
  }
  if (level >= header.levels || level >= header.index.size()) {
    return say("There is no level %" PRIu32 ".", level);
  }
  const Level &entry = header.index[level];
  const uint64_t expected = levelBytes(header, level);
  if (out == nullptr || room != expected ||
      !inside(entry.offset, entry.length, size)) {
    return say("Level %" PRIu32 " was asked for into the wrong room.", level);
  }
  const uint8_t *from = data + entry.offset;

  if (header.scheme == Supercompression::none) {
    memcpy(out, from, size_t(expected));
    return {};
  }

  // One zstd frame per level. Its own header says how much it holds, and a
  // frame claiming anything but the level index's size is refused before a
  // byte is written — ZSTD_decompress would refuse to overrun `out` anyway,
  // but the reason is worth saying.
  const unsigned long long framed =
      ZSTD_getFrameContentSize(from, size_t(entry.length));
  if (framed == ZSTD_CONTENTSIZE_ERROR) {
    return say("Level %" PRIu32 " is not a zstd frame.", level);
  }
  if (framed != ZSTD_CONTENTSIZE_UNKNOWN && framed != expected) {
    return say("Level %" PRIu32 "'s zstd frame holds %llu bytes, where the "
               "level holds %" PRIu64 ".",
               level, framed, expected);
  }
  const size_t got =
      ZSTD_decompress(out, size_t(expected), from, size_t(entry.length));
  if (ZSTD_isError(got)) {
    return say("Level %" PRIu32 " could not be decompressed: %s.", level,
               ZSTD_getErrorName(got));
  }
  if (got != expected) {
    return say("Level %" PRIu32 " decompressed to %zu bytes, where it holds "
               "%" PRIu64 ".",
               level, got, expected);
  }
  return {};
}

uint32_t levelsToSkip(const Header &header, uint32_t maxSide) {
  if (maxSide == 0) return 0;
  uint32_t skip = 0;
  while (skip + 1 < header.levels &&
         std::max(levelWidth(header, skip), levelHeight(header, skip)) >
             maxSide) {
    skip++;
  }
  return skip;
}

std::string withoutLargestLevels(const uint8_t *data, size_t size,
                                 const Header &header, uint32_t skip,
                                 std::vector<uint8_t> &out) {
  out.clear();
  if (skip == 0 || skip >= header.levels ||
      header.index.size() != header.levels || data == nullptr ||
      size < kHeaderBytes) {
    return "There are not that many levels to leave out.";
  }
  const uint32_t kept = header.levels - skip;
  const uint32_t dfdOffset = u32(data + 48);
  const uint32_t dfdLength = u32(data + 52);
  const uint32_t kvdOffset = u32(data + 56);
  const uint32_t kvdLength = u32(data + 60);
  const uint64_t sgdOffset = u64(data + 64);
  const uint64_t sgdLength = u64(data + 72);

  // The supercompression data, less the image descriptors of the levels
  // left out. ETC1S keeps one per image, level by level from the largest,
  // and the slices they point at are measured from the start of their own
  // level — so the rest need no change.
  std::vector<uint8_t> sgd;
  if (sgdLength != 0) {
    const uint8_t *from = data + sgdOffset;
    if (header.scheme == Supercompression::basisLZ) {
      constexpr size_t kSgdHeader = 20;
      constexpr size_t kImageDesc = 20;
      const uint64_t perLevel = uint64_t(header.faces) * kImageDesc;
      const uint64_t described = perLevel * header.levels;
      if (sgdLength < kSgdHeader + described) {
        return "Its ETC1S data is shorter than its image descriptors.";
      }
      sgd.assign(from, from + kSgdHeader);
      sgd.insert(sgd.end(), from + kSgdHeader + perLevel * skip,
                 from + sgdLength);
    } else {
      sgd.assign(from, from + sgdLength);
    }
  }

  const auto align = [](uint64_t at, uint64_t to) {
    return (at + to - 1) / to * to;
  };
  uint64_t at = kHeaderBytes + uint64_t(kept) * kLevelIndexEntryBytes;
  const uint64_t newDfd = at;
  at += dfdLength;
  const uint64_t newKvd = kvdLength != 0 ? at : 0;
  at += kvdLength;
  uint64_t newSgd = 0;
  if (!sgd.empty()) {
    at = align(at, 8);
    newSgd = at;
    at += sgd.size();
  }
  // Levels unsqueezed have to start on a block boundary; squeezed ones may
  // start anywhere.
  const uint64_t levelAlign =
      header.scheme == Supercompression::none ? 16 : 1;
  std::vector<uint64_t> offsets(kept);
  for (uint32_t i = kept; i-- > 0;) {
    at = align(at, levelAlign);
    offsets[i] = at;
    at += header.index[i + skip].length;
  }
  if (at > kLargestDecompressed) return "It is too large to rewrite.";

  out.assign(size_t(at), 0);
  memcpy(out.data(), data, kHeaderBytes);
  put32(out.data() + 20, levelWidth(header, skip));
  put32(out.data() + 24, levelHeight(header, skip));
  put32(out.data() + 40, kept);
  put32(out.data() + 48, uint32_t(newDfd));
  put32(out.data() + 56, uint32_t(newKvd));
  put64(out.data() + 64, newSgd);
  put64(out.data() + 72, sgd.size());
  for (uint32_t i = 0; i < kept; i++) {
    const Level &level = header.index[i + skip];
    uint8_t *entry = out.data() + kHeaderBytes + i * kLevelIndexEntryBytes;
    put64(entry, offsets[i]);
    put64(entry + 8, level.length);
    put64(entry + 16, level.uncompressedLength);
    memcpy(out.data() + offsets[i], data + level.offset, size_t(level.length));
  }
  if (dfdLength != 0) {
    memcpy(out.data() + newDfd, data + dfdOffset, dfdLength);
  }
  if (kvdLength != 0) {
    memcpy(out.data() + newKvd, data + kvdOffset, kvdLength);
  }
  if (!sgd.empty()) memcpy(out.data() + newSgd, sgd.data(), sgd.size());
  return {};
}

bool namesCookedSet(const std::string &path) {
  if (!endsWithIgnoringCase(path, ".ktx2")) return false;
  const std::string stem = path.substr(0, path.size() - 5);
  return !endsWithIgnoringCase(stem, ".astc") &&
         !endsWithIgnoringCase(stem, ".bc") &&
         !endsWithIgnoringCase(stem, ".etc2");
}

std::string siblingName(const std::string &path, Family family) {
  const char *infix = family == Family::astc  ? ".astc"
                      : family == Family::bc  ? ".bc"
                      : family == Family::etc2 ? ".etc2"
                                               : "";
  if (path.size() < 5) return path;
  return path.substr(0, path.size() - 5) + infix +
         path.substr(path.size() - 5);
}

}  // namespace ktx2
}  // namespace orblit
