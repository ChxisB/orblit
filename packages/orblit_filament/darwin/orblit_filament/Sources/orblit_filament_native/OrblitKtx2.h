#pragma once

// KTX 2.0 files, read from bytes and nothing else.
//
// A cooked texture arrives as blocks the GPU samples directly — ASTC, BC or
// ETC2 — squeezed per level with zstd, so a load is a decompression and an
// upload rather than a transcode. Filament's own KTX 2 reader only reads
// Basis, which is transcoded on every load (29 ms for one 2048² texture), so
// this reads the rest.
//
// Deliberately a pure function of bytes: no Filament type, no renderer, no
// file system. What it answers — is this a KTX 2 file, what does it hold,
// where is each level and what is it once decompressed — is the same
// question a browser's worker will ask before handing the levels to the page,
// and a separate small wasm can take this file and zstd and nothing more.
// Which Filament format a vkFormat becomes is OrblitTextures.cpp's business.
//
// Every refusal is a sentence, because it ends up in a scene note in front of
// somebody who has to decide what to recook.

#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

namespace orblit {
namespace ktx2 {

/// How a level's bytes are squeezed, as the header numbers it.
enum class Supercompression : uint32_t {
  none = 0,
  basisLZ = 1,
  zstd = 2,
  zlib = 3,
};

/// Which cooked sibling a format belongs in: `x.astc.ktx2`, `x.bc.ktx2` or
/// `x.etc2.ktx2`. Uncompressed formats belong in none of them.
enum class Family : uint8_t { uncompressed, astc, bc, etc2 };

/// What the colour in a file is encoded as, from its data format descriptor.
enum class Transfer : uint8_t { unspecified = 0, linear = 1, srgb = 2 };

/// A vkFormat this reads, and what its bytes are.
struct Format {
  uint32_t vkFormat;
  /// Vulkan's name for it, without VK_FORMAT_, for notes.
  const char *name;
  /// Texels a block covers. One by one for an uncompressed format.
  uint8_t blockWidth;
  uint8_t blockHeight;
  /// Bytes a block — or, uncompressed, a texel — takes.
  uint8_t bytesPerBlock;
  /// What the header's typeSize has to say for it: the size of one channel
  /// of an uncompressed format, one for a block format.
  uint8_t typeSize;
  bool srgb;
  Family family;
};

/// The format a vkFormat names, or null for one this does not read — which
/// includes VK_FORMAT_UNDEFINED, the vkFormat a Basis file carries.
const Format *formatOf(uint32_t vkFormat);

/// The same blocks read the other way: BC7_SRGB for BC7_UNORM and back.
///
/// The bytes of an sRGB block format and its linear twin are identical — the
/// transfer function is only how the sampler turns them into numbers — so a
/// file cooked as one can be sampled as the other at no cost. Null where
/// there is no twin: BC4, BC5, BC6H, EAC and the float formats have no sRGB
/// form at all. `format` itself when it is already the one asked for.
const Format *withTransfer(const Format &format, bool srgb);

/// Every format formatOf reads, in no particular order.
const Format *allFormats(size_t *count);

/// One entry of the level index.
struct Level {
  uint64_t offset;
  uint64_t length;
  uint64_t uncompressedLength;
};

/// A file's header, level index and descriptor, checked.
struct Header {
  uint32_t vkFormat = 0;
  uint32_t typeSize = 0;
  uint32_t width = 0;
  uint32_t height = 0;
  /// Six for a cubemap, one otherwise.
  uint32_t faces = 1;
  /// Levels stored, never nought: a header's nought, "make the mipmaps
  /// yourself", stores one.
  uint32_t levels = 1;
  /// Whether the header asked for mipmaps to be made at run time.
  bool generateMipmaps = false;
  Supercompression scheme = Supercompression::none;
  /// The descriptor's colour model: 166 is UASTC, 163 ETC1S.
  uint8_t colourModel = 0;
  Transfer transfer = Transfer::unspecified;
  bool premultiplied = false;
  /// ETC1S or UASTC: transcoded, not uploaded as it is. `format` is null.
  bool basis = false;
  const Format *format = nullptr;
  /// Largest level first, as the file lists them.
  std::vector<Level> index;
};

/// The twelve bytes every KTX 2 file begins with.
bool isKtx2(const uint8_t *data, size_t size);

/// Reads and checks a file's header, level index and data format descriptor.
///
/// Checks that every level lies inside the file and, for anything but Basis,
/// that each is exactly as large as its format and size say once
/// decompressed — which is also what stops a small file claiming to unpack
/// into gigabytes. Empty on success; otherwise why not, as a sentence.
std::string read(const uint8_t *data, size_t size, Header &out);

/// Bytes one level holds decompressed, every face of it. Nought for Basis.
uint64_t levelBytes(const Header &header, uint32_t level);

/// Width and height of a level: the base halved `level` times, never below
/// one.
uint32_t levelWidth(const Header &header, uint32_t level);
uint32_t levelHeight(const Header &header, uint32_t level);

/// Level `level` decompressed into `out`, which must hold exactly
/// levelBytes(header, level). `header` must be what read() made of these same
/// bytes. Empty on success.
///
/// Thread-safe and allocation-free apart from zstd's own context, so any
/// number of levels of any number of files can be read at once.
std::string readLevel(const uint8_t *data, size_t size, const Header &header,
                      uint32_t level, uint8_t *out, size_t room);

/// How many of the largest levels to leave out so the largest one kept is
/// no wider or taller than `maxSide`. Nought when `maxSide` is nought, when
/// the file already fits, or when it has no smaller level to fall back on;
/// never so many that no level is left.
uint32_t levelsToSkip(const Header &header, uint32_t maxSide);

/// The same file without its `skip` largest levels, as a whole KTX 2 file.
///
/// For Basis, whose transcoder reads a file rather than levels: the level
/// index, the size in the header and — for ETC1S — the per-image descriptors
/// in the supercompression data all start at the new largest level, and the
/// level data is copied across unchanged. Empty on success.
std::string withoutLargestLevels(const uint8_t *data, size_t size,
                                 const Header &header, uint32_t skip,
                                 std::vector<uint8_t> &out);

/// Whether a path names a cooked set — `x.ktx2` — rather than one member of
/// one: `x.astc.ktx2`, `x.bc.ktx2` and `x.etc2.ktx2` are taken as they are.
bool namesCookedSet(const std::string &path);

/// A sibling's name: `x.ktx2` and astc make `x.astc.ktx2`. `path` must name
/// a cooked set.
std::string siblingName(const std::string &path, Family family);

}  // namespace ktx2
}  // namespace orblit
