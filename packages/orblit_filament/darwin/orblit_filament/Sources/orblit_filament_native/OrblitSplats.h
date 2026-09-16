// Gaussian splats on the processor: reading them, and putting them in order.
//
// Plain C++ with nothing from Filament, Apple or any platform in it, because
// this is the half of splatting that every port needs unchanged: the file
// formats are the same everywhere, and so is the sort. What differs from one
// renderer backend to the next is only how the result reaches the GPU, and
// that is OrblitSplatSet's business.
//
// 3D Gaussian splatting is Kerbl, Kopanas, Leimkühler and Drettakis,
// SIGGRAPH 2023. The compact `.splat` layout is the one antimatter15's web
// viewer introduced and most tools now write.
#pragma once

#include <cstddef>
#include <cstdint>
#include <memory>
#include <string>
#include <vector>

namespace orblit {

/// Floats per splat set in the scene message: a column-major transform, an
/// opacity multiplier and a brightness multiplier. Must match
/// OrblitSplats.stride in Dart and splatStride in the plugin.
constexpr size_t kSplatParams = 18;

/// The bits of a cloud's flags in the scene message, low to high: whether it
/// is sorted; the spherical-harmonic degree, two bits; whether the sort is
/// coarse; and, from bit eight up, how many splats to keep at most, nought
/// for all of them. Must match OrblitSplats.flags in Dart.
constexpr int32_t kSplatFlagSorted = 1 << 0;
constexpr int kSplatFlagDegreeShift = 1;
constexpr int32_t kSplatFlagCoarse = 1 << 3;
constexpr int kSplatFlagLimitShift = 8;

/// Bytes per splat in the compact layout, which is also the layout an
/// in-memory cloud travels in: position, scale, colour, rotation.
constexpr size_t kSplatRecordBytes = 32;

/// Width of the textures the splats and their order live in. Must match
/// kWidth in splat.mat.
constexpr uint32_t kSplatTextureWidth = 2048;

/// Texels one splat takes in the splat texture: centre and the covariance's
/// first element; four more elements; the last element and the colour.
constexpr uint32_t kSplatTexelsPerSplat = 3;

/// The degree-zero spherical harmonic, which is what turns a trained
/// `f_dc` coefficient into a colour: 0.5 + SH_C0 * f_dc.
constexpr float kShC0 = 0.28209479177387814f;

/// The highest spherical-harmonic degree a capture is read at.
///
/// Three because there is no fourth to support: a trained capture carries 3,
/// 8 or 15 coefficients a colour channel and nothing else.
constexpr uint32_t kSplatMaxHarmonicDegree = 3;

/// Coefficients one colour channel has at each degree: none at all, then the
/// first band's three, the second band's five on top of those, the third's
/// seven on top again.
constexpr uint32_t kSplatHarmonicCoefficients[4] = {0, 3, 8, 15};

/// Texels one splat's harmonics take at `degree` — which is `degree` itself.
///
/// A texel of the harmonics texture is four unsigned integers and a
/// coefficient is quantised to a byte, so sixteen coefficients fit in one.
/// Nine bytes carry the first band for three channels, twenty-four carry two
/// bands and forty-five carry three: one, two and three texels exactly, with
/// seven, eight and three bytes spare. Paying those few bytes keeps a splat's
/// harmonics at a whole number of texels, so the shader fetches `degree` of
/// them at a known offset rather than working out which texel a coefficient
/// straddles.
constexpr uint32_t splatHarmonicTexels(uint32_t degree) { return degree; }

/// Bytes one splat's harmonics take before that padding.
constexpr uint32_t splatHarmonicBytes(uint32_t degree) {
  return kSplatHarmonicCoefficients[degree] * 3;
}

/// How far either side of a band's scale a stored byte reaches.
///
/// A coefficient becomes 128 + round(127 * coefficient / scale), so 128 is
/// nought exactly and 1 and 255 are the scale itself either way. Must match
/// the decode in splat.mat.
constexpr float kSplatHarmonicSteps = 127.0f;

/// Clouds of up to this many splats are sorted by the thread that asks.
///
/// Below this a sort takes a fraction of a millisecond, which is less than
/// handing the work to a thread or a worker and taking it back costs — and it
/// means a small cloud is in order on the very frame it asked to be.
constexpr uint32_t kSplatInlineSortLimit = 16384;

/// How far past the edge of the screen, in clip space, a splat's centre may
/// be and still be kept by a sort that culls.
///
/// splat.mat drops a centre past 1.3, so a splat that reaches onto the screen
/// from just off it still draws. Wider here, so that what a sort keeps is
/// always more than the shader would draw from the same camera, and so that a
/// camera turning while the next sort is on its way has some way to turn
/// before it sees past the last one. Must match GUARD in
/// native/web/orblit_splat_worker.js.
constexpr float kSplatCullGuard = 1.5f;

/// A cloud as the renderer holds it, already turned into what the shader
/// reads: a centre, the six numbers of a symmetric 3D covariance, and a
/// colour with its opacity.
struct SplatCloud {
  uint32_t count = 0;
  std::vector<float> positions;    // three each
  std::vector<float> covariances;  // six each: 00 01 02 11 12 22
  std::vector<uint32_t> colours;   // RGBA8, red in the low byte
  /// A box around every splat out to three standard deviations.
  float minimum[3] = {0, 0, 0};
  float maximum[3] = {0, 0, 0};

  /// The degree of the spherical harmonics in `harmonics`: 0 for a cloud that
  /// is the same colour from everywhere, up to kSplatMaxHarmonicDegree.
  uint32_t harmonicDegree = 0;
  /// The bands above the flat one, a byte a coefficient: per splat, in the
  /// order the coefficients come, with red, green and blue together within
  /// each. splatHarmonicBytes(degree) of them a splat, and empty at degree 0.
  ///
  /// Not in the order the file has them. A `.ply` writes every coefficient of
  /// red, then of green, then of blue, which is the worst order to read one
  /// splat's coefficients in; this is the order the shader wants them.
  std::vector<uint8_t> harmonics;
  /// What a byte of `harmonics` is worth, band by band from the first.
  /// Nought for a band that is not there.
  float harmonicScale[3] = {0, 0, 0};

  /// Whether the file had higher spherical-harmonic bands that were read
  /// past. Said so a caller can report it rather than pretend.
  bool droppedHigherBands = false;
};

/// Σ = R S Sᵀ Rᵀ, from per-axis scales (already exponentiated) and a unit
/// quaternion in (w, x, y, z) order — the order `rot_0..3` are stored in.
void splatCovariance(const float scale[3], const float rotation[4],
                     float out[6]);

/// Reads the compact 32-byte layout: position float3, scale float3 (linear,
/// not log), colour RGBA8 with opacity in alpha, rotation as four bytes
/// (w, x, y, z), each (byte - 128) / 128.
bool readSplatRecords(const uint8_t *data, size_t length, SplatCloud &into,
                      std::string &error);

/// Reads the layout the reference trainer writes: a binary little-endian PLY
/// whose vertex element has x y z, f_dc_0..2, optional f_rest_*, opacity as
/// a logit, scale_0..2 as log-scales and rot_0..3.
///
/// `maxDegree` is how much of the view-dependent colour to keep: 0 for the
/// flat degree-zero colour alone, up to kSplatMaxHarmonicDegree. A file
/// carrying more bands than that is read to the degree asked for and says so
/// in `droppedHigherBands`; one carrying fewer is read as far as it goes.
bool readSplatPly(const uint8_t *data, size_t length, uint32_t maxDegree,
                  SplatCloud &into, std::string &error);

/// Reads Niantic's `.spz`, versions 2 and 3: a gzip-compressed 16-byte header
/// and then each attribute in a block of its own — positions as 24-bit fixed
/// point, then alphas, colours, scales, rotations, and the bands above the
/// flat colour a byte a coefficient.
///
/// A `.spz` holds its capture right-up-back, where a `.ply` from the reference
/// trainer is right-down-front, so this turns it into the trainer's frame:
/// every file this renderer reads then needs the same transform to stand it
/// up. Version 1, and the version-4 stream format, are refused and say so.
bool readSplatSpz(const uint8_t *data, size_t length, uint32_t maxDegree,
                  SplatCloud &into, std::string &error);

/// Reads Orblit's own `.osplat`: the cloud exactly as the renderer holds it —
/// centres, covariances, colours and quantised harmonics — so that opening
/// one is a read and four copies, rather than a parse, an exponential, a
/// quaternion and a covariance a splat.
///
/// What a cook step writes and a launch that must not stall reads. Bytes as
/// this machine stores them, little-endian, like the `.ply` reader above:
/// this is a file made for the device that reads it.
bool readSplatCooked(const uint8_t *data, size_t length, uint32_t maxDegree,
                     SplatCloud &into, std::string &error);

/// That file, from a cloud read out of any of the others.
std::vector<uint8_t> writeSplatCooked(const SplatCloud &cloud);

/// Whichever of the readers above the file's extension names — `.ply`,
/// `.spz`, `.osplat`, or the compact records of anything else — from bytes
/// provided under `path` or else the file of that name.
///
/// `maxDegree` reaches every format that carries bands at all; a `.splat` has
/// no room for any, so one is flat whatever is asked for.
bool loadSplatFile(const std::string &path, uint32_t maxDegree,
                   SplatCloud &into, std::string &error);

/// Keeps the `limit` splats that add most to a picture, and drops the rest.
///
/// Ranked by opacity times how much of the screen a splat can cover, so a
/// limit takes the faint and the small first: what a capture loses is the
/// dust it is thickest with, not its walls. The splats kept stay in the order
/// they came, and the box is measured again round them. Nought, or a limit of
/// at least the cloud's size, keeps everything and changes nothing.
///
/// Answers how many splats there were, so a caller can say what it dropped.
uint32_t keepMostVisibleSplats(SplatCloud &cloud, uint32_t limit);

/// The cloud as the RGBA32UI texels the shader fetches, padded out to whole
/// rows of kSplatTextureWidth.
void packSplatTexels(const SplatCloud &cloud, std::vector<uint32_t> &texels);

/// The cloud's harmonics as their own RGBA32UI texels, sixteen quantised
/// coefficients to a texel and splatHarmonicTexels(degree) texels a splat,
/// padded out to whole rows. Empty for a cloud at degree 0, which is what
/// keeps a capture without them costing nothing at all.
void packSplatHarmonicTexels(const SplatCloud &cloud,
                             std::vector<uint32_t> &texels);

/// A float as an unsigned integer that sorts the same way.
inline uint32_t sortableBits(float value) {
  uint32_t bits;
  static_assert(sizeof(bits) == sizeof(value), "a float is four bytes");
  __builtin_memcpy(&bits, &value, sizeof(bits));
  // Negative floats sort backwards and below the positive ones, so their
  // bits are all flipped; positive ones only need the sign bit set.
  return (bits & 0x80000000u) ? ~bits : (bits | 0x80000000u);
}

/// What one sort is asked for: which way the camera faces and, to leave out
/// what it cannot see, where it is.
struct SplatSortRequest {
  /// The camera's forward vector in the cloud's own space, unit length. The
  /// order is by distance along it, farthest first.
  float direction[3] = {0, 0, -1};

  /// Whether to leave out the splats the camera cannot see: a centre not in
  /// front of it, or past kSplatCullGuard of the screen either way. The order
  /// then holds only the splats kept.
  bool cull = false;

  /// Model to view and model to clip, column-major. Read only when culling.
  float viewFromModel[16] = {1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1};
  float clipFromModel[16] = {1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1};

  /// Sixteen bits of depth rather than thirty-two: two radix passes rather
  /// than four, and splats within a 65 536th of the kept splats' depth of one
  /// another in either order. For a device where the sort is what a frame
  /// waits on.
  bool coarse = false;
};

/// Puts the splats in order, farthest first, leaving out what `request`
/// culls.
///
/// An LSD radix sort. With a full key — the float depth made sortable, then
/// inverted so that ascending order is back to front — in four passes of
/// eight bits; with a coarse one, the depth measured across the kept splats'
/// own range in 65 536 steps, in two. A pass is skipped when every key agrees
/// on its byte. Stable, so culling only takes splats out: the ones kept come
/// in the order a sort of all of them would have put them in, and the shader
/// drops every splat culled here anyway, so from the camera it was sorted for
/// the picture is the same one from fewer quads.
///
/// `scratch` is kept by the caller so a million-splat sort does not allocate
/// each time.
void sortSplats(const float *positions, uint32_t count,
                const SplatSortRequest &request, std::vector<uint32_t> &order,
                std::vector<uint32_t> &scratch);

/// Sorts a cloud without the render thread waiting for it.
///
/// The render thread asks, carries on drawing with the order it already has,
/// and picks the answer up on whichever frame it is ready. At a million
/// splats a sort is tens of milliseconds, and a frame that waited for it
/// would be the hitch every camera turn produced.
///
/// Where the sort runs is makeSplatSorter's choice: on a thread of its own
/// natively, on a Web Worker in a browser without threads, and on the thread
/// that asks for a cloud too small for either to be worth it.
class SplatSorter {
 public:
  virtual ~SplatSorter() = default;

  /// Asks for a sort. Ask only when not busy: a request made while another is
  /// in flight may replace it or wait behind it, depending on where it runs.
  virtual void request(const SplatSortRequest &request) = 0;

  /// Whether a request is waiting or being worked on.
  virtual bool busy() = 0;

  /// Hands over the newest finished order, if there is one since last time.
  virtual bool take(std::vector<uint32_t> &order, double &milliseconds) = 0;
};

/// The sorter for a cloud of `count` splats at `positions`, which are shared
/// and never written again — which is what lets a sort read them without a
/// lock.
std::unique_ptr<SplatSorter> makeSplatSorter(
    std::shared_ptr<const std::vector<float>> positions, uint32_t count);

#if defined(__EMSCRIPTEN__) && !defined(__EMSCRIPTEN_PTHREADS__)
/// A sorter on a Web Worker, or null where the page will not start one.
/// Defined beside the web build, in native/web/OrblitSplatSorterWeb.cpp.
std::unique_ptr<SplatSorter> makeWorkerSplatSorter(
    std::shared_ptr<const std::vector<float>> positions, uint32_t count);
#endif

}  // namespace orblit
