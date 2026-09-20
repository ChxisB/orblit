#include "OrblitMaterialPackages.h"

#include "OrblitRendererCore.h"

namespace orblit {
namespace {

// What setup.sh chose to build. A combination left out of a set is still a
// header and still a symbol — it points at the package standing in for it —
// so this is the only thing that says which packages are really distinct.
// __has_include because a set generated before the manifest existed holds
// every combination, which is what the fallback says.
#if defined(__has_include)
#if __has_include("material_set.h")
#include "material_set.h"
#endif
#endif
#ifndef ORBLIT_HAS_SLIM_SURFACE
#define ORBLIT_HAS_SLIM_SURFACE 1
#define ORBLIT_MATERIAL_TIERS "full slim"
#endif

#include "lit_opaque_material.h"
#include "sharpen_material.h"
#include "smaa_edges_material.h"
#include "smaa_weights_material.h"
#include "smaa_blend_material.h"
#include "bounce_material.h"
#include "irradiance_material.h"
#include "copy_material.h"
// SMAA's precomputed tables, fetched by setup.sh from the reference
// implementation. MIT, Jorge Jimenez et al. — see LICENSES/SMAA.txt.
#include "AreaTex.h"
#include "SearchTex.h"
#include "LtcTables.h"
#include "lit_transparent_material.h"
#include "lit_fade_material.h"
#include "lit_masked_material.h"
#include "lit_add_material.h"
// The slim lit surface: chosen instead of the five above when the engine
// cannot manage feature level 3. See lit_slim.mat and surfaceAt below.
#include "lit_slim_opaque_material.h"
#include "lit_slim_transparent_material.h"
#include "lit_slim_fade_material.h"
#include "lit_slim_masked_material.h"
#include "lit_slim_add_material.h"
#include "unlit_opaque_material.h"
#include "unlit_transparent_material.h"
#include "unlit_fade_material.h"
#include "unlit_masked_material.h"
#include "unlit_add_material.h"
#include "video_opaque_material.h"
#include "video_transparent_material.h"
#include "video_fade_material.h"
#include "video_masked_material.h"
#include "video_add_material.h"
#include "mist_material.h"
#include "instanced_material.h"
#include "depth_material.h"
#include "shadowcatcher_material.h"
#include "sky_material.h"
#include "rain_material.h"

// The lit, unlit, video and shadow-catcher grid, in the order
// Renderer::surfaceIndexFor numbers it: shading first, then blend.
const uint8_t *const kSurfacePackages[kSurfaceCount] = {
    klit_opaqueMaterial,        klit_transparentMaterial,
    klit_fadeMaterial,          klit_maskedMaterial,
    klit_addMaterial,           kunlit_opaqueMaterial,
    kunlit_transparentMaterial, kunlit_fadeMaterial,
    kunlit_maskedMaterial,      kunlit_addMaterial,
    kvideo_opaqueMaterial,      kvideo_transparentMaterial,
    kvideo_fadeMaterial,        kvideo_maskedMaterial,
    kvideo_addMaterial,         kshadowcatcherMaterial,
};
const size_t kSurfaceSizes[kSurfaceCount] = {
    klit_opaqueMaterial_len,        klit_transparentMaterial_len,
    klit_fadeMaterial_len,          klit_maskedMaterial_len,
    klit_addMaterial_len,           kunlit_opaqueMaterial_len,
    kunlit_transparentMaterial_len, kunlit_fadeMaterial_len,
    kunlit_maskedMaterial_len,      kunlit_addMaterial_len,
    kvideo_opaqueMaterial_len,      kvideo_transparentMaterial_len,
    kvideo_fadeMaterial_len,        kvideo_maskedMaterial_len,
    kvideo_addMaterial_len,         kshadowcatcherMaterial_len,
};

// The slim lit surface's own five, chosen instead of the standard grid's
// first five where a device cannot build those. _slimSurface is decided once
// in startWithWidth and does not change, so an engine never mixes the two.
constexpr int kSlimSurfaces = 5;
const uint8_t *const kSlimLitPackages[kSlimSurfaces] = {
    klit_slim_opaqueMaterial, klit_slim_transparentMaterial,
    klit_slim_fadeMaterial,   klit_slim_maskedMaterial,
    klit_slim_addMaterial,
};
const size_t kSlimLitSizes[kSlimSurfaces] = {
    klit_slim_opaqueMaterial_len, klit_slim_transparentMaterial_len,
    klit_slim_fadeMaterial_len,   klit_slim_maskedMaterial_len,
    klit_slim_addMaterial_len,
};

// Checked here because this is the only file that can see the arrays: a
// generator that changed their shape should stop the build rather than have
// the wrong number of texels uploaded.
static_assert(sizeof(kLtcMatrix) / sizeof(float) == kLtcSide * kLtcSide * 4,
              "the LTC matrix table is not 64x64 RGBA");
static_assert(sizeof(kLtcFresnel) / sizeof(float) == kLtcSide * kLtcSide * 4,
              "the LTC Fresnel table is not 64x64 RGBA");

}  // namespace

void materialPackage(Package which, const uint8_t **package, size_t *length) {
  switch (which) {
    case Package::mist:
      *package = kmistMaterial;
      *length = kmistMaterial_len;
      return;
    case Package::sky:
      *package = kskyMaterial;
      *length = kskyMaterial_len;
      return;
    case Package::rain:
      *package = krainMaterial;
      *length = krainMaterial_len;
      return;
    case Package::instanced:
      *package = kinstancedMaterial;
      *length = kinstancedMaterial_len;
      return;
    case Package::depth:
      *package = kdepthMaterial;
      *length = kdepthMaterial_len;
      return;
    case Package::irradiance:
      *package = kirradianceMaterial;
      *length = kirradianceMaterial_len;
      return;
  }
}

void surfacePackage(int index, bool slim, const uint8_t **package,
                    size_t *length) {
  if (index < 0 || index >= kSurfaceCount) index = 0;
  if (slim && index < kSlimSurfaces) {
    *package = kSlimLitPackages[index];
    *length = kSlimLitSizes[index];
    return;
  }
  *package = kSurfacePackages[index];
  *length = kSurfaceSizes[index];
}

bool effectPackage(int effect, const uint8_t **package, size_t *length) {
  switch (effect) {
    case kEffectSharpen:
      *package = ksharpenMaterial;
      *length = ksharpenMaterial_len;
      return true;
    case kEffectSmaaEdges:
      *package = ksmaa_edgesMaterial;
      *length = ksmaa_edgesMaterial_len;
      return true;
    case kEffectSmaaWeights:
      *package = ksmaa_weightsMaterial;
      *length = ksmaa_weightsMaterial_len;
      return true;
    case kEffectSmaaBlend:
      *package = ksmaa_blendMaterial;
      *length = ksmaa_blendMaterial_len;
      return true;
    case kEffectBounce:
      *package = kbounceMaterial;
      *length = kbounceMaterial_len;
      return true;
    case kEffectCopy:
      *package = kcopyMaterial;
      *length = kcopyMaterial_len;
      return true;
    default:
      return false;
  }
}

bool hasSlimSurface() { return ORBLIT_HAS_SLIM_SURFACE != 0; }

const char *materialTiers() { return ORBLIT_MATERIAL_TIERS; }

SmaaTable smaaAreaTable() {
  return {areaTexBytes, AREATEX_SIZE, AREATEX_WIDTH, AREATEX_HEIGHT};
}

SmaaTable smaaSearchTable() {
  return {searchTexBytes, SEARCHTEX_SIZE, SEARCHTEX_WIDTH, SEARCHTEX_HEIGHT};
}

const float *ltcMatrixTable() { return kLtcMatrix; }

const float *ltcFresnelTable() { return kLtcFresnel; }

}  // namespace orblit
