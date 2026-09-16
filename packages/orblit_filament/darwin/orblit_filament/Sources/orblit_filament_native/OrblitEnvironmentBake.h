#pragma once

// cmgen's arithmetic on the CPU, with nothing of Filament's in it.
//
// What turns a decoded equirectangular picture into the two things a matte
// surface and a rough one read: three bands of spherical harmonics for the
// diffuse, and — where the GPU cannot filter — a small prefiltered cubemap for
// the reflections.
//
// Ported, step for step, from Filament's libibl (Apache 2.0, the same
// licence as Filament; see LICENSES/Apache-2.0.txt) as cmgen calls it:
// CubemapUtils::equirectangularToCubemap, mirrorCubemap and
// downsampleCubemapLevelBoxFilter, Cubemap::makeSeamless and its filtering,
// CubemapSH::computeSH, windowSH and preprocessSHForShader, and
// CubemapIBL::roughnessFilter as tools/cmgen's iblRoughnessPrefilter drives
// it. Ported rather than called because libibl only runs on a
// utils::JobSystem, whose header Filament's release does not ship, and a
// JobSystem only runs work on a thread the engine has adopted — never on a
// worker of the renderer's own, and never in the browser worker this is
// meant to move to. The port keeps libibl's float and double choices, so the
// harmonics come out as cmgen's own; native/headless's environment check
// compares them with the ones cmgen writes into a KTX.
//
// Pure functions of their arguments, safe on any thread.

#include <array>
#include <cstddef>
#include <cstdint>
#include <functional>
#include <vector>

#include "OrblitHdrImage.h"

namespace orblit {

/// Runs `body(i)` for every i below `count`, however the caller likes: on
/// every core natively (orblit::parallelFor), in a loop where there are no
/// threads. Every body here writes only its own face, so any order is right
/// and the results are the same bit for bit.
using ForEach =
    std::function<void(size_t count, const std::function<void(size_t)> &body)>;

/// A loop, for callers with nothing better.
void forEachInOrder(size_t count, const std::function<void(size_t)> &body);

/// A cubemap in memory, laid out as libibl lays one out: six square faces of
/// linear RGB in Filament's order (+X, −X, +Y, −Y, +Z, −Z), rows top first,
/// each face with a one-texel border that makeSeamless fills from its
/// neighbours so a bilinear read at an edge lands on the next face.
struct CpuCubemap {
  uint32_t size = 0;
  /// 6 × (size + 2)² × 3 floats.
  std::vector<float> texels;

  static CpuCubemap ofSize(uint32_t size);

  /// The texel at `x`, `y` of `face`, where −1 and `size` are the border.
  float *at(uint32_t face, int x, int y) {
    const size_t stride = size + 2;
    return &texels[((face * stride + size_t(y + 1)) * stride + size_t(x + 1)) *
                   3];
  }
  const float *at(uint32_t face, int x, int y) const {
    return const_cast<CpuCubemap *>(this)->at(face, x, y);
  }
};

/// Three bands of spherical harmonics, nine RGB coefficients end to end,
/// pre-scaled for Filament's shader: what IndirectLight::Builder::irradiance
/// takes, and what cmgen writes into a KTX's "sh" metadata.
using Harmonics = std::array<float, 27>;

/// The cube `size` texels a side that cmgen makes of an equirectangular
/// picture: each texel the average of as many picture pixels as its corners
/// span.
CpuCubemap cubemapFromEquirectangular(const HdrImage &picture, uint32_t size,
                                      const ForEach &forEach);

/// The same cube seen in a mirror across X, which cmgen does to every cube
/// it makes, and which Filament's own equirectangular filter does too.
CpuCubemap mirroredCubemap(const CpuCubemap &cube, const ForEach &forEach);

/// Fills every face's border from the faces beside it.
void makeSeamless(CpuCubemap &cube);

/// Half the size, each texel the average of the four it covers, seamless.
CpuCubemap halvedCubemap(const CpuCubemap &cube);

/// The diffuse light a surface facing any way receives, as cmgen computes
/// it: projected onto three bands, convolved with a cosine lobe, windowed
/// until it rings nowhere negative, and pre-scaled for the shader.
Harmonics irradianceHarmonics(const CpuCubemap &cube, const ForEach &forEach);

/// How many roughness levels cmgen writes for a cube `size` texels a side:
/// every level down to sixteen texels, or all of them for a cube that small.
uint32_t prefilterLevelCount(uint32_t size);

/// cmgen's reflections: level 0 the mirror, each level after it blurred by a
/// rougher GGX lobe, `samples` a texel for the first two levels and doubling
/// from the third, each sample reading the mip chain `mips` (the mirrored,
/// seamless cube at every size down to one) at the level its footprint
/// covers. Every level is returned seamless.
std::vector<CpuCubemap> roughnessPrefilter(const std::vector<CpuCubemap> &mips,
                                           uint32_t samples,
                                           const ForEach &forEach);

/// `picture` at half the width and height, each pixel the average of the
/// four it covers — how a picture larger than it needs to be is brought down
/// before it is uploaded. An odd last row or column is dropped.
HdrImage halvedImage(const HdrImage &picture);

/// `count` RGB floats as RGBA half floats, alpha one. Values past the
/// largest half float are held at it rather than becoming infinite, which a
/// filter would spread to every texel near it.
std::vector<uint16_t> halfFloatRgba(const float *rgb, size_t count);

/// A cube's six faces, without their borders, as RGBA half floats in face
/// order: what a cubemap texture's setImage takes for one level.
std::vector<uint16_t> halfFloatRgba(const CpuCubemap &cube);

/// One float as an IEEE half float, rounded to nearest.
uint16_t halfFloat(float value);

}  // namespace orblit
