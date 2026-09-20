#include "OrblitRendererInternal.h"

// The render graph's targets, and the tables and shadow map the passes
// through them read.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

// Hook (screen effects): the host's god-ray and distortion settings, kept by
// the plain C++ side until an effect pass reads them.
void Renderer::setGodRays(const float *godRays, size_t count, const float *distortions, size_t distortionCount) {
  if (_disposed) return;
  _screenEffects.setGodRays(godRays, count);
  _screenEffects.setDistortions(distortions, distortionCount);
}

void Renderer::setRenderGraph(const float *passes, uint32_t count, const float *targets, uint32_t targetCount, const std::vector<std::string> &names) {
  if (_disposed || _engine == nullptr) return;
  if (count > kMaxPasses) count = kMaxPasses;

  std::vector<float> passParams(passes, passes + count * kPassStride);
  std::vector<float> targetParams(targets,
                                  targets + targetCount * kTargetStride);
  std::vector<std::string> targetNames;
  targetNames.reserve(names.size());
  for (const std::string &name : names) targetNames.emplace_back(name);

  // A graph arrives on every frame like everything else, and rebuilding views
  // and render targets sixty times a second to say nothing changed is the
  // whole cost of the feature paid for nothing.
  if (passParams == _graphPassParams && targetParams == _graphTargetParams &&
      targetNames == _graphTargetNames) {
    return;
  }
  _graphPassParams = std::move(passParams);
  _graphTargetParams = std::move(targetParams);
  _graphTargetNames = std::move(targetNames);

  releaseGraph();
  releaseEnvironment();

  // Nothing is bound to anything any more, so whatever is still retired can
  // go — and has to, because Filament asserts on a texture outliving its
  // engine.
  for (const RetiredTexture &retired : _retiredTextures) {
    _engine->destroy(retired.texture);
  }
  _retiredTextures.clear();
  _targetBindings.clear();

  _targets.resize(targetCount);
  for (uint32_t i = 0; i < targetCount; i++) {
    GraphTarget &target = _targets[i];
    const float *row = targets + i * kTargetStride;
    target.name =
        i < _graphTargetNames.size() ? _graphTargetNames[i] : std::string();
    target.width = static_cast<uint32_t>(std::max(0.0f, row[0]));
    target.height = static_cast<uint32_t>(std::max(0.0f, row[1]));
    target.scale = row[2] > 0.0f ? row[2] : 1.0f;
    target.keepsDepth = row[3] != 0.0f;
    target.keepsColour = row[4] != 0.0f;
  }

  _passes.resize(count);
  for (uint32_t i = 0; i < count; i++) {
    GraphPass &pass = _passes[i];
    const float *row = passes + i * kPassStride;
    pass.kind = static_cast<int>(row[0]);
    pass.into = static_cast<int>(row[1]);
    if (pass.into >= static_cast<int>(targetCount)) pass.into = -1;
    // The top bit is hiding, and a pass is not allowed to ask for it: an
    // object switched off should stay off however the graph is written.
    pass.layers = static_cast<uint8_t>(static_cast<int>(row[2])) & kAllLayers;
    pass.clears = row[3] != 0.0f;
    for (int p = 0; p < 4; p++) pass.plane[p] = row[8 + p];
    for (int r = 0; r < 4; r++) pass.reads[r] = static_cast<int>(row[4 + r]);
    pass.effect = static_cast<int>(row[12]);
  }

  // Every effect this graph names, compiled now rather than on the frame that
  // first draws it. A graph is set when the view is configured and changes
  // rarely, so this is the one moment where the whole list is known and no
  // frame is being timed.
  {
    bool warmed = false;
    for (const GraphPass &pass : _passes) {
      if (pass.kind != kPassEffect) continue;
      if (!_effectsWarmed.insert(pass.effect).second) continue;
      warmUp(materialForEffect(pass.effect));
      warmed = true;
    }
    if (warmed) _engine->flush();
  }

  // Motion blur hook: whether this graph blurs, and whether any of its blurs
  // follow objects' own motion. What motion blur remembers and allocates all
  // waits on this, which is what keeps it free for a graph that never asks.
  {
    bool blurs = false;
    bool objects = false;
    for (const GraphPass &pass : _passes) {
      if (pass.kind != kPassEffect || pass.effect != kEffectMotionBlur) continue;
      blurs = true;
      objects = objects || pass.plane[2] >= 0.0f;
    }
    if (blurs && !_motionBlur) {
      _motionBlur = std::make_unique<orblit::MotionBlur>(*_engine);
    }
    if (_motionBlur) _motionBlur->setWanted(blurs, objects);
  }

  // Built here rather than only at the top of the frame, because materials
  // are bound straight after this and a material sampling a target that does
  // not exist yet gets the blank white texture instead. That is not a
  // rendering fault anybody can see the cause of — it is a mirror that is
  // simply white, on the first frame and every frame after, because nothing
  // re-binds it.
  prepareTargets();
}

/// Makes sure every target a pass writes exists at the right size.
///
/// Called at the top of a frame rather than when the graph arrives, because
/// a target that follows the view has no size until the view has one — and
/// the view's size changes on a window drag, which is not when a graph is
/// sent.
void Renderer::prepareTargets() {
  sweepRetiredTextures();

  bool rebuilt = false;
  for (GraphTarget &target : _targets) {
    uint32_t wide = target.width;
    uint32_t tall = target.height;
    if (wide == 0 || tall == 0) {
      wide = static_cast<uint32_t>(std::lround(_width * target.scale));
      tall = static_cast<uint32_t>(std::lround(_height * target.scale));
    }
    wide = std::max(1u, wide);
    tall = std::max(1u, tall);

    if (target.target != nullptr && target.builtWidth == wide &&
        target.builtHeight == tall) {
      continue;
    }

    releaseTarget(target);

    auto builder = RenderTarget::Builder();
    if (target.keepsColour) {
      // Sixteen bits a channel because what a pass draws is linear light
      // that another pass will sample and light with. Eight bits would clip
      // every highlight in a reflection to white.
      target.colour = Texture::Builder()
                          .width(wide)
                          .height(tall)
                          .levels(1)
                          .usage(Texture::Usage::COLOR_ATTACHMENT |
                                 Texture::Usage::SAMPLEABLE)
                          .format(Texture::InternalFormat::RGBA16F)
                          .build(*_engine);
      builder.texture(RenderTarget::AttachmentPoint::COLOR, target.colour);
    }
    if (target.keepsDepth) {
      // Sampleable as well as attachable, so a later pass can read the
      // shape of the scene rather than only its colour. That is the whole
      // difference between an effect that can tint a picture and one that
      // knows what is in front of what — occlusion, bounced light, contact
      // shadows all begin here. It costs nothing when nothing samples it.
      target.depth = Texture::Builder()
                         .width(wide)
                         .height(tall)
                         .levels(1)
                         .usage(Texture::Usage::DEPTH_ATTACHMENT |
                                Texture::Usage::SAMPLEABLE)
                         .format(Texture::InternalFormat::DEPTH32F)
                         .build(*_engine);
      builder.texture(RenderTarget::AttachmentPoint::DEPTH, target.depth);
    }

    // Neither colour nor depth is a target that cannot be drawn into. Left
    // null rather than half-built: a pass writing it is skipped and named,
    // which is a legible failure.
    if (target.colour == nullptr && target.depth == nullptr) continue;

    target.target = builder.build(*_engine);
    target.builtWidth = wide;
    target.builtHeight = tall;

    rebuilt = true;
  }

  // A texture cannot be resized, so a target that follows the view is a new
  // texture every time the window changes — and every material sampling the
  // old one is left pointing at a texture nothing writes to any more. What
  // that looks like is the missing-texture magenta, for ever, because a host
  // showing a scene that does not change never publishes again and nothing
  // asks for the binding a second time.
  //
  // So it is renewed here, by whoever rebuilt it.
  if (rebuilt) rebindTargets();
}

/// Points every material sampler that reads a pass at the texture that pass
/// now draws into.
void Renderer::rebindTargets() {
  const TextureSampler drawn(TextureSampler::MinFilter::LINEAR,
                             TextureSampler::MagFilter::LINEAR,
                             TextureSampler::WrapMode::CLAMP_TO_EDGE);

  for (const TargetBinding &binding : _targetBindings) {
    Texture *texture = targetTextureNamed(binding.target);
    if (texture == nullptr) continue;
    binding.instance->setParameter(binding.parameter.c_str(), texture, drawn);
  }
}

/// The view a pass draws through, made on first use and kept.
/// SMAA's lookup tables, uploaded the first time a weights pass runs.
///
/// Two channels for the area table, because a coverage answer is two numbers —
/// how much of the pixel each side of the edge takes. One for the search
/// table, which holds a distance. Both are read with linear filtering: the
/// index into them is fractional, and point-sampling a coverage table
/// quantises the anti-aliasing it is there to provide.
void Renderer::buildSmaaTables() {
  if (_smaaArea != nullptr) return;

  const SmaaTable area = smaaAreaTable();
  _smaaArea = Texture::Builder()
                  .width(area.width)
                  .height(area.height)
                  .levels(1)
                  .format(Texture::InternalFormat::RG8)
                  .sampler(Texture::Sampler::SAMPLER_2D)
                  .build(*_engine);
  _smaaArea->setImage(
      *_engine, 0,
      Texture::PixelBufferDescriptor(
          area.bytes, area.size,
          Texture::PixelBufferDescriptor::PixelDataFormat::RG,
          Texture::PixelBufferDescriptor::PixelDataType::UBYTE));

  const SmaaTable search = smaaSearchTable();
  _smaaSearch = Texture::Builder()
                    .width(search.width)
                    .height(search.height)
                    .levels(1)
                    .format(Texture::InternalFormat::R8)
                    .sampler(Texture::Sampler::SAMPLER_2D)
                    .build(*_engine);
  _smaaSearch->setImage(
      *_engine, 0,
      Texture::PixelBufferDescriptor(
          search.bytes, search.size,
          Texture::PixelBufferDescriptor::PixelDataFormat::R,
          Texture::PixelBufferDescriptor::PixelDataType::UBYTE));
}

/// One rectangle's four texels, in the coordinates the surface shader reads.
///
/// The conversion from what an artist states to what the integral wants
/// happens here rather than in the shader, because it is the same answer for
/// every fragment the light touches.
void Renderer::packRectangle(const float *p, float *out, bool casting) {
  const float3 colour = {p[0], p[1], p[2]};
  const float lumens = p[3];
  const float3 centre = {p[4], p[5], p[6]};
  float3 normal = {p[7], p[8], p[9]};
  const float falloff = p[10];
  const float width = std::max(p[17], 1e-4f);
  const float height = std::max(p[18], 1e-4f);
  float3 tangent = {p[19], p[20], p[21]};

  // A direction that is not a direction is the commonest thing to be handed,
  // and normalising nothing gives NaN, which spreads to every pixel the light
  // reaches rather than to none of them.
  if (length(normal) < 1e-6f) normal = {0.0f, -1.0f, 0.0f};
  normal = normalize(normal);

  // The tangent only has to be roughly right: it is squared up against the
  // face here. If it was given parallel to the face's normal there is no
  // rectangle to describe, so any perpendicular will do.
  tangent = tangent - normal * dot(tangent, normal);
  if (length(tangent) < 1e-6f) {
    const float3 other =
        std::abs(normal.x) < 0.9f ? float3{1, 0, 0} : float3{0, 1, 0};
    tangent = other - normal * dot(other, normal);
  }
  tangent = normalize(tangent);

  // Crossed this way round so that cross(right, up) — which is what both the
  // integral and the shadow lookup take as the panel's axis — comes out as
  // *minus* the normal: it points from a lit surface back at the panel. The
  // integral wants that sign to keep the front lit and the back dark, and the
  // lookup wants it to tell a surface facing the panel from one edge-on to
  // it. Worth stating outright, because the identity that makes it true —
  // cross(t, cross(t, n)) is minus n — is not obvious at a glance, and both
  // readers of it would silently do the wrong thing if it flipped.
  const float3 up = cross(tangent, normal);

  // Lumens to luminance. A one-sided Lambertian panel of area A emitting a
  // luminous flux F has a luminance of F / (pi * A), and that is the unit
  // Filament's own lights arrive in, so a rectangle and a bulb of the same
  // stated brightness agree. It is also why making a panel larger does not
  // make a room brighter: the same flux is spread over more surface, which is
  // what softens the shadow rather than lifting the exposure.
  const float radiance =
      lumens / (float(M_PI) * std::max(width * height, 1e-6f));

  out[0] = centre.x;
  out[1] = centre.y;
  out[2] = centre.z;
  out[3] = 0.0f;
  out[4] = colour.x * radiance;
  out[5] = colour.y * radiance;
  out[6] = colour.z * radiance;
  // The inverse radius, so the shader multiplies rather than divides. Zero
  // means no window at all, which is a light that reaches as far as it is
  // bright enough to.
  out[7] = falloff > 1e-4f ? 1.0f / falloff : 0.0f;
  out[8] = tangent.x;
  out[9] = tangent.y;
  out[10] = tangent.z;
  out[11] = width;
  out[12] = up.x;
  out[13] = up.y;
  out[14] = up.z;
  out[15] = height;

  // Whether this one casts, and the numbers the lookup needs to turn what
  // the map holds into metres and a penumbra: see packAreaShadowSettings.
  orblit::packAreaShadowSettings(casting, _areaShadowFrame, width, height,
                                out + 16);

  if (casting) {
    // Column major, as the shader's mat4 constructor reads it: four texels,
    // each one a column.
    const filament::math::mat4f &m = _areaShadowMatrix;
    for (int c = 0; c < 4; c++) {
      out[20 + c * 4 + 0] = m[c][0];
      out[20 + c * 4 + 1] = m[c][1];
      out[20 + c * 4 + 2] = m[c][2];
      out[20 + c * 4 + 3] = m[c][3];
    }
  }
}

/// Puts this frame's rectangles on the GPU, and tells the surfaces if how
/// many there are has changed.
void Renderer::uploadRectangles(const float *rectangles, uint32_t count) {
  if (_lightData == nullptr) return;

  const size_t floats = size_t(kAreaLightTexels) * kAreaLightBudget * 4;

  std::vector<float> wanted(floats, 0.0f);
  memcpy(wanted.data(), rectangles,
         size_t(count) * kAreaLightTexels * 4 * sizeof(float));
  // How many, in the first light's spare channel. With no lights the whole
  // texture is zeros, which reads as a count of nought without needing a
  // special case for it.
  wanted[3] = float(count);

  if (wanted == _areaLightsOnGpu) return;
  _areaLightsOnGpu = wanted;

  // Its own copy rather than the vector's storage: the descriptor keeps the
  // pointer until the driver thread performs the upload, and the vector is
  // free to be reassigned before then.
  float *copy = static_cast<float *>(malloc(floats * sizeof(float)));
  memcpy(copy, wanted.data(), floats * sizeof(float));
  // Only the four columns the rectangles use, in the rows below the tables.
  _lightData->setImage(
      *_engine, 0, 0, 64, kAreaLightTexels, kAreaLightBudget,
      Texture::PixelBufferDescriptor(
          copy, floats * sizeof(float),
          Texture::PixelBufferDescriptor::PixelDataFormat::RGBA,
          Texture::PixelBufferDescriptor::PixelDataType::FLOAT,
          [](void *buffer, size_t, void *) { free(buffer); }));
}

/// Draws what the casting rectangle can see, into its own depth map.
///
/// Nothing at all when no rectangle casts, which is the usual answer: the
/// view is not rendered, so the map keeps whatever it held and the surfaces
/// never look at it because their flag is nought.
void Renderer::renderAreaShadow() {
  if (!_areaShadowCasting || _areaShadowView == nullptr) return;
  _renderer->render(_areaShadowView);
}

/// The one rectangle's depth map, and the view that draws it.
///
/// Built on first use rather than at startup, because most scenes have no
/// casting rectangle and a megapixel of depth is not worth reserving against
/// the chance of one.
void Renderer::buildAreaShadow() {
  if (_areaShadow != nullptr) return;

  _areaShadow = Texture::Builder()
                    .width(kAreaShadowSide)
                    .height(kAreaShadowSide)
                    .levels(1)
                    // Depth rather than colour: the scene is drawn with no
                    // shading at all, so there is nothing to keep but how far
                    // away it was. A colour target would mean a material of
                    // its own on every object to write distance into it.
                    .format(Texture::InternalFormat::DEPTH32F)
                    .usage(Texture::Usage::DEPTH_ATTACHMENT |
                           Texture::Usage::SAMPLEABLE)
                    .build(*_engine);

  _areaShadowTarget = RenderTarget::Builder()
                          .texture(RenderTarget::AttachmentPoint::DEPTH,
                                   _areaShadow)
                          .build(*_engine);

  _areaShadowCameraEntity = utils::EntityManager::get().create();
  _areaShadowCamera = _engine->createCamera(_areaShadowCameraEntity);

  _areaShadowView = _engine->createView();
  _areaShadowView->setScene(_scene);
  _areaShadowView->setCamera(_areaShadowCamera);
  _areaShadowView->setRenderTarget(_areaShadowTarget);
  _areaShadowView->setViewport({0, 0, kAreaShadowSide, kAreaShadowSide});
  // Nothing here is looked at, so nothing here is worth computing. Filament
  // still runs the fragment stage for anything that could discard, which is
  // why a masked leaf still cuts its own shape out of the shadow.
  _areaShadowView->setShadowingEnabled(false);
  _areaShadowView->setPostProcessingEnabled(false);
  _areaShadowView->setFrustumCullingEnabled(true);
}

/// Where the casting rectangle stands, as a matrix that turns a point in the
/// world into a place on its depth map.
///
/// A perspective frustum rather than an orthographic one, because a panel is
/// somewhere rather than everywhere: a wall two metres behind a lamp should
/// not be in its map, and a light that fills the room in front of it is what
/// the falloff radius already describes.
bool Renderer::aimAreaShadowAt(const float *p) {
  orblit::AreaShadowFrame frame;
  if (!orblit::frameAreaShadow(float3{p[4], p[5], p[6]},
                              float3{p[7], p[8], p[9]}, p[17], p[18], p[10],
                              frame)) {
    return false;
  }
  _areaShadowFrame = frame;

  // Filament's own projection: the far plane at infinity for drawing, the
  // finite one kept only for culling. A custom matrix with a finite far was
  // used here before, and it works, but it puts the map's depth on a curve
  // that depends on both planes; at infinity it is exactly near / distance,
  // which the surface can turn back into metres with one divide.
  _areaShadowCamera->setProjection(frame.fovDegrees, 1.0, frame.near,
                                   frame.far, Camera::Fov::VERTICAL);
  _areaShadowCamera->lookAt(frame.eye, frame.target, frame.up);

  // What a surface has to be multiplied by to land on the map. Filament keeps
  // the world shifted to the camera for precision, and this matrix is applied
  // to `getUserWorldPosition` — the unshifted one — so it is built from the
  // camera's own unshifted transform.
  //
  // The *rendering* projection, not the culling one, and then the remap
  // Filament applies in every vertex shader: the camera's matrix is the
  // OpenGL one, z from minus one to one, and the depth buffer holds that
  // turned into nought to one and reversed. Leaving the remap out was why
  // this shadow never showed: the surface compared a number near one against
  // a map near nought, and was lit wherever it stood. Found by drawing, per
  // pixel, whether the map held anything and which convention it agreed with
  // — it held the right depths all along.
  //
  // With the remap in place the Panel shadows example darkens 126k of its
  // 1.92M pixels by a tenth or more when the panel is asked to cast, the
  // umbra under an occluder falling to a fifth of the lit floor beside it
  // (31.0 to 6.2 levels of luminance) while the lit floor itself does not
  // move. Without it, nothing changed but the dither.
  _areaShadowMatrix = orblit::depthFromClip() *
                      filament::math::mat4f(_areaShadowCamera->getProjectionMatrix() *
                                            _areaShadowCamera->getViewMatrix());
  return true;
}

/// The two fitted tables, side by side in one texture.
///
/// One texture rather than two because a material's sampler slots are the
/// scarce thing and a tile is free. Thirty-two bit float rather than half:
/// the matrix entries reach into the tens of thousands at the smooth end of
/// the table, which is past what a half can hold, and a table that silently
/// saturates gives a mirror-smooth surface no highlight at all.
void Renderer::buildLtcTables() {
  if (_lightData != nullptr) return;

  constexpr uint32_t kSide = kLtcSide;
  constexpr size_t kTexels = size_t(kSide) * kSide;
  const float *const matrix = ltcMatrixTable();
  const float *const fresnel = ltcFresnelTable();

  // Interleaved row by row, because the two tiles share rows in the texture
  // and are contiguous only in the source arrays.
  const size_t floats = kTexels * 4 * 2;
  float *packed = static_cast<float *>(malloc(floats * sizeof(float)));
  for (uint32_t y = 0; y < kSide; y++) {
    const size_t row = size_t(y) * kSide * 4;
    memcpy(packed + y * kSide * 2 * 4, matrix + row, kSide * 4 * sizeof(float));
    memcpy(packed + y * kSide * 2 * 4 + kSide * 4, fresnel + row,
           kSide * 4 * sizeof(float));
  }

  // Taller on the slim surface: its decalData rows live in this texture's
  // tail (from kSlimDecalRow) rather than in a sampler of their own. The
  // standard surface's height is unchanged from before this existed.
  _lightData = Texture::Builder()
                   .width(kSide * 2)
                   .height(kSide + kAreaLightBudget +
                           (_slimSurface ? orblit::kDecalBudget : 0))
                   .levels(1)
                   .format(Texture::InternalFormat::RGBA32F)
                   .sampler(Texture::Sampler::SAMPLER_2D)
                   .usage(Texture::Usage::SAMPLEABLE |
                          Texture::Usage::UPLOADABLE)
                   .build(*_engine);
  // Freed by the callback rather than after this returns: the descriptor
  // keeps the pointer until the driver thread performs the upload, which is
  // later than here.
  _lightData->setImage(
      *_engine, 0, 0, 0, kSide * 2, kSide,
      Texture::PixelBufferDescriptor(
          packed, floats * sizeof(float),
          Texture::PixelBufferDescriptor::PixelDataFormat::RGBA,
          Texture::PixelBufferDescriptor::PixelDataType::FLOAT,
          [](void *buffer, size_t, void *) { free(buffer); }));

  // The rows the rectangles live in, started empty so that a surface binding
  // this before any light exists reads nought rather than whatever the driver
  // last had there.
  const size_t blankFloats = size_t(kAreaLightTexels) * kAreaLightBudget * 4;
  float *blank = static_cast<float *>(calloc(blankFloats, sizeof(float)));
  _lightData->setImage(
      *_engine, 0, 0, kSide, kAreaLightTexels, kAreaLightBudget,
      Texture::PixelBufferDescriptor(
          blank, blankFloats * sizeof(float),
          Texture::PixelBufferDescriptor::PixelDataFormat::RGBA,
          Texture::PixelBufferDescriptor::PixelDataType::FLOAT,
          [](void *buffer, size_t, void *) { free(buffer); }));
}
}  // namespace orblit
