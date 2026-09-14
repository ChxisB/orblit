#include "OrblitRendererCore.h"

// The renderer's work, in the order OrblitRenderer.mm had it. See the header
// for how this file maps onto the old one.

#include <filament/LightManager.h>
#include <filament/Options.h>
#include <filament/RenderableManager.h>
#include <filament/TransformManager.h>
#include <filament/Viewport.h>
#include <geometry/SurfaceOrientation.h>
#include <gltfio/materials/uberarchive.h>
#include <image/Ktx1Bundle.h>
#include <ktxreader/Ktx1Reader.h>
#include <utils/EntityManager.h>
#include <utils/Panic.h>

#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <exception>
#include <limits>

#include "OrblitBackend.h"
#include "OrblitDecals.h"

// M_PI is POSIX rather than C++, and MSVC only defines it when asked to.
#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

using namespace filament;
using namespace filament::math;

namespace orblit {
namespace {

// The compiled materials and the lookup tables, as the C arrays setup.sh
// writes. In an anonymous namespace because xxd chooses their names and
// makes them global: a library other hosts link should not export
// `klit_opaqueMaterial` into their symbol table, and two copies of the
// renderer in one binary — as there briefly were while it moved — would not
// link at all.
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
#include "shadowcatcher_material.h"
#include "sky_material.h"
#include "rain_material.h"

}  // namespace
