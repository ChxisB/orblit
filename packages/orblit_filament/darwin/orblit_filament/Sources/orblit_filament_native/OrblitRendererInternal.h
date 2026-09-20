// What every one of the renderer's own source files needs, and nothing else.
//
// `orblit::Renderer` was one 7,000-line file until it was fourteen (see
// PORTING.md). They all open with the same includes, so those are here rather
// than repeated; so are the two helpers that more than one of them uses, which
// would otherwise be two copies drifting apart. Not a public header — nothing
// outside this directory includes it, and nothing here is part of the
// renderer's interface.
#pragma once

#include "OrblitRendererCore.h"

#include <filament/LightManager.h>
#include <filament/Options.h>
#include <filament/RenderableManager.h>
#include <filament/TransformManager.h>
#include <filament/Viewport.h>
#include <geometry/SurfaceOrientation.h>
#include <gltfio/Animator.h>
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
#include "OrblitHdrImage.h"
#include "OrblitImport.h"
#include "OrblitMaterialPackages.h"

// M_PI is POSIX rather than C++, and MSVC only defines it when asked to.
#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

namespace orblit {

/// Which axis a stated field of view is measured across.
///
/// Filament's four-argument setProjection measures it vertically. That is
/// right for a window wider than it is tall and wrong for one that is not:
/// the vertical extent stays where it was put while the horizontal collapses
/// with the aspect ratio. A phone held upright is 384 by 832, and showed 24
/// degrees across where a 1600 by 1200 desktop window showed 64 -- roughly a
/// third of the width -- so every scene looked as though the camera had been
/// shoved into it, and the examples framed for a desktop did not survive the
/// trip. Measuring across whichever axis is shorter means a stated 50 degrees
/// is 50 degrees of the dimension that actually constrains the shot.
///
/// Landscape is arithmetically untouched: aspect >= 1 takes the branch
/// setProjection would have taken on its own, so every frame this repository
/// has ever drawn or compared is the frame it was.
inline filament::Camera::Fov fovAxisFor(double aspect) {
  return aspect < 1.0 ? filament::Camera::Fov::HORIZONTAL
                      : filament::Camera::Fov::VERTICAL;
}

/// The geometry of a triangle that covers the screen.
///
/// The buffers are the caller's to destroy: building a renderable out of them
/// does not hand them over.
struct ScreenTriangle {
  filament::VertexBuffer *vertices = nullptr;
  filament::IndexBuffer *indices = nullptr;
};

/// Builds one.
///
/// A triangle rather than a quad, and bigger than the screen rather than
/// exactly it: two triangles meeting across the middle of the frame make the
/// hardware shade the pixels along that seam twice, and one oversized triangle
/// covers everything with no seam to pay for.
///
/// Positions are already in clip space — the materials drawn this way are
/// `vertexDomain : device`, so nothing transforms them — and the UVs are what
/// the fragment reads its source image by.
ScreenTriangle makeScreenTriangle(filament::Engine &engine);

/// Makes `entity` the renderable that draws one, wearing `material`.
void buildScreenRenderable(filament::Engine &engine, utils::Entity entity,
                           filament::MaterialInstance *material,
                           const ScreenTriangle &triangle);

}  // namespace orblit
