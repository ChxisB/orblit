// The materials and lookup tables setup.sh compiles into C arrays, behind a
// lookup.
//
// The generated headers are included in exactly one place — this header's
// .cpp — because xxd chooses their names and makes them global: a library
// other hosts link should not export `klit_opaqueMaterial` into their symbol
// table, and two copies of them in one binary would not link at all. The
// renderer itself is a dozen files, and inlining the packages into each is
// tens of megabytes of duplicate material in the binary. ScreenEffects.cpp
// keeps god rays' and distortion's packages to itself for the same reason,
// which is why they are not here.
#pragma once

#include <cstddef>
#include <cstdint>

namespace orblit {

/// The materials that have one use each, rather than a family of variants.
enum class Package {
  mist,
  sky,
  rain,
  instanced,
  depth,
  irradiance,
};

/// The bytes one of those was compiled to.
void materialPackage(Package which, const uint8_t **package, size_t *length);

/// One of the surfaces `Renderer::surfaceIndexFor` numbers.
///
/// `slim` picks the slim lit tier for the first five (shading 0, "lit"),
/// which are in the same blend order as the standard grid's first five, and
/// is ignored for the rest. An index outside the grid is clamped to nought.
void surfacePackage(int index, bool slim, const uint8_t **package,
                    size_t *length);

/// The material behind an effect pass whose number the renderer owns:
/// sharpen, SMAA's three passes, copy and bounce. False for the effects whose
/// packages live elsewhere — motion blur's, and the two screen effects'.
bool effectPackage(int effect, const uint8_t **package, size_t *length);

/// Whether this build's materials were generated with the slim lit tier. A
/// build without it cannot draw lit objects on a device below feature level
/// 3; `materialTiers` says which tiers it does have, for the note.
bool hasSlimSurface();
const char *materialTiers();

/// One of SMAA's two precomputed tables: the bytes, and the shape to upload
/// them with. MIT, Jorge Jimenez et al. — see LICENSES/SMAA.txt.
struct SmaaTable {
  const uint8_t *bytes;
  size_t size;
  uint32_t width;
  uint32_t height;
};

SmaaTable smaaAreaTable();
SmaaTable smaaSearchTable();

/// The two fitted tables the area-light integral reads, each kLtcSide square
/// and RGBA float, laid out row by row.
constexpr uint32_t kLtcSide = 64;

const float *ltcMatrixTable();
const float *ltcFresnelTable();

}  // namespace orblit
