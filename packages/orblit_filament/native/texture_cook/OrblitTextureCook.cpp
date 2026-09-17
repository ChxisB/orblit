// The texture cooker. See OrblitTextureCook.h for what it makes and why.
//
// Four stages, in order:
//
//   decode      PNG and JPEG through stb_image, Basis .ktx2 through Basis
//               Universal's transcoder, both to RGBA8, with sizes checked
//               before anything large is allocated.
//   mip chain   a Kaiser-windowed sinc, applied to light rather than to
//               sRGB codes, weighted by alpha, renormalised for normals, and
//               with a cut-out's coverage held to level 0's.
//   UASTC       every level encoded once, block by block.
//   families    each UASTC block transcoded to ASTC, BC7 or BC4 (BC5 on
//               request), ETC2 or EAC, and every file written as KTX2 with
//               each level compressed by zstd on its own.
//
// Why encode once and transcode, rather than run a separate encoder per
// family: UASTC was designed as the common ancestor of those formats, and
// the transcoder turns a block into each of them in microseconds with little
// loss — while running a BC7, an ASTC and an ETC2 encoder over every level
// is three encodes where there was one. What that costs in quality is
// measured, not assumed: orblit_texture_cook_check --measure prints the PSNR
// of every family at every level against the level it was encoded from, and
// the same for Basis Universal's direct ASTC encoder, which is what an ASTC
// file from a PNG or JPEG comes from (see AstcRoute).

#include "OrblitTextureCook.h"

#include "encoder/basisu_astc_ldr_encode.h"
#include "encoder/basisu_enc.h"
#include "encoder/basisu_uastc_enc.h"
#include "transcoder/basisu_transcoder.h"
#include "transcoder/basisu_transcoder_uastc.h"
#include "zstd/zstd.h"

#include "stb_image.h"

#include <algorithm>
#include <atomic>
#include <chrono>
#include <climits>
#include <cmath>
#include <cstring>
#include <exception>
#include <mutex>
#include <thread>
#include <utility>

#include "OrblitSrgbTables.inc"

namespace orblit {
namespace texturecook {
namespace {

// The largest texture read, on either side and in total. The total is what
// the float working copy of one level costs — seven planes of four bytes a
// texel for an alpha-weighted colour texture, so 1.9 GB at this limit — and
// the side is what every GPU the renderer targets can sample.
constexpr uint32_t kMaxSide = 16384;
constexpr uint64_t kMaxTexels = uint64_t(8192) * 8192;

constexpr uint8_t kKtx2Identifier[12] = {0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32,
                                         0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A};

using Clock = std::chrono::steady_clock;

double secondsSince(Clock::time_point from) {
  return std::chrono::duration<double>(Clock::now() - from).count();
}

std::once_flag initialised;

void initialise() {
  std::call_once(initialised, [] {
    // Builds the encoder's and transcoder's tables. Once, and before any
    // thread touches them.
    basisu::basisu_encoder_init(false, false);
  });
}

/// Runs work(begin, end) over [0, count) split into contiguous pieces, one per
/// thread. Only ever given work whose pieces write disjoint outputs from
/// shared inputs, so the result cannot depend on how many pieces there are.
template <typename Work>
void parallel(uint32_t count, uint32_t threads, Work &&work) {
  threads = std::max<uint32_t>(1, std::min(threads, count));
  if (threads == 1) {
    work(uint32_t(0), count);
    return;
  }
  const uint32_t piece = (count + threads - 1) / threads;
  std::vector<std::thread> pool;
  pool.reserve(threads);
  for (uint32_t begin = 0; begin < count; begin += piece) {
    const uint32_t end = std::min(count, begin + piece);
    pool.emplace_back([&work, begin, end] { work(begin, end); });
  }
  for (std::thread &thread : pool) thread.join();
}

// ---------------------------------------------------------------------------
// Decoding

bool sizeAllowed(uint64_t width, uint64_t height, std::string &why) {
  if (width == 0 || height == 0) {
    why = "the image has no texels";
    return false;
  }
  if (width > kMaxSide || height > kMaxSide || width * height > kMaxTexels) {
    why = "the image is " + std::to_string(width) + "x" + std::to_string(height) +
          "; the most this cooks is " + std::to_string(kMaxSide) +
          " on a side and 8192x8192 in total";
    return false;
  }
  return true;
}

bool anyAlpha(const Image &image) {
  for (size_t i = 3; i < image.rgba.size(); i += 4) {
    if (image.rgba[i] != 255) return true;
  }
  return false;
}

bool decodeKtx2(const uint8_t *data, size_t size, Image &image, Source &source,
                std::string &why) {
  source.container = "ktx2";
  if (size > UINT32_MAX) {
    why = "the KTX2 file is larger than 4 GB";
    return false;
  }
  basist::ktx2_transcoder transcoder;
  if (!transcoder.init(data, uint32_t(size))) {
    why = "not a KTX2 file Basis Universal can read (only Basis Universal "
          "KTX2 is cooked from; a GPU-format file is already cooked)";
    return false;
  }
  if (transcoder.get_header().m_vk_format != 0) {
    why = "this KTX2 is already in a GPU format (vkFormat " +
          std::to_string(transcoder.get_header().m_vk_format) +
          "); cook from its source image or its Basis file instead";
    return false;
  }
  if (transcoder.get_layers() > 1 || transcoder.get_faces() != 1 ||
      transcoder.get_header().m_pixel_depth > 1) {
    why = "only 2D textures are cooked, and this is an array, cubemap or "
          "volume";
    return false;
  }
  if (!transcoder.is_ldr()) {
    why = "this KTX2 holds HDR data, which the texture cooker does not take";
    return false;
  }
  if (!sizeAllowed(transcoder.get_width(), transcoder.get_height(), why)) {
    return false;
  }
  if (!transcoder.start_transcoding()) {
    why = "the KTX2's Basis data is damaged";
    return false;
  }
  const uint32_t width = transcoder.get_width();
  const uint32_t height = transcoder.get_height();
  image.width = width;
  image.height = height;
  image.rgba.assign(size_t(width) * height * 4, 0);
  if (!transcoder.transcode_image_level(0, 0, 0, image.rgba.data(),
                                        width * height,
                                        basist::transcoder_texture_format::cTFRGBA32)) {
    why = "the KTX2's top level could not be decoded";
    return false;
  }
  source.width = width;
  source.height = height;
  source.transferKnown = true;
  source.srgb = transcoder.is_srgb();
  return true;
}

}  // namespace

bool decode(const uint8_t *data, size_t size, Image &image, Source &source,
            std::string &why) {
  initialise();
  image = Image();
  source = Source();
  if (data == nullptr || size < 8) {
    why = "too short to be an image";
    return false;
  }
  if (size >= sizeof(kKtx2Identifier) &&
      std::memcmp(data, kKtx2Identifier, sizeof(kKtx2Identifier)) == 0) {
    if (!decodeKtx2(data, size, image, source, why)) return false;
    source.hasAlpha = anyAlpha(image);
    return true;
  }

  // Sniffed from the bytes rather than trusted from a file name.
  static constexpr uint8_t png[8] = {0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A};
  if (std::memcmp(data, png, 8) == 0) {
    source.container = "png";
  } else if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) {
    source.container = "jpeg";
  } else {
    why = "not a PNG, JPEG or KTX2 file";
    return false;
  }
  if (size > size_t(INT_MAX)) {
    why = "the file is larger than 2 GB";
    return false;
  }

  // The header first, so a file that claims to be 60000 pixels wide is
  // refused before a byte of it is inflated.
  int width = 0, height = 0, channels = 0;
  if (!stbi_info_from_memory(data, int(size), &width, &height, &channels)) {
    why = "unreadable " + source.container + ": " + stbi_failure_reason();
    return false;
  }
  if (!sizeAllowed(uint64_t(std::max(width, 0)), uint64_t(std::max(height, 0)), why)) {
    return false;
  }
  source.sixteenBit = stbi_is_16_bit_from_memory(data, int(size)) != 0;

  int decodedWidth = 0, decodedHeight = 0, decodedChannels = 0;
  stbi_uc *pixels = stbi_load_from_memory(data, int(size), &decodedWidth,
                                          &decodedHeight, &decodedChannels, 4);
  if (pixels == nullptr) {
    why = "unreadable " + source.container + ": " + stbi_failure_reason();
    return false;
  }
  if (decodedWidth != width || decodedHeight != height) {
    stbi_image_free(pixels);
    why = "the " + source.container + "'s header and its image disagree about its size";
    return false;
  }
  image.width = uint32_t(width);
  image.height = uint32_t(height);
  image.rgba.assign(pixels, pixels + size_t(width) * size_t(height) * 4);
  stbi_image_free(pixels);
  source.width = image.width;
  source.height = image.height;
  source.hasAlpha = anyAlpha(image);
  return true;
}

namespace {

// ---------------------------------------------------------------------------
// The kernel
//
// A Kaiser-windowed sinc three destination texels wide on each side, alpha 4:
// the filter NVIDIA Texture Tools offers for mipmaps. A box filter — the
// average of four — is what makes a halved checkerboard grey mush in one
// place and aliasing moiré in another; a windowed sinc keeps what the
// smaller level can hold and removes what it cannot.
//
// sin and the Bessel function are summed here as series of + and × rather
// than taken from libm, whose rounding differs between platforms: the
// weights, and so every texel after them, are the same bytes on macOS and
// Linux, at any optimisation level.

constexpr double kPi = 3.14159265358979323846264338327950288;
constexpr double kKernelRadius = 3.0;
constexpr double kKaiserAlpha = 4.0;

/// sin(pi * x), by Taylor series on the nearest half-period.
double sinPi(double x) {
  const double whole = std::floor(x + 0.5);
  const double y = kPi * (x - whole);  // |y| <= pi/2
  const double y2 = y * y;
  double term = y;
  double sum = y;
  for (int k = 1; k <= 12; k++) {
    term = -term * y2 / double((2 * k) * (2 * k + 1));
    sum += term;
  }
  // sin(pi(n + f)) = (-1)^n sin(pi f)
  const bool odd = std::fmod(whole, 2.0) != 0.0;
  return odd ? -sum : sum;
}

/// The zeroth-order modified Bessel function of the first kind, by series.
double besselI0(double x) {
  const double quarter = x * x / 4.0;
  double term = 1.0;
  double sum = 1.0;
  for (int k = 1; k <= 40; k++) {
    term = term * quarter / double(k * k);
    sum += term;
  }
  return sum;
}

double kaiser(double x) {
  if (x <= -kKernelRadius || x >= kKernelRadius) return 0.0;
  const double sinc = x == 0.0 ? 1.0 : sinPi(x) / (kPi * x);
  const double t = x / kKernelRadius;
  return sinc * besselI0(kKaiserAlpha * std::sqrt(1.0 - t * t)) /
         besselI0(kKaiserAlpha);
}

/// For each destination texel along one axis, which source texels it reads
/// and how much of each: `count` taps per destination, zero-weighted where a
/// destination has fewer.
struct Taps {
  uint32_t count = 0;
  std::vector<uint32_t> index;
  std::vector<float> weight;
};

Taps tapsFor(uint32_t from, uint32_t to, Edge edge) {
  Taps taps;
  if (from == to) {
    taps.count = 1;
    taps.index.resize(to);
    taps.weight.assign(to, 1.0f);
    for (uint32_t i = 0; i < to; i++) taps.index[i] = i;
    return taps;
  }
  const double scale = double(from) / double(to);
  const double reach = kKernelRadius * scale;
  taps.count = uint32_t(std::ceil(reach)) * 2 + 2;
  taps.index.assign(size_t(to) * taps.count, 0);
  taps.weight.assign(size_t(to) * taps.count, 0.0f);
  std::vector<double> weights(taps.count);
  std::vector<int64_t> sources(taps.count);
  for (uint32_t d = 0; d < to; d++) {
    const double centre = (double(d) + 0.5) * scale - 0.5;
    const int64_t first = int64_t(std::floor(centre - reach));
    uint32_t n = 0;
    double sum = 0.0;
    for (int64_t s = first; n < taps.count; s++, n++) {
      const double w = kaiser((double(s) - centre) / scale);
      weights[n] = w;
      sources[n] = s;
      sum += w;
    }
    for (uint32_t k = 0; k < taps.count; k++) {
      int64_t s = sources[k];
      if (edge == Edge::kWrap) {
        s %= int64_t(from);
        if (s < 0) s += from;
      } else {
        s = std::min<int64_t>(std::max<int64_t>(s, 0), int64_t(from) - 1);
      }
      taps.index[size_t(d) * taps.count + k] = uint32_t(s);
      taps.weight[size_t(d) * taps.count + k] = float(weights[k] / sum);
    }
  }
  return taps;
}

/// A level as floats, one plane per channel.
struct Planes {
  uint32_t width = 0;
  uint32_t height = 0;
  uint32_t channels = 0;
  std::vector<float> data;

  float *plane(uint32_t c) { return data.data() + size_t(c) * width * height; }
  const float *plane(uint32_t c) const {
    return data.data() + size_t(c) * width * height;
  }
};

Planes resample(const Planes &in, uint32_t width, uint32_t height, Edge edge,
                uint32_t threads) {
  const Taps across = tapsFor(in.width, width, edge);
  const Taps down = tapsFor(in.height, height, edge);

  // Across first, into a level as wide as the result and as tall as the
  // source; then down.
  Planes wide;
  wide.width = width;
  wide.height = in.height;
  wide.channels = in.channels;
  wide.data.assign(size_t(width) * in.height * in.channels, 0.0f);
  parallel(in.height, threads, [&](uint32_t begin, uint32_t end) {
    for (uint32_t c = 0; c < in.channels; c++) {
      const float *source = in.plane(c);
      float *target = wide.data.data() + size_t(c) * width * in.height;
      for (uint32_t y = begin; y < end; y++) {
        const float *row = source + size_t(y) * in.width;
        for (uint32_t x = 0; x < width; x++) {
          const uint32_t *index = &across.index[size_t(x) * across.count];
          const float *weight = &across.weight[size_t(x) * across.count];
          float sum = 0.0f;
          for (uint32_t k = 0; k < across.count; k++) {
            sum += weight[k] * row[index[k]];
          }
          target[size_t(y) * width + x] = sum;
        }
      }
    }
  });

  Planes out;
  out.width = width;
  out.height = height;
  out.channels = in.channels;
  out.data.assign(size_t(width) * height * in.channels, 0.0f);
  parallel(height, threads, [&](uint32_t begin, uint32_t end) {
    for (uint32_t c = 0; c < in.channels; c++) {
      const float *source = wide.plane(c);
      float *target = out.plane(c);
      for (uint32_t y = begin; y < end; y++) {
        const uint32_t *index = &down.index[size_t(y) * down.count];
        const float *weight = &down.weight[size_t(y) * down.count];
        for (uint32_t x = 0; x < width; x++) {
          float sum = 0.0f;
          for (uint32_t k = 0; k < down.count; k++) {
            sum += weight[k] * source[size_t(index[k]) * width + x];
          }
          target[size_t(y) * width + x] = sum;
        }
      }
    }
  });
  return out;
}

// ---------------------------------------------------------------------------
// Between bytes and light

inline float clamp01(float v) { return v < 0.0f ? 0.0f : (v > 1.0f ? 1.0f : v); }

inline uint8_t quantise(float v) {
  return uint8_t(std::floor(clamp01(v) * 255.0f + 0.5f));
}

/// The sRGB code of a linear value: how many of the half-code thresholds it
/// has reached.
inline uint8_t encodeSrgb(float linear) {
  const double v = double(linear);
  const double *end = kSrgbThresholds + 255;
  return uint8_t(std::upper_bound(kSrgbThresholds, end, v) - kSrgbThresholds);
}

inline float decodeSrgb(uint8_t code) { return float(kSrgbToLinear[code]); }

/// How a level becomes floats and back.
struct Plan {
  Content content = Content::kColour;
  bool srgb = true;
  /// Colour filtered weighted by alpha, so the colour of texels nobody can
  /// see does not bleed into the ones they border.
  bool alphaWeighted = false;
  float cutout = -1.0f;
  Edge edge = Edge::kClamp;
};

// Below this alpha a filtered texel's colour is taken from the unweighted
// filter instead: there is too little of it left to divide by.
constexpr float kUnweightFloor = 1.0f / 4096.0f;

Planes toPlanes(const Image &image, const Plan &plan) {
  Planes planes;
  planes.width = image.width;
  planes.height = image.height;
  const size_t texels = size_t(image.width) * image.height;
  switch (plan.content) {
    case Content::kColour:
      planes.channels = plan.alphaWeighted ? 7 : 4;
      break;
    case Content::kNormal:
      planes.channels = 4;
      break;
    case Content::kSingleChannel:
      planes.channels = 1;
      break;
  }
  planes.data.assign(texels * planes.channels, 0.0f);
  const uint8_t *px = image.rgba.data();
  for (size_t i = 0; i < texels; i++) {
    const uint8_t *t = px + i * 4;
    switch (plan.content) {
      case Content::kColour: {
        const float a = float(t[3]) / 255.0f;
        for (uint32_t c = 0; c < 3; c++) {
          const float v = plan.srgb ? decodeSrgb(t[c]) : float(t[c]) / 255.0f;
          if (plan.alphaWeighted) {
            planes.data[texels * c + i] = v * a;
            planes.data[texels * (4 + c) + i] = v;
          } else {
            planes.data[texels * c + i] = v;
          }
        }
        planes.data[texels * 3 + i] = a;
        break;
      }
      case Content::kNormal:
        for (uint32_t c = 0; c < 3; c++) {
          planes.data[texels * c + i] = float(t[c]) / 127.5f - 1.0f;
        }
        planes.data[texels * 3 + i] = float(t[3]) / 255.0f;
        break;
      case Content::kSingleChannel:
        planes.data[i] = float(t[0]) / 255.0f;
        break;
    }
  }
  return planes;
}

/// The scale for a level's alpha that makes as near as possible the same
/// fraction of its texels pass the alpha test as passed at level 0.
///
/// Castaño ("Computing alpha mipmaps", 2010) finds it by bisection; this
/// finds it directly. A texel passes when alpha * scale reaches the
/// threshold, so choosing the scale is choosing a cut in the level's alpha
/// values: everything at or above the cut passes. The `keep` texels with the
/// most alpha are the ones that should, so the cut goes just below the
/// keep-th largest alpha — or just above it, when that value is shared by a
/// crowd of texels (the solid middle of every leaf is exactly 1.0) and
/// leaving the whole crowd out lands nearer the target than letting it in.
/// Either way the cut sits halfway to the neighbouring value, as far from
/// both as it can be.
double coverageScale(const std::vector<float> &alpha, double target,
                     uint32_t threshold8) {
  const size_t count = alpha.size();
  const double threshold = (double(threshold8) - 0.5) / 255.0;
  const size_t keep = size_t(std::floor(target * double(count) + 0.5));

  double smallestPositive = 0.0;
  double largest = 0.0;
  for (float a : alpha) {
    if (a > 0.0f && (smallestPositive == 0.0 || a < smallestPositive)) {
      smallestPositive = a;
    }
    largest = std::max(largest, double(a));
  }
  if (largest <= 0.0) return 1.0;  // Nothing to scale.
  // Every texel with any alpha at all passes; a zero never can.
  const double everything = threshold / (smallestPositive * 0.5);
  if (keep == 0) return std::min(1.0, 0.5 * threshold / largest);
  if (keep >= count) return everything;

  std::vector<float> sorted(alpha);
  std::nth_element(sorted.begin(), sorted.begin() + (keep - 1), sorted.end(),
                   std::greater<float>());
  const float value = sorted[keep - 1];
  if (value <= 0.0f) return everything;

  size_t above = 0, atOrAbove = 0;
  float nextBelow = 0.0f;       // the largest alpha under `value`
  float nextAbove = 2.0f;       // the smallest alpha over it
  for (float a : alpha) {
    if (a > value) {
      above++;
      nextAbove = std::min(nextAbove, a);
    } else if (a < value) {
      nextBelow = std::max(nextBelow, a);
    }
    if (a >= value) atOrAbove++;
  }
  const size_t missIn = atOrAbove - keep;  // atOrAbove >= keep by construction
  const size_t missOut = keep - above;
  if (missIn <= missOut) {
    return threshold / ((double(value) + double(nextBelow)) * 0.5);
  }
  return threshold / ((double(value) + double(nextAbove)) * 0.5);
}

uint8_t scaledAlpha(float alpha, double scale) {
  const double v = std::min(1.0, double(alpha) * scale);
  return uint8_t(std::floor(v * 255.0 + 0.5));
}

struct LevelOut {
  Image image;
  double coverage = -1.0;
  double naiveCoverage = -1.0;
};

LevelOut fromPlanes(const Planes &planes, const Plan &plan, double targetCoverage,
                    uint32_t threads) {
  LevelOut out;
  Image &image = out.image;
  image.width = planes.width;
  image.height = planes.height;
  const size_t texels = size_t(planes.width) * planes.height;
  image.rgba.assign(texels * 4, 255);

  std::vector<float> alpha;
  if (plan.content != Content::kSingleChannel) alpha.resize(texels);

  parallel(planes.height, threads, [&](uint32_t begin, uint32_t end) {
    for (size_t i = size_t(begin) * planes.width; i < size_t(end) * planes.width; i++) {
      uint8_t *t = image.rgba.data() + i * 4;
      switch (plan.content) {
        case Content::kColour: {
          const float a = planes.data[texels * 3 + i];
          for (uint32_t c = 0; c < 3; c++) {
            float v;
            if (!plan.alphaWeighted) {
              v = planes.data[texels * c + i];
            } else if (a > kUnweightFloor) {
              v = planes.data[texels * c + i] / a;
            } else {
              v = planes.data[texels * (4 + c) + i];
            }
            t[c] = plan.srgb ? encodeSrgb(v) : quantise(v);
          }
          alpha[i] = clamp01(a);
          break;
        }
        case Content::kNormal: {
          const float x = planes.data[i];
          const float y = planes.data[texels + i];
          const float z = planes.data[texels * 2 + i];
          const float length = std::sqrt(x * x + y * y + z * z);
          float n[3] = {0.0f, 0.0f, 1.0f};
          if (length > 1e-6f) {
            n[0] = x / length;
            n[1] = y / length;
            n[2] = z / length;
          }
          for (uint32_t c = 0; c < 3; c++) t[c] = quantise(n[c] * 0.5f + 0.5f);
          alpha[i] = clamp01(planes.data[texels * 3 + i]);
          break;
        }
        case Content::kSingleChannel: {
          const uint8_t v = quantise(planes.data[i]);
          t[0] = t[1] = t[2] = v;
          break;
        }
      }
    }
  });

  if (alpha.empty()) return out;

  const bool cutout = plan.content == Content::kColour && plan.cutout > 0.0f;
  double scale = 1.0;
  if (cutout) {
    const uint32_t threshold = alphaThreshold(plan.cutout);
    size_t naive = 0;
    for (float a : alpha) naive += quantise(a) >= threshold ? 1 : 0;
    out.naiveCoverage = double(naive) / double(texels);
    scale = coverageScale(alpha, targetCoverage, threshold);
  }
  size_t passing = 0;
  const uint32_t threshold = cutout ? alphaThreshold(plan.cutout) : 256;
  for (size_t i = 0; i < texels; i++) {
    const uint8_t a = cutout ? scaledAlpha(alpha[i], scale) : quantise(alpha[i]);
    image.rgba[i * 4 + 3] = a;
    passing += a >= threshold ? 1 : 0;
  }
  if (cutout) out.coverage = double(passing) / double(texels);
  return out;
}

// ---------------------------------------------------------------------------
// Formats

enum class Encoding {
  kUastc,
  kRgba8,
  kAstc4x4,
  kBc7,
  kBc5,
  kBc4,
  kEtc2Rgb,
  kEtc2Rgba,
  kEacR11,
  kEacRg11,
};

struct Sample {
  uint32_t bitOffset;
  uint32_t bitLength;  // the number of bits, not the specification's n - 1
  uint32_t channel;    // channel id, before qualifiers
  uint32_t upper;
};

struct Format {
  const char *name;
  uint32_t vkFormatLinear;
  uint32_t vkFormatSrgb;  // 0 when the format has no sRGB variant
  uint32_t model;
  uint32_t blockWidth;
  uint32_t blockHeight;
  uint32_t bytesPerBlock;
  std::vector<Sample> samples;
};

// Channel ids and colour models from the Khronos Data Format Specification
// 1.4 (khr_df.h); vkFormat values from Vulkan. The sample layouts are what
// KTX-Software's createDFDCompressed and createDFDUnpacked write for the
// same vkFormat — the descriptor "must match the format's definition", and
// that is the definition tools validate against. Note ETC2 RGBA: its alpha
// block comes first in the texel block, and its sample does too.
constexpr uint32_t kChannelAlpha = 15;
constexpr uint32_t kQualifierLinear = 0x10;
constexpr uint32_t kModelRgbsda = 1;
constexpr uint32_t kModelBc4 = 131;
constexpr uint32_t kModelBc5 = 132;
constexpr uint32_t kModelBc7 = 134;
constexpr uint32_t kModelEtc2 = 161;
constexpr uint32_t kModelAstc = 162;
constexpr uint32_t kModelUastc = 166;
constexpr uint32_t kUastcRgb = 0;
constexpr uint32_t kUastcRgba = 3;
constexpr uint32_t kUastcRrr = 4;

const Format &formatOf(Encoding encoding) {
  static const Format uastc{"UASTC", 0, 0, kModelUastc, 4, 4, 16,
                            {{0, 128, kUastcRgb, UINT32_MAX}}};
  static const Format rgba8{"R8G8B8A8", 37, 43, kModelRgbsda, 1, 1, 4,
                            {{0, 8, 0, 255}, {8, 8, 1, 255}, {16, 8, 2, 255},
                             {24, 8, kChannelAlpha, 255}}};
  static const Format astc{"ASTC 4x4", 157, 158, kModelAstc, 4, 4, 16,
                           {{0, 128, 0, UINT32_MAX}}};
  static const Format bc7{"BC7", 145, 146, kModelBc7, 4, 4, 16,
                          {{0, 128, 0, UINT32_MAX}}};
  static const Format bc5{"BC5", 141, 0, kModelBc5, 4, 4, 16,
                          {{0, 64, 0, UINT32_MAX}, {64, 64, 1, UINT32_MAX}}};
  static const Format bc4{"BC4", 139, 0, kModelBc4, 4, 4, 8,
                          {{0, 64, 0, UINT32_MAX}}};
  static const Format etc2Rgb{"ETC2 RGB8", 147, 148, kModelEtc2, 4, 4, 8,
                              {{0, 64, 2, UINT32_MAX}}};
  static const Format etc2Rgba{"ETC2 RGBA8", 151, 152, kModelEtc2, 4, 4, 16,
                               {{0, 64, kChannelAlpha, UINT32_MAX},
                                {64, 64, 2, UINT32_MAX}}};
  static const Format eacR11{"EAC R11", 153, 0, kModelEtc2, 4, 4, 8,
                             {{0, 64, 0, UINT32_MAX}}};
  static const Format eacRg11{"EAC RG11", 155, 0, kModelEtc2, 4, 4, 16,
                              {{0, 64, 0, UINT32_MAX}, {64, 64, 1, UINT32_MAX}}};
  switch (encoding) {
    case Encoding::kUastc: return uastc;
    case Encoding::kRgba8: return rgba8;
    case Encoding::kAstc4x4: return astc;
    case Encoding::kBc7: return bc7;
    case Encoding::kBc5: return bc5;
    case Encoding::kBc4: return bc4;
    case Encoding::kEtc2Rgb: return etc2Rgb;
    case Encoding::kEtc2Rgba: return etc2Rgba;
    case Encoding::kEacR11: return eacR11;
    case Encoding::kEacRg11: return eacRg11;
  }
  return uastc;
}

void put32(std::vector<uint8_t> &out, uint32_t v) {
  for (int i = 0; i < 4; i++) out.push_back(uint8_t(v >> (8 * i)));
}

void put64(std::vector<uint8_t> &out, uint64_t v) {
  for (int i = 0; i < 8; i++) out.push_back(uint8_t(v >> (8 * i)));
}

void set64(std::vector<uint8_t> &out, size_t at, uint64_t v) {
  for (int i = 0; i < 8; i++) out[at + i] = uint8_t(v >> (8 * i));
}

/// The Basic Data Format Descriptor, with its leading total size.
std::vector<uint8_t> descriptorFor(const Format &format, bool srgb,
                                   uint32_t uastcChannel) {
  std::vector<uint8_t> dfd;
  const uint32_t samples = uint32_t(format.samples.size());
  const uint32_t blockSize = 24 + 16 * samples;
  put32(dfd, 4 + blockSize);                     // dfdTotalSize
  put32(dfd, 0);                                 // vendorId 0, descriptorType 0
  put32(dfd, 2 | (blockSize << 16));             // versionNumber 2 (1.3/1.4)
  dfd.push_back(uint8_t(format.model));
  dfd.push_back(1);                              // colorPrimaries BT709
  dfd.push_back(srgb ? 2 : 1);                   // transferFunction
  dfd.push_back(0);                              // flags: alpha straight
  dfd.push_back(uint8_t(format.blockWidth - 1));
  dfd.push_back(uint8_t(format.blockHeight - 1));
  dfd.push_back(0);
  dfd.push_back(0);
  put32(dfd, format.bytesPerBlock);              // bytesPlane0..3
  put32(dfd, 0);                                 // bytesPlane4..7
  for (const Sample &sample : format.samples) {
    uint32_t channel = format.model == kModelUastc ? uastcChannel : sample.channel;
    // sRGB describes colour; alpha stays linear, and says so.
    if (srgb && channel == kChannelAlpha) channel |= kQualifierLinear;
    put32(dfd, sample.bitOffset | ((sample.bitLength - 1) << 16) | (channel << 24));
    put32(dfd, 0);                               // samplePosition0..3
    put32(dfd, 0);                               // sampleLower
    put32(dfd, sample.upper);                    // sampleUpper
  }
  return dfd;
}

struct KeyValue {
  std::string key;
  std::string value;  // written with its terminating NUL
};

/// Compresses blobs with zstd, each on its own, all at once: a work queue
/// the threads take from, largest first so the long jobs start together.
/// Each output depends only on its own input, so the order they finish in
/// changes nothing.
bool compressAll(const std::vector<const std::vector<uint8_t> *> &inputs, int zstdLevel,
                 uint32_t threads, std::vector<std::vector<uint8_t>> &outputs,
                 std::string &why) {
  const uint32_t count = uint32_t(inputs.size());
  outputs.assign(count, {});
  std::vector<uint32_t> order(count);
  for (uint32_t i = 0; i < count; i++) order[i] = i;
  std::stable_sort(order.begin(), order.end(), [&](uint32_t a, uint32_t b) {
    return inputs[a]->size() > inputs[b]->size();
  });
  std::vector<std::string> errors(count);
  std::atomic<uint32_t> next(0);
  const auto work = [&] {
    for (uint32_t taken = next++; taken < count; taken = next++) {
      const uint32_t i = order[taken];
      const std::vector<uint8_t> &input = *inputs[i];
      std::vector<uint8_t> packed(ZSTD_compressBound(input.size()));
      const size_t size =
          ZSTD_compress(packed.data(), packed.size(), input.data(), input.size(), zstdLevel);
      if (ZSTD_isError(size)) {
        errors[i] = ZSTD_getErrorName(size);
        continue;
      }
      packed.resize(size);
      outputs[i] = std::move(packed);
    }
  };
  std::vector<std::thread> pool;
  for (uint32_t t = 1; t < std::min(threads, count); t++) pool.emplace_back(work);
  work();
  for (std::thread &thread : pool) thread.join();
  for (const std::string &error : errors) {
    if (!error.empty()) {
      why = "zstd could not compress a level: " + error;
      return false;
    }
  }
  return true;
}

/// A KTX 2.0 file: header, level index, descriptor, key/value data, then the
/// levels smallest first, each already compressed with zstd on its own.
void writeKtx2(uint32_t vkFormat, const std::vector<uint8_t> &dfd, uint32_t width,
               uint32_t height, const std::vector<std::vector<uint8_t>> &levels,
               const std::vector<std::vector<uint8_t>> &compressed,
               std::vector<KeyValue> keyValues, std::vector<uint8_t> &out) {
  const uint32_t count = uint32_t(levels.size());
  std::sort(keyValues.begin(), keyValues.end(),
            [](const KeyValue &a, const KeyValue &b) { return a.key < b.key; });
  std::vector<uint8_t> kvd;
  for (const KeyValue &kv : keyValues) {
    const uint32_t length = uint32_t(kv.key.size() + 1 + kv.value.size() + 1);
    put32(kvd, length);
    kvd.insert(kvd.end(), kv.key.begin(), kv.key.end());
    kvd.push_back(0);
    kvd.insert(kvd.end(), kv.value.begin(), kv.value.end());
    kvd.push_back(0);
    while (kvd.size() % 4 != 0) kvd.push_back(0);
  }

  out.clear();
  out.insert(out.end(), kKtx2Identifier, kKtx2Identifier + 12);
  put32(out, vkFormat);
  put32(out, 1);        // typeSize
  put32(out, width);
  put32(out, height);
  put32(out, 0);        // pixelDepth
  put32(out, 0);        // layerCount
  put32(out, 1);        // faceCount
  put32(out, count);    // levelCount
  put32(out, 2);        // supercompressionScheme: zstd
  const uint32_t dfdOffset = 80 + 24 * count;
  const uint32_t kvdOffset = dfdOffset + uint32_t(dfd.size());
  put32(out, dfdOffset);
  put32(out, uint32_t(dfd.size()));
  put32(out, kvd.empty() ? 0 : kvdOffset);
  put32(out, uint32_t(kvd.size()));
  put64(out, 0);        // sgdByteOffset
  put64(out, 0);        // sgdByteLength
  const size_t index = out.size();
  out.resize(index + 24 * size_t(count), 0);
  out.insert(out.end(), dfd.begin(), dfd.end());
  out.insert(out.end(), kvd.begin(), kvd.end());

  // Smallest level first in the file, so a reader streaming it has
  // something to show early; the index stays largest first. Compressed
  // levels need no alignment padding.
  for (uint32_t i = count; i-- > 0;) {
    const size_t at = index + 24 * size_t(i);
    set64(out, at, out.size());
    set64(out, at + 8, compressed[i].size());
    set64(out, at + 16, levels[i].size());
    out.insert(out.end(), compressed[i].begin(), compressed[i].end());
  }
}

// ---------------------------------------------------------------------------
// Encoding

uint32_t blocksAcross(uint32_t size) { return (size + 3) / 4; }

/// The 4x4 block at (bx, by), repeating the last row and column past the
/// image's edge — the same padding the GPU's decoder ignores.
void blockAt(const Image &image, uint32_t bx, uint32_t by, uint8_t *pixels) {
  for (uint32_t y = 0; y < 4; y++) {
    const uint32_t sy = std::min(by * 4 + y, image.height - 1);
    for (uint32_t x = 0; x < 4; x++) {
      const uint32_t sx = std::min(bx * 4 + x, image.width - 1);
      std::memcpy(pixels + (y * 4 + x) * 4,
                  image.rgba.data() + (size_t(sy) * image.width + sx) * 4, 4);
    }
  }
}

std::vector<basist::uastc_block> encodeUastc(const Image &image, int level,
                                             uint32_t threads) {
  const uint32_t across = blocksAcross(image.width);
  const uint32_t down = blocksAcross(image.height);
  std::vector<basist::uastc_block> blocks(size_t(across) * down);
  parallel(down, threads, [&](uint32_t begin, uint32_t end) {
    uint8_t pixels[64];
    for (uint32_t by = begin; by < end; by++) {
      for (uint32_t bx = 0; bx < across; bx++) {
        blockAt(image, bx, by, pixels);
        basisu::encode_uastc(pixels, blocks[size_t(by) * across + bx], uint32_t(level));
      }
    }
  });
  return blocks;
}

bool transcodeLevel(const std::vector<basist::uastc_block> &blocks, Encoding encoding,
                    uint32_t threads, std::vector<uint8_t> &out) {
  const Format &format = formatOf(encoding);
  const uint32_t count = uint32_t(blocks.size());
  out.assign(size_t(count) * format.bytesPerBlock, 0);
  std::vector<uint8_t> failed(count, 0);
  parallel(count, threads, [&](uint32_t begin, uint32_t end) {
    for (uint32_t i = begin; i < end; i++) {
      const basist::uastc_block &block = blocks[i];
      void *target = out.data() + size_t(i) * format.bytesPerBlock;
      bool ok = true;
      switch (encoding) {
        case Encoding::kUastc:
          std::memcpy(target, block.m_bytes, 16);
          break;
        case Encoding::kAstc4x4:
          ok = basist::transcode_uastc_to_astc(block, target);
          break;
        case Encoding::kBc7:
          ok = basist::transcode_uastc_to_bc7(block, target);
          break;
        case Encoding::kBc5:
          ok = basist::transcode_uastc_to_bc5(block, target, true, 0, 1);
          break;
        case Encoding::kBc4:
          ok = basist::transcode_uastc_to_bc4(block, target, true, 0);
          break;
        case Encoding::kEtc2Rgb:
          // ETC1 blocks: a subset of ETC2 RGB8 that every ETC2 decoder reads.
          ok = basist::transcode_uastc_to_etc1(block, target);
          break;
        case Encoding::kEtc2Rgba:
          ok = basist::transcode_uastc_to_etc2_rgba(block, target);
          break;
        case Encoding::kEacR11:
          ok = basist::transcode_uastc_to_etc2_eac_r11(block, target, true, 0);
          break;
        case Encoding::kEacRg11:
          ok = basist::transcode_uastc_to_etc2_eac_rg11(block, target, true, 0, 1);
          break;
        case Encoding::kRgba8:
          ok = false;
          break;
      }
      failed[i] = ok ? 0 : 1;
    }
  });
  return std::find(failed.begin(), failed.end(), uint8_t(1)) == failed.end();
}

bool encodeAstcDirect(const Image &image, bool srgb, uint32_t threads,
                      std::vector<uint8_t> &out) {
  basisu::image source(image.width, image.height);
  for (uint32_t y = 0; y < image.height; y++) {
    for (uint32_t x = 0; x < image.width; x++) {
      const uint8_t *t = image.rgba.data() + (size_t(y) * image.width + x) * 4;
      source(x, y).set(t[0], t[1], t[2], t[3]);
    }
  }
  basisu::astc_ldr::astc_ldr_encode_config config;
  config.m_astc_block_width = 4;
  config.m_astc_block_height = 4;
  config.m_astc_decode_mode_srgb = srgb;
  config.m_use_dct = false;
  config.m_lossy_supercompression = false;
  basisu::job_pool pool(std::max<uint32_t>(1, threads));
  basisu::uint8_vec intermediate;
  basisu::vector2D<astc_helpers::log_astc_block> coded;
  if (!basisu::astc_ldr::compress_image(source, intermediate, coded, config, pool)) {
    return false;
  }
  const uint32_t across = blocksAcross(image.width);
  const uint32_t down = blocksAcross(image.height);
  if (coded.get_width() != across || coded.get_height() != down) return false;
  out.assign(size_t(across) * down * 16, 0);
  for (uint32_t by = 0; by < down; by++) {
    for (uint32_t bx = 0; bx < across; bx++) {
      astc_helpers::astc_block physical;
      if (!astc_helpers::pack_astc_block(physical, coded(bx, by))) return false;
      std::memcpy(out.data() + (size_t(by) * across + bx) * 16, &physical, 16);
    }
  }
  return true;
}

const char *contentName(Content content) {
  switch (content) {
    case Content::kColour: return "colour";
    case Content::kNormal: return "normal";
    case Content::kSingleChannel: return "single-channel";
  }
  return "colour";
}

}  // namespace

// ---------------------------------------------------------------------------

const char *suffixOf(Family family) {
  switch (family) {
    case kFamilyBasis: return ".ktx2";
    case kFamilyAstc: return ".astc.ktx2";
    case kFamilyBc: return ".bc.ktx2";
    case kFamilyEtc2: return ".etc2.ktx2";
    default: return ".ktx2";
  }
}

bool parseFamilies(const std::string &text, uint32_t &families) {
  families = 0;
  size_t from = 0;
  while (from <= text.size()) {
    size_t to = text.find(',', from);
    if (to == std::string::npos) to = text.size();
    const std::string name = text.substr(from, to - from);
    if (name == "basis") {
      families |= kFamilyBasis;
    } else if (name == "astc") {
      families |= kFamilyAstc;
    } else if (name == "bc") {
      families |= kFamilyBc;
    } else if (name == "etc2") {
      families |= kFamilyEtc2;
    } else if (name == "all") {
      families |= kAllFamilies;
    } else {
      return false;
    }
    from = to + 1;
  }
  return families != 0;
}

int revisionOf(Family family, Content content, bool twoChannelNormals) {
  if (content == Content::kNormal && !twoChannelNormals &&
      (family == kFamilyBc || family == kFamilyEtc2)) {
    return 2;
  }
  return 1;
}

uint32_t alphaThreshold(float cutoff) {
  const double v = std::ceil(double(cutoff) * 255.0 - 1e-9);
  return uint32_t(std::min(255.0, std::max(0.0, v)));
}

double coverageOf(const Image &image, float cutoff) {
  const uint32_t threshold = alphaThreshold(cutoff);
  const size_t texels = size_t(image.width) * image.height;
  if (texels == 0) return 0.0;
  size_t passing = 0;
  for (size_t i = 0; i < texels; i++) passing += image.rgba[i * 4 + 3] >= threshold ? 1 : 0;
  return double(passing) / double(texels);
}

namespace {

Cooked refuse(Cooked cooked, const std::string &why) {
  cooked.files.clear();
  cooked.note = why;
  return cooked;
}

Cooked cookUnguarded(const uint8_t *data, size_t size, const Settings &settings) {
  Cooked cooked;
  Report &report = cooked.report;
  if (settings.families == 0 || (settings.families & ~uint32_t(kAllFamilies)) != 0) {
    return refuse(cooked, "no families to cook for");
  }
  if (settings.zstdLevel < 1 || settings.zstdLevel > 22) {
    return refuse(cooked, "the zstd level must be 1 to 22");
  }
  if (settings.uastcLevel < 0 || settings.uastcLevel > 4) {
    return refuse(cooked, "the UASTC level must be 0 to 4");
  }
  if (settings.cutout >= 0.0f && !(settings.cutout > 0.0f && settings.cutout < 1.0f)) {
    return refuse(cooked, "a cut-out threshold must be between 0 and 1");
  }
  if (settings.twoChannelNormals && settings.content != Content::kNormal) {
    return refuse(cooked, "two-channel normals are for a normal map");
  }
  const uint32_t threads = settings.threads > 0
                               ? settings.threads
                               : std::max<uint32_t>(1, std::thread::hardware_concurrency());
  report.threads = threads;

  auto from = Clock::now();
  Image top;
  std::string why;
  if (!decode(data, size, top, report.source, why)) return refuse(cooked, why);
  report.decodeSeconds = secondsSince(from);

  const Source &source = report.source;
  const Content content = settings.content;
  report.content = content;
  bool srgb;
  switch (settings.transfer) {
    case Transfer::kSrgb:
      srgb = true;
      break;
    case Transfer::kLinear:
      srgb = false;
      break;
    case Transfer::kInfer:
    default:
      if (source.transferKnown) {
        srgb = source.srgb;
        if (srgb && content != Content::kColour) {
          return refuse(cooked, std::string("the file says its texels are sRGB, and a ") +
                                    contentName(content) +
                                    " texture is linear; pass --linear if its bytes "
                                    "are linear after all");
        }
      } else {
        srgb = content == Content::kColour;
      }
      break;
  }
  if (srgb && content != Content::kColour) {
    return refuse(cooked, std::string("a ") + contentName(content) +
                              " texture is linear data and has no sRGB format");
  }
  report.srgb = srgb;

  const bool lossless = settings.lossless;
  report.lossless = lossless;
  report.twoChannelNormals = settings.twoChannelNormals;
  const bool mips = settings.mips < 0 ? !lossless : settings.mips > 0;
  if (lossless) {
    if (content != Content::kColour) {
      return refuse(cooked, "lossless cooking is for colour: pixel art and UI");
    }
    if ((settings.families & kFamilyBasis) == 0) {
      return refuse(cooked, "a lossless texture is x.ktx2 alone, with no siblings; "
                            "include the basis target");
    }
    if (source.sixteenBit) {
      return refuse(cooked, "a 16-bit PNG cannot be kept bit for bit in eight bits "
                            "a channel");
    }
    if (settings.maxSize > 0 && std::max(top.width, top.height) > settings.maxSize) {
      return refuse(cooked, "lossless keeps the image as it is, and --max-size " +
                                std::to_string(settings.maxSize) + " would drop it");
    }
  }

  const bool cutout = settings.cutout > 0.0f;
  if (cutout && content != Content::kColour) {
    return refuse(cooked, "only a colour texture's alpha is tested as a cut-out");
  }
  if (cutout && !source.hasAlpha) {
    report.warnings.push_back("a cut-out threshold was given, but every texel is "
                              "opaque, so there is no coverage to keep");
  }
  report.cutout = cutout && source.hasAlpha ? settings.cutout : -1.0f;

  // Single-channel content keeps red and nothing else, from the top level
  // down, so every family holds the same thing.
  if (content == Content::kSingleChannel) {
    for (size_t i = 0; i < top.rgba.size(); i += 4) {
      top.rgba[i + 1] = top.rgba[i + 2] = top.rgba[i];
      top.rgba[i + 3] = 255;
    }
  }
  const bool hasAlpha = content != Content::kSingleChannel && anyAlpha(top);

  // The mip chain.
  from = Clock::now();
  Plan plan;
  plan.content = content;
  plan.srgb = srgb;
  plan.alphaWeighted = content == Content::kColour && hasAlpha;
  plan.cutout = report.cutout;
  plan.edge = settings.edge;
  const double target = plan.cutout > 0.0f ? coverageOf(top, plan.cutout) : 0.0;
  report.targetCoverage = target;

  std::vector<Image> &levels = cooked.levels;
  std::vector<double> coverage, naive;
  levels.push_back(top);
  coverage.push_back(plan.cutout > 0.0f ? target : -1.0);
  naive.push_back(coverage.back());
  const auto fits = [&](const Image &image) {
    return settings.maxSize == 0 || std::max(image.width, image.height) <= settings.maxSize;
  };
  if (mips || !fits(top)) {
    Planes planes = toPlanes(top, plan);
    while (planes.width > 1 || planes.height > 1) {
      if (!mips && fits(levels.back())) break;
      planes = resample(planes, std::max<uint32_t>(1, planes.width / 2),
                        std::max<uint32_t>(1, planes.height / 2), plan.edge, threads);
      LevelOut level = fromPlanes(planes, plan, target, threads);
      levels.push_back(std::move(level.image));
      coverage.push_back(level.coverage);
      naive.push_back(level.naiveCoverage);
    }
  }
  while (levels.size() > 1 && !fits(levels.front())) {
    levels.erase(levels.begin());
    coverage.erase(coverage.begin());
    naive.erase(naive.begin());
  }
  if (!mips) {
    levels.resize(1);
    coverage.resize(1);
    naive.resize(1);
  }
  if (plan.cutout > 0.0f) {
    report.coverage = coverage;
    report.naiveCoverage = naive;
  }
  report.mipSeconds = secondsSince(from);
  report.width = levels.front().width;
  report.height = levels.front().height;
  report.levels = uint32_t(levels.size());
  if (!lossless && (report.width % 4 != 0 || report.height % 4 != 0)) {
    report.warnings.push_back("the top level is " + std::to_string(report.width) + "x" +
                              std::to_string(report.height) +
                              ", not a multiple of 4; Direct3D and WebGL refuse "
                              "block-compressed textures like that");
  }

  // What every file says about where it came from. No times and no paths, so
  // the bytes depend on the input and the settings alone.
  std::string parameters = content == Content::kColour
                               ? std::string()
                               : std::string("--") + contentName(content) + " ";
  parameters += std::string(srgb ? "--srgb" : "--linear") + " --zstd " +
                std::to_string(settings.zstdLevel);
  if (lossless) {
    parameters += " --lossless";
  } else {
    parameters += " --uastc " + std::to_string(settings.uastcLevel);
  }
  parameters += mips ? " --mips kaiser3" : " --no-mips";
  if (settings.maxSize > 0) parameters += " --max-size " + std::to_string(settings.maxSize);
  if (settings.edge == Edge::kWrap) parameters += " --wrap";
  if (plan.cutout > 0.0f) {
    char text[32];
    std::snprintf(text, sizeof(text), " --cutout %.4f", double(plan.cutout));
    parameters += text;
  }
  if (settings.twoChannelNormals) parameters += " --two-channel-normals";
  std::string directParameters = parameters + " --astc direct";
  // Each file names its own revision, not the cooker's version, so a file
  // whose bytes a new cooker would not change keeps exactly the bytes it had.
  const auto keyValues = [&](Family family, const std::string &scParameters) {
    const std::string writer =
        "Orblit texture cook " +
        std::to_string(revisionOf(family, content, settings.twoChannelNormals)) +
        " (Basis Universal 2.1.0r, zstd " + ZSTD_versionString() + ")";
    return std::vector<KeyValue>{{"KTXorientation", "rd"},
                                 {"KTXwriter", writer},
                                 {"KTXwriterScParams", scParameters}};
  };

  // Every file's levels, encoded; then all of them compressed together;
  // then each file assembled.
  struct Pending {
    Family family;
    Encoding encoding;
    std::vector<std::vector<uint8_t>> levels;
    std::string scParameters;
  };
  std::vector<Pending> pending;
  uint32_t uastcChannel = hasAlpha ? kUastcRgba : kUastcRgb;
  if (content == Content::kSingleChannel) uastcChannel = kUastcRrr;

  if (lossless) {
    Pending file{kFamilyBasis, Encoding::kRgba8, {}, parameters};
    for (const Image &level : levels) file.levels.push_back(level.rgba);
    pending.push_back(std::move(file));
  } else {
    const uint32_t families = settings.families;
    const bool directAstc =
        settings.astc == AstcRoute::kDirect ||
        (settings.astc == AstcRoute::kAuto && source.container != "ktx2");
    report.directAstc = directAstc && (families & kFamilyAstc) != 0;
    const bool needUastc = (families & (kFamilyBasis | kFamilyBc | kFamilyEtc2)) != 0 ||
                           ((families & kFamilyAstc) != 0 && !directAstc);
    std::vector<std::vector<basist::uastc_block>> uastc;
    if (needUastc) {
      from = Clock::now();
      for (const Image &level : levels) {
        uastc.push_back(encodeUastc(level, settings.uastcLevel, threads));
      }
      report.uastcSeconds = secondsSince(from);
    }

    std::vector<std::pair<Family, Encoding>> jobs;
    if (families & kFamilyAstc) jobs.push_back({kFamilyAstc, Encoding::kAstc4x4});
    // A normal map takes the colour formats — three channels, linear —
    // unless two channels were asked for; see OrblitTextureCook.h.
    const bool twoChannel = content == Content::kNormal && settings.twoChannelNormals;
    if (families & kFamilyBc) {
      jobs.push_back({kFamilyBc, content == Content::kSingleChannel ? Encoding::kBc4
                                 : twoChannel                       ? Encoding::kBc5
                                                                    : Encoding::kBc7});
    }
    if (families & kFamilyEtc2) {
      jobs.push_back({kFamilyEtc2, content == Content::kSingleChannel ? Encoding::kEacR11
                                   : twoChannel                       ? Encoding::kEacRg11
                                   : hasAlpha                         ? Encoding::kEtc2Rgba
                                                                      : Encoding::kEtc2Rgb});
    }
    if (families & kFamilyBasis) jobs.push_back({kFamilyBasis, Encoding::kUastc});

    from = Clock::now();
    for (const auto &job : jobs) {
      const bool direct = job.second == Encoding::kAstc4x4 && directAstc;
      Pending file{job.first, job.second, std::vector<std::vector<uint8_t>>(levels.size()),
                   direct ? directParameters : parameters};
      for (size_t i = 0; i < levels.size(); i++) {
        const bool ok = direct ? encodeAstcDirect(levels[i], srgb, threads, file.levels[i])
                               : transcodeLevel(uastc[i], job.second, threads, file.levels[i]);
        if (!ok) {
          return refuse(cooked, std::string("could not encode level ") + std::to_string(i) +
                                    " as " + formatOf(job.second).name);
        }
      }
      pending.push_back(std::move(file));
    }
    report.familySeconds = secondsSince(from);
  }

  from = Clock::now();
  std::vector<const std::vector<uint8_t> *> blobs;
  for (const Pending &file : pending) {
    for (const std::vector<uint8_t> &level : file.levels) blobs.push_back(&level);
  }
  std::vector<std::vector<uint8_t>> compressed;
  if (!compressAll(blobs, settings.zstdLevel, threads, compressed, why)) {
    return refuse(cooked, why);
  }
  size_t taken = 0;
  for (Pending &file : pending) {
    const Format &format = formatOf(file.encoding);
    File out;
    out.family = file.family;
    out.suffix = suffixOf(file.family);
    out.format = format.name;
    out.vkFormat = srgb && format.vkFormatSrgb != 0 ? format.vkFormatSrgb
                                                    : format.vkFormatLinear;
    const bool fileSrgb =
        srgb && (format.vkFormatSrgb != 0 || file.encoding == Encoding::kUastc);
    const std::vector<std::vector<uint8_t>> levelBlobs(
        std::make_move_iterator(compressed.begin() + std::ptrdiff_t(taken)),
        std::make_move_iterator(compressed.begin() + std::ptrdiff_t(taken + file.levels.size())));
    taken += file.levels.size();
    writeKtx2(out.vkFormat, descriptorFor(format, fileSrgb, uastcChannel), report.width,
              report.height, file.levels, levelBlobs, keyValues(file.family, file.scParameters),
              out.bytes);
    cooked.files.push_back(std::move(out));
  }
  report.compressSeconds = secondsSince(from);
  return cooked;
}

}  // namespace

Cooked cook(const uint8_t *data, size_t size, const Settings &settings) {
  initialise();
  try {
    return cookUnguarded(data, size, settings);
  } catch (const std::bad_alloc &) {
    Cooked cooked;
    cooked.note = "ran out of memory";
    return cooked;
  } catch (const std::exception &error) {
    Cooked cooked;
    cooked.note = std::string("failed: ") + error.what();
    return cooked;
  }
}

}  // namespace texturecook
}  // namespace orblit
