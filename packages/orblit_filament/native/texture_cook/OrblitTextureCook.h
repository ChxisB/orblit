// The offline texture cooker: a PNG, JPEG or Basis .ktx2 in, GPU-ready KTX2
// files out, one per family of GPU, with every mip level inside.
//
// What a cooked texture `x` is — the contract the renderer's loader reads:
//
//   x.ktx2       the fallback every device can use. Basis UASTC LDR 4x4,
//                or, for a lossless cook, plain R8G8B8A8 — and a lossless
//                texture has no siblings.
//   x.astc.ktx2  ASTC 4x4.
//   x.bc.ktx2    BC7, for colour and for a normal map alike; BC4 for one
//                channel, and BC5 for a normal map only when asked for.
//   x.etc2.ktx2  ETC2 RGBA8, or RGB8 when opaque, for colour and normal maps
//                alike; EAC R11 for one channel, and EAC RG11 for a normal
//                map only when asked for.
//
// A normal map keeps all three channels unless the cook is told otherwise,
// because nothing that draws with these files rebuilds Z: lit.mat samples
// the map's .xyz, and so do gltfio's materials. BC5 and EAC RG11 hold X and
// Y and sample 0 for blue, which bends every normal into the surface. They
// are behind Settings::twoChannelNormals for a material that rebuilds Z,
// which Phase 5's material system may add. BC4 and EAC R11 likewise sample 0
// for green and blue, so a single-channel cook is only for a texture every
// material reads red alone from.
//
// What each backend can sample, which decides the sibling a device takes:
// Filament's Metal backend has no sRGB ASTC (MetalEnums.mm maps only the
// linear RGBA_ASTC_* formats), so an Apple device never takes x.astc.ktx2
// for colour, and falls through to BC on a Mac and ETC2 on iOS. Metal also
// has no BC1 without alpha; nothing here writes BC1.
//
// Each is a standard KTX 2.0 file: its vkFormat set (0, as the specification
// requires, for UASTC), zstd applied to each level separately, a Basic Data
// Format Descriptor whose transfer function is sRGB for colour and linear
// otherwise, and the level index largest first. A loader asked for x.ktx2
// takes the first sibling the device can sample — astc, bc, etc2 — and x.ktx2
// itself when it can sample none of them.
//
// Why a sibling per family rather than one Basis file transcoded at load:
// the renderer used to transcode Basis on every load, 29 ms for one 2048
// square, and the old cook_textures.sh measured that adding mip levels to a
// Basis file made the Bistro load slower, not faster — every level is one
// more transcode. A GPU-ready level is a memcpy and an upload.
//
// The mip chain is made here rather than by the encoder, because the three
// things that make mips look right are all things a generic resizer gets
// wrong: colour is filtered as light (linearised, filtered, re-encoded), a
// normal map is renormalised at every level, and a cut-out's alpha is scaled
// per level so the fraction of texels that pass the alpha test stays what it
// was at the top — Castaño's coverage-preserving mipmaps. Without that last
// one, foliage thins out and then vanishes as it recedes, and what is left is
// the blocky leaves the old cook script recorded.
//
// Deterministic: the same input and settings give the same bytes on every
// run, at any thread count, at -O0 or -O2. Everything in the filter is
// IEEE arithmetic in a fixed order with contraction off (see build.sh); the
// sRGB curve is a table of constants; the kernel's sine and Bessel function
// are series summed here rather than libm calls; and threads only ever divide
// work whose pieces do not affect each other.
//
// Problems are returned, not thrown: a cook that cannot be done comes back
// with no files and a note saying why.
//
// Built by build.sh beside this file, into orblit_texture_cook and its check.
// None of this is linked into the renderer, and nothing here may be: every
// app compiles Sources/orblit_filament_native, and an encoder is megabytes
// that only a build machine needs.

#pragma once

#include <cstddef>
#include <cstdint>
#include <string>
#include <vector>

namespace orblit {
namespace texturecook {

/// The families of GPU a texture is cooked for, one file each.
enum Family : uint32_t {
  kFamilyBasis = 1u << 0,  // x.ktx2
  kFamilyAstc = 1u << 1,   // x.astc.ktx2
  kFamilyBc = 1u << 2,     // x.bc.ktx2
  kFamilyEtc2 = 1u << 3,   // x.etc2.ktx2
  kAllFamilies = kFamilyBasis | kFamilyAstc | kFamilyBc | kFamilyEtc2,
};

/// What the texels mean, which decides both the filter and the formats.
enum class Content {
  /// Colour, with or without alpha: BC7, ETC2 RGB(A)8.
  kColour,
  /// A tangent-space normal in RGB: linear, renormalised per level, and kept
  /// as three channels — BC7 and ETC2 RGB8 — unless twoChannelNormals.
  kNormal,
  /// One linear channel, taken from red: BC4 and EAC R11, which sample 0 for
  /// green and blue. Only for a texture every material reads red alone from:
  /// glTF's occlusion does, its metallicRoughness reads green and blue.
  /// tool/cook_textures.sh never asks for it.
  kSingleChannel,
};

enum class Transfer {
  /// sRGB or linear as the input says: a KTX2's Data Format Descriptor says;
  /// a PNG or JPEG does not, and is taken as sRGB colour, which is what
  /// an image editor writes.
  kInfer,
  kSrgb,
  kLinear,
};

enum class Edge {
  /// Texels past the edge repeat the edge texel.
  kClamp,
  /// Texels past the edge come from the opposite edge, for a tiling texture.
  kWrap,
};

/// Where the ASTC file's blocks come from.
///
/// Measured with orblit_texture_cook_check --measure, PSNR against the level
/// encoded, on seven PNG and JPEG textures of 1024 and 2048 and four of the
/// Bistro's Basis .ktx2 files:
///
///   from PNG or JPEG   Basis Universal's own ASTC encoder beat transcoding
///                      the UASTC encode at level 0 by 0.9 to 3.6 dB on
///                      colour, and by -0.9 to +3.1 dB on normal maps; half
///                      way down the chain by 0.9 to 3.1 dB, except one
///                      normal map where it lost 2.9. Its files were 3 to 24%
///                      larger after zstd, and it adds 2.5 to 9.5 s to a 2048
///                      texture on six threads, where the rest of the cook
///                      takes 0.8 to 5.8 s.
///   from a Basis .ktx2 mixed at level 0: transcoding won by 4.9 and 6.3 dB
///                      on two, lost by 0.2 and 4.2 dB on the other two;
///                      direct encoding won by 0.9 to 1.6 dB half way down.
///
/// So direct encoding where it clearly wins and the cost is paid once per
/// source image, and transcoding for Basis inputs, where it is no worse on
/// average and a four-hundred-texture scene cooks in a quarter of an hour
/// rather than well over an hour.
enum class AstcRoute {
  /// Direct from a PNG or JPEG, transcoded from a .ktx2.
  kAuto,
  /// Transcoded from the UASTC blocks every other family comes from.
  kTranscoded,
  /// Basis Universal's ASTC LDR encoder, at its default effort.
  kDirect,
};

/// The zstd level every file is compressed at unless told otherwise. On a
/// 2048 colour texture's four files, level 3 made 12.08 MB in 0.02 s, 9 made
/// 11.27 MB, 15 11.20 MB, 19 10.84 MB in 0.85 s and 22 10.84 MB in 0.95 s;
/// decompression costs the same at any level, and a cook is paid once.
constexpr int kDefaultZstdLevel = 19;

/// UASTC's own effort level, 0 (fastest) to 4 (slowest).
constexpr int kDefaultUastcLevel = 2;

struct Settings {
  uint32_t families = kAllFamilies;
  Content content = Content::kColour;
  Transfer transfer = Transfer::kInfer;
  /// The alpha-test threshold of a cut-out, in (0, 1); negative when the
  /// alpha is not tested. Only a cut-out's coverage is preserved.
  float cutout = -1.0f;
  /// Lossless R8G8B8A8 in x.ktx2 and nothing else: pixel art, UI.
  bool lossless = false;
  /// -1: mips unless lossless. 0: level 0 only. 1: a full chain.
  int mips = -1;
  /// Levels larger than this on either side are dropped; 0 keeps them all.
  uint32_t maxSize = 0;
  Edge edge = Edge::kClamp;
  AstcRoute astc = AstcRoute::kAuto;
  /// A normal map as BC5 and EAC RG11, X and Y alone. Only for a material
  /// that rebuilds Z from them; nothing in Orblit does yet.
  bool twoChannelNormals = false;
  int zstdLevel = kDefaultZstdLevel;
  int uastcLevel = kDefaultUastcLevel;
  /// 0: one per hardware thread.
  uint32_t threads = 0;
};

/// Eight bits per channel, RGBA, rows top first.
struct Image {
  uint32_t width = 0;
  uint32_t height = 0;
  std::vector<uint8_t> rgba;
};

/// What decoding the input found.
struct Source {
  /// "png", "jpeg" or "ktx2".
  std::string container;
  uint32_t width = 0;
  uint32_t height = 0;
  /// Whether any texel's alpha is below 255.
  bool hasAlpha = false;
  /// Whether the file itself says sRGB or linear, and which.
  bool transferKnown = false;
  bool srgb = true;
  /// A 16-bit PNG, which is read as 8 bits.
  bool sixteenBit = false;
};

/// A cooked file: the suffix after the texture's stem, and its bytes.
struct File {
  std::string suffix;
  uint32_t family = 0;
  uint32_t vkFormat = 0;
  /// A short name for the encoding: "UASTC", "BC7", "EAC RG11"...
  std::string format;
  std::vector<uint8_t> bytes;
};

/// What a cook did, for printing and for the checks.
struct Report {
  Source source;
  bool srgb = true;
  Content content = Content::kColour;
  bool lossless = false;
  /// Whether the ASTC file was encoded directly (see AstcRoute).
  bool directAstc = false;
  bool twoChannelNormals = false;
  float cutout = -1.0f;
  uint32_t width = 0;
  uint32_t height = 0;
  uint32_t levels = 0;
  uint32_t threads = 0;
  /// Per cooked level, when a cut-out: the fraction of texels at or above
  /// the threshold, with coverage preserved and as a plain filter would
  /// have left it.
  std::vector<double> coverage;
  std::vector<double> naiveCoverage;
  double targetCoverage = 0.0;
  /// Seconds spent in each stage.
  double decodeSeconds = 0.0;
  double mipSeconds = 0.0;
  double uastcSeconds = 0.0;
  double familySeconds = 0.0;
  double compressSeconds = 0.0;
  /// Things worth knowing that did not stop the cook.
  std::vector<std::string> warnings;
};

struct Cooked {
  std::vector<File> files;
  /// The levels the files were encoded from, largest first — what the check
  /// measures each file against.
  std::vector<Image> levels;
  Report report;
  /// Why there are no files, when there are none.
  std::string note;
};

/// Cooks the bytes of a PNG, JPEG or Basis .ktx2.
Cooked cook(const uint8_t *data, size_t size, const Settings &settings);

/// Decodes the bytes of a PNG, JPEG or Basis .ktx2 to RGBA8, refusing what is
/// malformed, too large or not a 2D LDR texture. Returns false with `why`.
bool decode(const uint8_t *data, size_t size, Image &image, Source &source,
            std::string &why);

/// The file suffix each family writes.
const char *suffixOf(Family family);

/// Parses "astc,bc,etc2,basis" into families; returns false on a name it does
/// not know.
bool parseFamilies(const std::string &text, uint32_t &families);

/// The 8-bit alpha a texel needs for alpha/255 to pass a test at `cutoff`,
/// as the GPU compares them (kept when alpha >= cutoff).
uint32_t alphaThreshold(float cutoff);

/// The fraction of an image's texels whose alpha passes a test at `cutoff`.
double coverageOf(const Image &image, float cutoff);

/// Which revision of the cooker's output a file of this family and content
/// is, written into its KTXwriter. A file's revision changes only when the
/// same input and settings would cook that file to different bytes, so a
/// resumable cook (tool/cook_textures.sh, Phase 6's cache) re-cooks what
/// changed and nothing else.
///
///   1  the first cooker
///   2  a normal map's BC and ETC2 files keep three channels, BC7 and ETC2
///      RGB8, where they were BC5 and EAC RG11
int revisionOf(Family family, Content content, bool twoChannelNormals);

/// The cooker's version: the highest revision any file it writes can have.
/// Bump it with revisionOf.
constexpr int kCookVersion = 2;

}  // namespace texturecook
}  // namespace orblit
