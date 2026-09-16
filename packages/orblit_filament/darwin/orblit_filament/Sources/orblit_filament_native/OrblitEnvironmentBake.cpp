#include "OrblitEnvironmentBake.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>
#include <random>

// Every function below names the libibl function it is ported from. The
// arithmetic, its order and its float-versus-double choices are libibl's,
// because a harmonic that is right to five places and wrong in the sixth is
// still a diffuse light that differs from the bake it is meant to match.

namespace orblit {

namespace {

// Filament's math/scalar.h, which libibl multiplies by as doubles.
constexpr double kPi = 3.14159265358979323846264338327950288;
constexpr double kOneOverPi = 0.318309886183790671537767526745028724;
constexpr double kTwoOverSqrtPi = 1.12837916709551257389615890312154517;
constexpr double kSqrt2 = 1.41421356237309504880168872420969808;
constexpr double kSqrtHalf = 0.707106781186547524400844362104849039;

struct Vec3 {
  float x, y, z;
};

inline Vec3 operator+(Vec3 a, Vec3 b) { return {a.x + b.x, a.y + b.y, a.z + b.z}; }
inline Vec3 operator-(Vec3 a, Vec3 b) { return {a.x - b.x, a.y - b.y, a.z - b.z}; }
inline Vec3 operator-(Vec3 a) { return {-a.x, -a.y, -a.z}; }
inline Vec3 operator*(Vec3 a, float s) { return {a.x * s, a.y * s, a.z * s}; }
inline Vec3 operator*(float s, Vec3 a) { return {s * a.x, s * a.y, s * a.z}; }
inline Vec3 operator/(Vec3 a, float s) { return {a.x / s, a.y / s, a.z / s}; }
inline Vec3 &operator+=(Vec3 &a, Vec3 b) {
  a = a + b;
  return a;
}
inline float dot(Vec3 a, Vec3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
inline Vec3 cross(Vec3 a, Vec3 b) {
  return {a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x};
}
inline Vec3 normalize(Vec3 a) { return a * (1.0f / std::sqrt(dot(a, a))); }

/// A 3×3 matrix by its columns, as Filament's TMat33 holds one.
struct Mat3 {
  Vec3 column[3];
};

inline Vec3 operator*(const Mat3 &m, Vec3 v) {
  return m.column[0] * v.x + m.column[1] * v.y + m.column[2] * v.z;
}

inline Mat3 transpose(const Mat3 &m) {
  return {{{m.column[0].x, m.column[1].x, m.column[2].x},
           {m.column[0].y, m.column[1].y, m.column[2].y},
           {m.column[0].z, m.column[1].z, m.column[2].z}}};
}

inline Vec3 readTexel(const float *texel) { return {texel[0], texel[1], texel[2]}; }

inline void writeTexel(float *texel, Vec3 value) {
  texel[0] = value.x;
  texel[1] = value.y;
  texel[2] = value.z;
}

enum Face : uint32_t { kPX = 0, kNX, kPY, kNY, kPZ, kNZ };

/// Cubemap::getDirectionFor, float version: where a point on a face looks.
inline Vec3 directionFor(uint32_t face, float x, float y, float scale) {
  const float cx = (x * scale) - 1;
  const float cy = 1 - (y * scale);
  Vec3 direction{};
  const float length = std::sqrt(cx * cx + cy * cy + 1);
  switch (face) {
    case kPX: direction = {1, cy, -cx}; break;
    case kNX: direction = {-1, cy, cx}; break;
    case kPY: direction = {cx, 1, -cy}; break;
    case kNY: direction = {cx, -1, cy}; break;
    case kPZ: direction = {cx, cy, 1}; break;
    default: direction = {-cx, cy, -1}; break;
  }
  return direction * (1 / length);
}

struct Address {
  uint32_t face;
  float s;
  float t;
};

/// Cubemap::getAddressFor: which face a direction lands on, and where.
inline Address addressFor(Vec3 r) {
  Address address{};
  float sc = 0;
  float tc = 0;
  float ma = 0;
  const float rx = std::abs(r.x);
  const float ry = std::abs(r.y);
  const float rz = std::abs(r.z);
  if (rx >= ry && rx >= rz) {
    ma = 1.0f / rx;
    if (r.x >= 0) {
      address.face = kPX;
      sc = -r.z;
      tc = -r.y;
    } else {
      address.face = kNX;
      sc = r.z;
      tc = -r.y;
    }
  } else if (ry >= rx && ry >= rz) {
    ma = 1.0f / ry;
    if (r.y >= 0) {
      address.face = kPY;
      sc = r.x;
      tc = r.z;
    } else {
      address.face = kNY;
      sc = r.x;
      tc = -r.z;
    }
  } else {
    ma = 1.0f / rz;
    if (r.z >= 0) {
      address.face = kPZ;
      sc = r.x;
      tc = -r.y;
    } else {
      address.face = kNZ;
      sc = -r.x;
      tc = -r.y;
    }
  }
  address.s = (sc * ma + 1.0f) * 0.5f;
  address.t = (tc * ma + 1.0f) * 0.5f;
  return address;
}

/// Cubemap::sampleAt(direction): the nearest texel.
inline Vec3 nearest(const CpuCubemap &cube, Vec3 direction) {
  const Address address = addressFor(direction);
  const size_t size = cube.size;
  const size_t x = std::min(size_t(address.s * size), size - 1);
  const size_t y = std::min(size_t(address.t * size), size - 1);
  return readTexel(cube.at(address.face, int(x), int(y)));
}

/// Cubemap::filterAt(image, x, y): bilinear, reading into the border.
inline Vec3 bilinear(const CpuCubemap &cube, uint32_t face, float x, float y) {
  const size_t x0 = size_t(x);
  const size_t y0 = size_t(y);
  const size_t x1 = x0 + 1;
  const size_t y1 = y0 + 1;
  const float u = float(x - x0);
  const float v = float(y - y0);
  const float oneMinusU = 1 - u;
  const float oneMinusV = 1 - v;
  const Vec3 c0 = readTexel(cube.at(face, int(x0), int(y0)));
  const Vec3 c1 = readTexel(cube.at(face, int(x1), int(y0)));
  const Vec3 c2 = readTexel(cube.at(face, int(x0), int(y1)));
  const Vec3 c3 = readTexel(cube.at(face, int(x1), int(y1)));
  return (oneMinusU * oneMinusV) * c0 + (u * oneMinusV) * c1 +
         (oneMinusU * v) * c2 + (u * v) * c3;
}

/// Cubemap::trilinearFilterAt.
inline Vec3 trilinear(const CpuCubemap &level0, const CpuCubemap &level1,
                      float lerp, Vec3 direction) {
  const Address address = addressFor(direction);
  const float upper0 = std::nextafter(float(level0.size), 0.0f);
  const float upper1 = std::nextafter(float(level1.size), 0.0f);
  const float x0 = std::min(address.s * level0.size, upper0);
  const float y0 = std::min(address.t * level0.size, upper0);
  const float x1 = std::min(address.s * level1.size, upper1);
  const float y1 = std::min(address.t * level1.size, upper1);
  Vec3 c0 = bilinear(level0, address.face, x0, y0);
  c0 += lerp * (bilinear(level1, address.face, x1, y1) - c0);
  return c0;
}

/// ibl::hammersley.
inline void hammersley(uint32_t i, float iN, float &u, float &v) {
  constexpr float tof = 0.5f / 0x80000000U;
  uint32_t bits = i;
  bits = (bits << 16u) | (bits >> 16u);
  bits = ((bits & 0x55555555u) << 1u) | ((bits & 0xAAAAAAAAu) >> 1u);
  bits = ((bits & 0x33333333u) << 2u) | ((bits & 0xCCCCCCCCu) >> 2u);
  bits = ((bits & 0x0F0F0F0Fu) << 4u) | ((bits & 0xF0F0F0F0u) >> 4u);
  bits = ((bits & 0x00FF00FFu) << 8u) | ((bits & 0xFF00FF00u) >> 8u);
  u = i * iN;
  v = bits * tof;
}

// ---- Spherical harmonics (CubemapSH) ----

inline size_t shIndex(long m, size_t l) { return size_t(long(l * (l + 1)) + m); }

/// CubemapSH's factorial: n! / d!.
float factorial(size_t n, size_t d = 1) {
  d = std::max(size_t(1), d);
  n = std::max(size_t(1), n);
  float r = 1.0;
  if (n == d) {
  } else if (n > d) {
    for (; n > d; n--) r *= n;
  } else {
    for (; d > n; d--) r *= d;
    r = 1.0f / r;
  }
  return r;
}

float kml(long m, size_t l) {
  m = m < 0 ? -m : m;
  const float k = (2 * l + 1) * factorial(size_t(long(l) - m), size_t(long(l) + m));
  return float(std::sqrt(k) * (kTwoOverSqrtPi * 0.25));
}

std::vector<float> ki(size_t bands) {
  std::vector<float> k(bands * bands);
  for (size_t l = 0; l < bands; l++) {
    k[shIndex(0, l)] = kml(0, l);
    for (size_t m = 1; m <= l; m++) {
      k[shIndex(long(m), l)] = k[shIndex(-long(m), l)] =
          float(kSqrt2 * kml(long(m), l));
    }
  }
  return k;
}

float truncatedCosSh(size_t l) {
  if (l == 0) return float(kPi);
  if (l == 1) return float(2 * kPi / 3);
  if (l & 1u) return 0;
  const size_t half = l / 2;
  const float a0 = ((half & 1u) ? 1.0f : -1.0f) / ((l + 2) * (l - 1));
  const float a1 = factorial(l, half) / (factorial(half) * (1 << l));
  return float(2 * kPi * a0 * a1);
}

/// CubemapSH::computeShBasis, for three bands.
void shBasis(float *basis, Vec3 s) {
  constexpr size_t bands = 3;
  float pml2 = 0;
  float pml1 = 1;
  basis[0] = pml1;
  for (size_t l = 1; l < bands; l++) {
    const float pml = ((2 * l - 1.0f) * pml1 * s.z - (l - 1.0f) * pml2) / l;
    pml2 = pml1;
    pml1 = pml;
    basis[shIndex(0, l)] = pml;
  }
  float pmm = 1;
  for (size_t m = 1; m < bands; m++) {
    pmm = (1.0f - 2 * m) * pmm;
    pml2 = pmm;
    pml1 = (2 * m + 1.0f) * pmm * s.z;
    basis[shIndex(-long(m), m)] = pml2;
    basis[shIndex(long(m), m)] = pml2;
    if (m + 1 < bands) {
      basis[shIndex(-long(m), m + 1)] = pml1;
      basis[shIndex(long(m), m + 1)] = pml1;
      for (size_t l = m + 2; l < bands; l++) {
        const float pml =
            ((2 * l - 1.0f) * pml1 * s.z - (l + m - 1.0f) * pml2) / (l - m);
        pml2 = pml1;
        pml1 = pml;
        basis[shIndex(-long(m), l)] = pml;
        basis[shIndex(long(m), l)] = pml;
      }
    }
  }
  float cm = s.x;
  float sm = s.y;
  for (size_t m = 1; m <= bands; m++) {
    for (size_t l = m; l < bands; l++) {
      basis[shIndex(-long(m), l)] *= sm;
      basis[shIndex(long(m), l)] *= cm;
    }
    const float cm1 = cm * s.x - sm * s.y;
    const float sm1 = sm * s.x + cm * s.y;
    cm = cm1;
    sm = sm1;
  }
}

/// CubemapUtils::solidAngle.
float solidAngle(size_t size, size_t u, size_t v) {
  const auto quadrant = [](float x, float y) {
    return std::atan2(x * y, std::sqrt(x * x + y * y + 1));
  };
  const float iSize = 1.0f / size;
  const float s = ((u + 0.5f) * 2 * iSize) - 1;
  const float t = ((v + 0.5f) * 2 * iSize) - 1;
  const float x0 = s - iSize;
  const float y0 = t - iSize;
  const float x1 = s + iSize;
  const float y1 = t + iSize;
  return quadrant(x0, y0) - quadrant(x0, y1) - quadrant(x1, y0) +
         quadrant(x1, y1);
}

using Sh3 = std::array<float, 9>;
using Float5 = std::array<float, 5>;

/// CubemapSH::multiply.
Float5 multiply(const Float5 m[5], const Float5 &x) {
  Float5 out{};
  for (size_t j = 0; j < 5; j++) {
    out[j] = m[0][j] * x[0] + m[1][j] * x[1] + m[2][j] * x[2] +
             m[3][j] * x[3] + m[4][j] * x[4];
  }
  return out;
}

/// CubemapSH::rotateShericalHarmonicBand1.
Vec3 rotateBand1(Vec3 band1, const Mat3 &m) {
  const Mat3 invA1TimesK{{{0, -1, 0}, {0, 0, 1}, {-1, 0, 0}}};
  const Vec3 mn0 = m.column[0];
  const Vec3 mn1 = m.column[1];
  const Vec3 mn2 = m.column[2];
  const Mat3 r1OverK{{{-mn0.y, mn0.z, -mn0.x},
                      {-mn1.y, mn1.z, -mn1.x},
                      {-mn2.y, mn2.z, -mn2.x}}};
  return r1OverK * (invA1TimesK * band1);
}

/// CubemapSH::rotateShericalHarmonicBand2.
Float5 rotateBand2(const Float5 &band2, const Mat3 &m) {
  constexpr float kSqrt3 = 1.7320508076f;
  const float n = float(kSqrtHalf);
  const Float5 invATimesK[5] = {{0, 1, 2, 0, 0},
                                {-1, 0, 0, 0, -2},
                                {0, kSqrt3, 0, 0, 0},
                                {1, 1, 0, -2, 0},
                                {2, 1, 0, 0, 0}};
  const auto project = [](Vec3 s) -> Float5 {
    return {(s.y * s.x), -(s.y * s.z), 1 / (2 * kSqrt3) * ((3 * s.z * s.z - 1)),
            -(s.z * s.x), 0.5f * ((s.x * s.x - s.y * s.y))};
  };
  const Float5 invATimesKTimesBand2 = multiply(invATimesK, band2);
  const Float5 rOverK[5] = {project(m.column[0]), project(m.column[2]),
                            project(n * (m.column[0] + m.column[1])),
                            project(n * (m.column[0] + m.column[2])),
                            project(n * (m.column[1] + m.column[2]))};
  return multiply(rOverK, invATimesKTimesBand2);
}

Sh3 rotateSh3(const Sh3 &sh, const Mat3 &m) {
  const Vec3 b1 = rotateBand1({sh[1], sh[2], sh[3]}, m);
  const Float5 b2 = rotateBand2({sh[4], sh[5], sh[6], sh[7], sh[8]}, m);
  return {sh[0], b1.x, b1.y, b1.z, b2[0], b2[1], b2[2], b2[3], b2[4]};
}

/// The polynomial form's constants, from "Stupid Spherical Harmonics".
constexpr float kSqrtPiF = 1.7724538509f;
constexpr float kSqrt3F = 1.7320508076f;
constexpr float kSqrt5F = 2.2360679775f;
constexpr float kSqrt15F = 3.8729833462f;
constexpr float kPolynomial[9] = {
    1.0f / (2.0f * kSqrtPiF),       -kSqrt3F / (2.0f * kSqrtPiF),
    kSqrt3F / (2.0f * kSqrtPiF),    -kSqrt3F / (2.0f * kSqrtPiF),
    kSqrt15F / (2.0f * kSqrtPiF),   -kSqrt15F / (2.0f * kSqrtPiF),
    kSqrt5F / (4.0f * kSqrtPiF),    -kSqrt15F / (2.0f * kSqrtPiF),
    kSqrt15F / (4.0f * kSqrtPiF)};

/// windowSH's shmin: the least value three bands reach anywhere on the
/// sphere (Sloan, "Deringing Spherical Harmonics").
float shMinimum(Sh3 f) {
  const float *a = kPolynomial;
  const Vec3 direction = normalize({-f[3], -f[1], f[2]});
  const Vec3 zAxis = -direction;
  const Vec3 xAxis = normalize(cross(zAxis, {0, 1, 0}));
  const Vec3 yAxis = cross(xAxis, zAxis);
  const Mat3 m = transpose(Mat3{{xAxis, yAxis, -zAxis}});
  f = rotateSh3(f, m);

  const float m2max = a[8] * std::sqrt(f[8] * f[8] + f[4] * f[4]);
  const float qa = 3 * a[6] * f[6] + m2max;
  const float qb = a[2] * f[2];
  const float qc = a[0] * f[0] - a[6] * f[6] - m2max;
  const float zmin = -qb / (2.0f * qa);
  const float m0minZ = qa * zmin * zmin + qb * zmin + qc;
  const float m0minB = std::min(qa + qb + qc, qa - qb + qc);
  const float m0min = (qa > 0 && zmin >= -1 && zmin <= 1) ? m0minZ : m0minB;
  const float d = a[4] * std::sqrt(f[5] * f[5] + f[7] * f[7]);

  float minimum = m0min - 0.5f * d;
  if (minimum < 0) {
    const auto func = [=](float x) {
      return (qa * x * x + qb * x + qc) + (d * x * std::sqrt(1 - x * x));
    };
    const auto increment = [=](float x) {
      return (x * x - 1) *
             (d - 2 * d * x * x + (qb + 2 * qa * x) * std::sqrt(1 - x * x)) /
             (3 * d * x - 2 * d * x * x * x -
              2 * qa * std::pow(1 - x * x, 1.5f));
    };
    float dz = 0;
    float z = float(-kSqrtHalf);
    // Newton's method, as libibl runs it, with one addition: a cap on the
    // steps. libibl's loop has none, and a pair of harmonics that sends it
    // back and forth between two points inside [−1, 1] would never return —
    // a hang no environment should be able to cause. Every convergent case
    // stops long before the cap, so the answer is libibl's.
    int steps = 0;
    do {
      minimum = func(z);
      dz = increment(z);
      z = z - dz;
    } while (std::abs(z) <= 1 && std::abs(dz) > 1e-5f && ++steps < 64);
    if (std::abs(z) > 1) minimum = std::min(func(1), func(-1));
  }
  return minimum;
}

float sincWindow(size_t l, float w) {
  if (l == 0) return 1.0f;
  if (l >= w) return 0.0f;
  float x = (float(kPi) * l) / w;
  x = std::sin(x) / x;
  return float(std::pow(x, 4));
}

Sh3 windowed(Sh3 f, float cutoff) {
  for (size_t l = 0; l < 3; l++) {
    const float w = sincWindow(l, cutoff);
    f[shIndex(0, l)] *= w;
    for (size_t m = 1; m <= l; m++) {
      f[shIndex(-long(m), l)] *= w;
      f[shIndex(long(m), l)] *= w;
    }
  }
  return f;
}

// ---- Prefiltering (CubemapIBL) ----

Vec3 importanceSampleGgx(float u, float v, float a) {
  const float phi = 2.0f * float(kPi) * u;
  const float cosTheta2 = (1 - v) / (1 + (a + 1) * ((a - 1) * v));
  const float cosTheta = std::sqrt(cosTheta2);
  const float sinTheta = std::sqrt(1 - cosTheta2);
  return {sinTheta * std::cos(phi), sinTheta * std::sin(phi), cosTheta};
}

float distributionGgx(float noh, float linearRoughness) {
  const float a = linearRoughness;
  const float f = (a - 1) * ((a + 1) * (noh * noh)) + 1;
  return (a * a) / (float(kPi) * f * f);
}

inline float log4(float x) { return std::log2(x) * 0.5f; }

inline float saturate(float x) { return std::min(1.0f, std::max(0.0f, x)); }

float lodToPerceptualRoughness(float lod) {
  const float a = 2.0f;
  const float b = -1.0f;
  return (lod != 0) ? saturate((std::sqrt(a * a + 4.0f * b * lod) - a) /
                               (2.0f * b))
                    : 0.0f;
}

uint32_t log2Of(uint32_t powerOfTwo) {
  uint32_t exponent = 0;
  while ((uint32_t(1) << exponent) < powerOfTwo) exponent++;
  return exponent;
}

/// CubemapIBL::roughnessFilter, prefiltered importance sampling, no mirror.
void roughnessFilter(CpuCubemap &dst, const std::vector<CpuCubemap> &levels,
                     float linearRoughness, size_t maxSamples,
                     const ForEach &forEach) {
  const float scale = 2.0f / dst.size;
  const uint32_t size = dst.size;

  if (linearRoughness == 0) {
    forEach(6, [&](size_t face) {
      for (uint32_t y = 0; y < size; y++) {
        for (uint32_t x = 0; x < size; x++) {
          const Vec3 n = directionFor(uint32_t(face), x + 0.5f, y + 0.5f, scale);
          writeTexel(dst.at(uint32_t(face), int(x), int(y)), nearest(levels[0], n));
        }
      }
    });
    return;
  }

  const float samples = float(maxSamples);
  const float inverseSamples = 1.0f / samples;
  const size_t maxLevel = levels.size() - 1;
  const float maxLevelF = float(maxLevel);
  const size_t size0 = levels[0].size;
  const float omegaP = (4.0f * float(kPi)) / float(6 * size0 * size0);

  struct Entry {
    Vec3 l;
    float brdfNoL;
    float lerp;
    uint8_t l0;
    uint8_t l1;
  };
  std::vector<Entry> cache;
  cache.reserve(maxSamples);
  float weight = 0;
  for (size_t index = 0; index < maxSamples; index++) {
    float u = 0;
    float v = 0;
    hammersley(uint32_t(index), inverseSamples, u, v);
    const Vec3 h = importanceSampleGgx(u, v, linearRoughness);
    const float noh = h.z;
    const float noh2 = h.z * h.z;
    const float nol = 2 * noh2 - 1;
    const Vec3 l{2 * noh * h.x, 2 * noh * h.y, nol};
    if (nol > 0) {
      const float pdf = distributionGgx(noh, linearRoughness) / 4;
      constexpr float k = 4;
      const float omegaS = 1 / (samples * pdf);
      const float lod = float(log4(omegaS) - log4(omegaP) + log4(k));
      const float mipLevel = std::min(std::max(lod, 0.0f), maxLevelF);
      weight += nol;
      const uint8_t l0 = uint8_t(mipLevel);
      const uint8_t l1 = uint8_t(std::min(maxLevel, size_t(l0 + 1)));
      cache.push_back({l, nol, mipLevel - float(l0), l0, l1});
    }
  }
  for (Entry &entry : cache) entry.brdfNoL *= 1.0f / weight;
  std::sort(cache.begin(), cache.end(), [](const Entry &a, const Entry &b) {
    return a.brdfNoL < b.brdfNoL;
  });

  forEach(6, [&](size_t face) {
    // A generator per face, fresh, as libibl's per-job state is.
    std::default_random_engine generator;
    std::uniform_real_distribution<float> angle{float(-kPi), float(kPi)};
    for (uint32_t y = 0; y < size; y++) {
      for (uint32_t x = 0; x < size; x++) {
        const Vec3 n = directionFor(uint32_t(face), x + 0.5f, y + 0.5f, scale);
        const Vec3 up = std::abs(n.z) < 0.999 ? Vec3{0, 0, 1} : Vec3{1, 0, 0};
        // In doubles, as libibl's `mat3 R` is.
        const Vec3 r0f = normalize(cross(up, n));
        const Vec3 r1f = cross(n, r0f);
        const double theta = angle(generator);
        const double c = std::cos(theta);
        const double s = std::sin(theta);
        const double r0[3] = {c * r0f.x + s * r1f.x, c * r0f.y + s * r1f.y,
                              c * r0f.z + s * r1f.z};
        const double r1[3] = {-s * r0f.x + c * r1f.x, -s * r0f.y + c * r1f.y,
                              -s * r0f.z + c * r1f.z};
        Vec3 li{0, 0, 0};
        for (const Entry &entry : cache) {
          const Vec3 l{float(r0[0] * entry.l.x + r1[0] * entry.l.y +
                             double(n.x) * entry.l.z),
                       float(r0[1] * entry.l.x + r1[1] * entry.l.y +
                             double(n.y) * entry.l.z),
                       float(r0[2] * entry.l.x + r1[2] * entry.l.y +
                             double(n.z) * entry.l.z)};
          li += trilinear(levels[entry.l0], levels[entry.l1], entry.lerp, l) *
                entry.brdfNoL;
        }
        writeTexel(dst.at(uint32_t(face), int(x), int(y)), li);
      }
    }
  });
}

}  // namespace

void forEachInOrder(size_t count, const std::function<void(size_t)> &body) {
  for (size_t i = 0; i < count; i++) body(i);
}

CpuCubemap CpuCubemap::ofSize(uint32_t size) {
  CpuCubemap cube;
  cube.size = size;
  cube.texels.assign(6 * size_t(size + 2) * size_t(size + 2) * 3, 0.0f);
  return cube;
}

/// CubemapUtils::equirectangularToCubemap.
CpuCubemap cubemapFromEquirectangular(const HdrImage &picture, uint32_t size,
                                      const ForEach &forEach) {
  CpuCubemap cube = CpuCubemap::ofSize(size);
  const size_t width = picture.width;
  const size_t height = picture.height;
  const float scale = 2.0f / size;
  const float *pixels = picture.rgb.get();

  const auto toRectilinear = [width, height](Vec3 s, float &x, float &y) {
    float xf = float(std::atan2(s.x, s.z) * kOneOverPi);
    float yf = float(std::asin(s.y) * (2 * kOneOverPi));
    xf = (xf + 1.0f) * 0.5f * (width - 1);
    yf = (1.0f - yf) * 0.5f * (height - 1);
    x = xf;
    y = yf;
  };

  forEach(6, [&](size_t faceIndex) {
    const uint32_t face = uint32_t(faceIndex);
    for (uint32_t y = 0; y < size; y++) {
      for (uint32_t x = 0; x < size; x++) {
        float px[4];
        float py[4];
        toRectilinear(directionFor(face, x + 0.0f, y + 0.0f, scale), px[0], py[0]);
        toRectilinear(directionFor(face, x + 1.0f, y + 0.0f, scale), px[1], py[1]);
        toRectilinear(directionFor(face, x + 0.0f, y + 1.0f, scale), px[2], py[2]);
        toRectilinear(directionFor(face, x + 1.0f, y + 1.0f, scale), px[3], py[3]);
        const float minX = std::min(px[0], std::min(px[1], std::min(px[2], px[3])));
        const float maxX = std::max(px[0], std::max(px[1], std::max(px[2], px[3])));
        const float minY = std::min(py[0], std::min(py[1], std::min(py[2], py[3])));
        const float maxY = std::max(py[0], std::max(py[1], std::max(py[2], py[3])));
        const float dx = std::max(1.0f, maxX - minX);
        const float dy = std::max(1.0f, maxY - minY);
        const size_t samples = size_t(dx * dy);
        const float inverseSamples = 1.0f / samples;
        Vec3 c{0, 0, 0};
        for (size_t sample = 0; sample < samples; sample++) {
          float hu = 0;
          float hv = 0;
          hammersley(uint32_t(sample), inverseSamples, hu, hv);
          float sx = 0;
          float sy = 0;
          toRectilinear(directionFor(face, x + hu, y + hv, scale), sx, sy);
          c += readTexel(pixels + (size_t(uint32_t(sy)) * width +
                                   size_t(uint32_t(sx))) *
                                      3);
        }
        c = c * inverseSamples;
        writeTexel(cube.at(face, int(x), int(y)), c);
      }
    }
  });
  return cube;
}

/// CubemapUtils::mirrorCubemap.
CpuCubemap mirroredCubemap(const CpuCubemap &cube, const ForEach &forEach) {
  CpuCubemap mirrored = CpuCubemap::ofSize(cube.size);
  const float scale = 2.0f / cube.size;
  forEach(6, [&](size_t face) {
    for (uint32_t y = 0; y < cube.size; y++) {
      for (uint32_t x = 0; x < cube.size; x++) {
        const Vec3 n = directionFor(uint32_t(face), x + 0.5f, y + 0.5f, scale);
        writeTexel(mirrored.at(uint32_t(face), int(x), int(y)),
                   nearest(cube, {-n.x, n.y, n.z}));
      }
    }
  });
  return mirrored;
}

/// Cubemap::makeSeamless.
void makeSeamless(CpuCubemap &cube) {
  const int d = int(cube.size);
  const int last = d - 1;
  struct Step {
    int dx;
    int dy;
  };
  constexpr Step kRight{1, 0};
  constexpr Step kLeft{-1, 0};
  constexpr Step kDown{0, 1};
  constexpr Step kUp{0, -1};

  const auto stitch = [&](uint32_t to, int toX, int toY, Step toStep,
                          uint32_t from, int fromX, int fromY, Step fromStep) {
    for (int i = 0; i < d; i++) {
      std::memcpy(cube.at(to, toX + i * toStep.dx, toY + i * toStep.dy),
                  cube.at(from, fromX + i * fromStep.dx, fromY + i * fromStep.dy),
                  3 * sizeof(float));
    }
  };
  const auto corners = [&](uint32_t face) {
    const auto t = [&](int x, int y) { return readTexel(cube.at(face, x, y)); };
    writeTexel(cube.at(face, -1, -1), (t(0, 0) + t(-1, 0) + t(0, -1)) / 3);
    writeTexel(cube.at(face, last + 1, -1),
               (t(last, 0) + t(last, -1) + t(last + 1, 0)) / 3);
    writeTexel(cube.at(face, -1, last + 1),
               (t(0, last) + t(-1, last) + t(0, last + 1)) / 3);
    // libibl's own fourth corner reads (L+1, L) twice and (L, L+1) not at
    // all; kept, so a seam reads as cmgen's does.
    writeTexel(cube.at(face, last + 1, last + 1),
               (t(last, last) + t(last + 1, last) + t(last + 1, last)) / 3);
  };

  stitch(kPY, -1, 0, kDown, kNX, 0, 0, kRight);
  stitch(kPY, 0, -1, kRight, kNZ, last, 0, kLeft);
  stitch(kPY, d, 0, kDown, kPX, last, 0, kLeft);
  stitch(kPY, 0, d, kRight, kPZ, 0, 0, kRight);
  corners(kPY);

  stitch(kNX, -1, 0, kDown, kNZ, last, 0, kDown);
  stitch(kNX, 0, -1, kRight, kPY, 0, 0, kDown);
  stitch(kNX, d, 0, kDown, kPZ, 0, 0, kDown);
  stitch(kNX, 0, d, kRight, kNY, 0, last, kUp);
  corners(kNX);

  stitch(kPZ, -1, 0, kDown, kNX, last, 0, kDown);
  stitch(kPZ, 0, -1, kRight, kPY, 0, last, kRight);
  stitch(kPZ, d, 0, kDown, kPX, 0, 0, kDown);
  stitch(kPZ, 0, d, kRight, kNY, 0, 0, kRight);
  corners(kPZ);

  stitch(kPX, -1, 0, kDown, kPZ, last, 0, kDown);
  stitch(kPX, 0, -1, kRight, kPY, last, last, kUp);
  stitch(kPX, d, 0, kDown, kNZ, 0, 0, kDown);
  stitch(kPX, 0, d, kRight, kNY, last, 0, kDown);
  corners(kPX);

  stitch(kNZ, -1, 0, kDown, kPX, last, 0, kDown);
  stitch(kNZ, 0, -1, kRight, kPY, last, 0, kLeft);
  stitch(kNZ, d, 0, kDown, kNX, 0, 0, kDown);
  stitch(kNZ, 0, d, kRight, kNY, last, last, kLeft);
  corners(kNZ);

  stitch(kNY, -1, 0, kDown, kNX, last, last, kLeft);
  stitch(kNY, 0, -1, kRight, kPZ, 0, last, kRight);
  stitch(kNY, d, 0, kDown, kPX, 0, last, kRight);
  stitch(kNY, 0, d, kRight, kNZ, last, last, kLeft);
  corners(kNY);
}

/// CubemapUtils::downsampleCubemapLevelBoxFilter, then makeSeamless, as
/// cmgen's generateMipmaps does.
CpuCubemap halvedCubemap(const CpuCubemap &cube) {
  CpuCubemap half = CpuCubemap::ofSize(std::max<uint32_t>(1, cube.size / 2));
  const uint32_t scale = cube.size / half.size;
  for (uint32_t face = 0; face < 6; face++) {
    for (uint32_t y = 0; y < half.size; y++) {
      for (uint32_t x = 0; x < half.size; x++) {
        const int x0 = int(x * scale);
        const int y0 = int(y * scale);
        const Vec3 c0 = readTexel(cube.at(face, x0, y0));
        const Vec3 c1 = readTexel(cube.at(face, x0 + 1, y0));
        const Vec3 c2 = readTexel(cube.at(face, x0, y0 + 1));
        const Vec3 c3 = readTexel(cube.at(face, x0 + 1, y0 + 1));
        writeTexel(half.at(face, int(x), int(y)), (c0 + c1 + c2 + c3) * 0.25f);
      }
    }
  }
  makeSeamless(half);
  return half;
}

/// CubemapSH::computeSH(3 bands, irradiance), windowSH(auto) and
/// preprocessSHForShader, as cmgen's sphericalHarmonics runs them for a KTX.
Harmonics irradianceHarmonics(const CpuCubemap &cube, const ForEach &forEach) {
  constexpr size_t bands = 3;
  constexpr size_t coefficients = bands * bands;
  const uint32_t size = cube.size;
  const float scale = 2.0f / size;

  // Summed a face at a time and the faces then summed in order, so the
  // result does not depend on how many threads ran it.
  std::array<std::array<Vec3, coefficients>, 6> faces{};
  forEach(6, [&](size_t face) {
    std::array<Vec3, coefficients> sum{};
    float basis[coefficients];
    for (uint32_t y = 0; y < size; y++) {
      for (uint32_t x = 0; x < size; x++) {
        const Vec3 s = directionFor(uint32_t(face), x + 0.5f, y + 0.5f, scale);
        Vec3 colour = readTexel(cube.at(uint32_t(face), int(x), int(y)));
        colour = colour * solidAngle(size, x, y);
        shBasis(basis, s);
        for (size_t i = 0; i < coefficients; i++) sum[i] += colour * basis[i];
      }
    }
    faces[face] = sum;
  });
  std::array<Vec3, coefficients> sh{};
  for (const auto &face : faces) {
    for (size_t i = 0; i < coefficients; i++) sh[i] += face[i];
  }

  std::vector<float> k = ki(bands);
  for (size_t l = 0; l < bands; l++) {
    const float cosine = truncatedCosSh(l);
    k[shIndex(0, l)] *= cosine;
    for (size_t m = 1; m <= l; m++) {
      k[shIndex(-long(m), l)] *= cosine;
      k[shIndex(long(m), l)] *= cosine;
    }
  }
  for (size_t i = 0; i < coefficients; i++) sh[i] = sh[i] * k[i];

  // windowSH, automatic: the largest window, per channel, under which the
  // reconstruction is nowhere negative; then the smallest of the three.
  float cutoff = float(bands * 4 + 1);
  for (size_t channel = 0; channel < 3; channel++) {
    Sh3 f{};
    for (size_t i = 0; i < coefficients; i++) {
      f[i] = channel == 0 ? sh[i].x : channel == 1 ? sh[i].y : sh[i].z;
    }
    float low = float(bands);
    float high = cutoff;
    for (size_t step = 0; step < 16 && low + 0.1f < high; step++) {
      const float middle = 0.5f * (low + high);
      if (shMinimum(windowed(f, middle)) < 0) {
        high = middle;
      } else {
        low = middle;
      }
    }
    cutoff = std::min(cutoff, low);
  }
  for (size_t l = 0; l < bands; l++) {
    const float w = sincWindow(l, cutoff);
    sh[shIndex(0, l)] = sh[shIndex(0, l)] * w;
    for (size_t m = 1; m <= l; m++) {
      sh[shIndex(-long(m), l)] = sh[shIndex(-long(m), l)] * w;
      sh[shIndex(long(m), l)] = sh[shIndex(long(m), l)] * w;
    }
  }

  // preprocessSHForShader: the polynomial constants and the Lambertian 1/π,
  // multiplied in as a double the way Filament's float3 *= double does.
  Harmonics out{};
  for (size_t i = 0; i < coefficients; i++) {
    const double factor = double(kPolynomial[i]) * kOneOverPi;
    out[i * 3] = float(sh[i].x * factor);
    out[i * 3 + 1] = float(sh[i].y * factor);
    out[i * 3 + 2] = float(sh[i].z * factor);
  }
  return out;
}

uint32_t prefilterLevelCount(uint32_t size) {
  const uint32_t base = log2Of(size);
  uint32_t smallest = log2Of(16);
  if (smallest >= base) smallest = 0;
  return base + 1 - smallest;
}

/// tools/cmgen's iblRoughnessPrefilter.
std::vector<CpuCubemap> roughnessPrefilter(const std::vector<CpuCubemap> &mips,
                                           uint32_t samples,
                                           const ForEach &forEach) {
  std::vector<CpuCubemap> levels;
  if (mips.empty()) return levels;
  const uint32_t base = log2Of(mips[0].size);
  const uint32_t count = prefilterLevelCount(mips[0].size);
  size_t sampleCount = samples;
  for (uint32_t level = 0; level < count; level++) {
    if (level >= 2) sampleCount *= 2;
    const float lod = saturate(level / (count - 1.0f));
    const float perceptual = lodToPerceptualRoughness(lod);
    CpuCubemap out = CpuCubemap::ofSize(uint32_t(1) << (base - level));
    roughnessFilter(out, mips, perceptual * perceptual, sampleCount, forEach);
    makeSeamless(out);
    levels.push_back(std::move(out));
  }
  return levels;
}

HdrImage halvedImage(const HdrImage &picture) {
  HdrImage half;
  half.width = std::max<uint32_t>(1, picture.width / 2);
  half.height = std::max<uint32_t>(1, picture.height / 2);
  const size_t pixels = size_t(half.width) * half.height;
  half.rgb.reset(static_cast<float *>(std::malloc(pixels * 3 * sizeof(float))));
  if (half.rgb == nullptr) {
    half.width = half.height = 0;
    return half;
  }
  const size_t sourceWidth = picture.width;
  const uint32_t stepX = picture.width > 1 ? 2 : 1;
  const uint32_t stepY = picture.height > 1 ? 2 : 1;
  for (size_t y = 0; y < half.height; y++) {
    for (size_t x = 0; x < half.width; x++) {
      const size_t x0 = x * stepX;
      const size_t y0 = y * stepY;
      const size_t x1 = x0 + stepX - 1;
      const size_t y1 = y0 + stepY - 1;
      for (int c = 0; c < 3; c++) {
        const float sum = picture.rgb[(y0 * sourceWidth + x0) * 3 + c] +
                          picture.rgb[(y0 * sourceWidth + x1) * 3 + c] +
                          picture.rgb[(y1 * sourceWidth + x0) * 3 + c] +
                          picture.rgb[(y1 * sourceWidth + x1) * 3 + c];
        half.rgb[(y * half.width + x) * 3 + c] = sum * 0.25f;
      }
    }
  }
  return half;
}

uint16_t halfFloat(float value) {
  if (std::isnan(value)) return 0;
  constexpr float kLargest = 65504.0f;
  value = std::max(-kLargest, std::min(kLargest, value));
  uint32_t bits = 0;
  std::memcpy(&bits, &value, sizeof bits);
  const uint16_t sign = uint16_t((bits >> 16) & 0x8000);
  const int exponent = int((bits >> 23) & 0xFF) - 127;
  const uint32_t mantissa = bits & 0x7FFFFF;
  // Below half the smallest subnormal half float: a signed nought.
  if (exponent < -25) return sign;
  if (exponent < -14) {
    // A subnormal half float counts in steps of 2^-24, so the float's whole
    // significand, leading one included, is shifted down to that step and
    // rounded to nearest, ties to even.
    const uint32_t significand = mantissa | 0x800000;
    const int shift = -exponent - 1;
    uint32_t half = significand >> shift;
    const uint32_t remainder = significand & ((uint32_t(1) << shift) - 1);
    const uint32_t halfway = uint32_t(1) << (shift - 1);
    if (remainder > halfway || (remainder == halfway && (half & 1))) half++;
    return uint16_t(sign | half);
  }
  uint32_t half = uint32_t(exponent + 15) << 10 | (mantissa >> 13);
  const uint32_t remainder = mantissa & 0x1FFF;
  if (remainder > 0x1000 || (remainder == 0x1000 && (half & 1))) half++;
  // Rounding up past the largest half float is held at it.
  if (half >= 0x7C00) half = 0x7BFF;
  return uint16_t(sign | half);
}

std::vector<uint16_t> halfFloatRgba(const float *rgb, size_t count) {
  std::vector<uint16_t> out(count * 4);
  const uint16_t one = halfFloat(1.0f);
  for (size_t i = 0; i < count; i++) {
    out[i * 4] = halfFloat(rgb[i * 3]);
    out[i * 4 + 1] = halfFloat(rgb[i * 3 + 1]);
    out[i * 4 + 2] = halfFloat(rgb[i * 3 + 2]);
    out[i * 4 + 3] = one;
  }
  return out;
}

std::vector<uint16_t> halfFloatRgba(const CpuCubemap &cube) {
  const size_t size = cube.size;
  std::vector<uint16_t> out(6 * size * size * 4);
  const uint16_t one = halfFloat(1.0f);
  size_t at = 0;
  for (uint32_t face = 0; face < 6; face++) {
    for (uint32_t y = 0; y < size; y++) {
      for (uint32_t x = 0; x < size; x++) {
        const float *texel = cube.at(face, int(x), int(y));
        out[at++] = halfFloat(texel[0]);
        out[at++] = halfFloat(texel[1]);
        out[at++] = halfFloat(texel[2]);
        out[at++] = one;
      }
    }
  }
  return out;
}

}  // namespace orblit
