#include "OrblitRendererInternal.h"

// Reflection probes, and the irradiance field they cannot cover.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

/// Takes one probe's photograph of the scene and filters it into reflections.
///
/// Six renders through a ninety-degree camera, one per face of a cube, and
/// then a filter that blurs the result by roughness so that a matte surface
/// and a mirror can sample the same texture at different levels. Expensive,
/// and deliberately not on the frame path: this runs when a probe is first
/// seen and when its version changes, and at no other time.
void Renderer::capture(Probe &probe, uint8_t layers) {
  using namespace filament;

  const uint32_t side = std::clamp(probe.resolution, 16u, 1024u);
  // A full chain, because the filter writes every level of it and the
  // roughest levels are what a matte surface reads.
  const uint8_t levels = uint8_t(std::floor(std::log2(float(side)))) + 1;

  if (probe.captured != nullptr) {
    _engine->destroy(probe.captured);
    probe.captured = nullptr;
  }
  probe.captured = Texture::Builder()
                       .width(side)
                       .height(side)
                       .levels(levels)
                       .format(Texture::InternalFormat::RGBA16F)
                       .sampler(Texture::Sampler::SAMPLER_CUBEMAP)
                       // Mipmappable as well: the filter builds the chain
                       // down from the captured faces before it convolves
                       // them, and refuses a texture it cannot do that to.
                       .usage(Texture::Usage::COLOR_ATTACHMENT |
                              Texture::Usage::SAMPLEABLE |
                              Texture::Usage::GEN_MIPMAPPABLE)
                       .build(*_engine);
  if (probe.captured == nullptr) return;

  // Its own view and camera, kept off the scene's: pointing the scene's
  // camera six ways and putting it back is the kind of thing that works until
  // something reads it in between. Built once and reused.
  if (_captureView == nullptr) {
    _captureView = _engine->createView();
    _captureCamera =
        _engine->createCamera(utils::EntityManager::get().create());
  }
  View *view = _captureView;
  Camera *camera = _captureCamera;
  view->setScene(_scene);
  view->setCamera(camera);
  view->setViewport({0, 0, side, side});
  view->setVisibleLayers(0xFF, layers);
  // No post-processing on a capture. Tone mapping turns light into pixels,
  // and what a reflection has to hold is light — mapped once here and again
  // when the frame it ends up in is drawn would darken every reflection in
  // the scene.
  view->setPostProcessingEnabled(false);
  camera->setProjection(90.0, 1.0, 0.05, 1000.0, Camera::Fov::VERTICAL);
  camera->setExposure(1.0f);

  // The six directions, in the order Filament's cubemap faces run, with the
  // up vector each one needs to sit the right way round against its
  // neighbours.
  const struct {
    Texture::CubemapFace face;
    math::float3 forward;
    math::float3 up;
  } faces[6] = {
      {Texture::CubemapFace::POSITIVE_X, {1, 0, 0}, {0, -1, 0}},
      {Texture::CubemapFace::NEGATIVE_X, {-1, 0, 0}, {0, -1, 0}},
      {Texture::CubemapFace::POSITIVE_Y, {0, 1, 0}, {0, 0, 1}},
      {Texture::CubemapFace::NEGATIVE_Y, {0, -1, 0}, {0, 0, -1}},
      {Texture::CubemapFace::POSITIVE_Z, {0, 0, 1}, {0, -1, 0}},
      {Texture::CubemapFace::NEGATIVE_Z, {0, 0, -1}, {0, -1, 0}},
  };

  if (probe.depth != nullptr) {
    _engine->destroy(probe.depth);
    probe.depth = nullptr;
  }
  probe.depth = Texture::Builder()
                    .width(side)
                    .height(side)
                    .levels(1)
                    .format(Texture::InternalFormat::DEPTH32F)
                    .usage(Texture::Usage::DEPTH_ATTACHMENT)
                    .build(*_engine);

  // The textures have to exist on the driver before anything is drawn into
  // them: Filament records rather than performs, and a target built in the
  // same frame as the render is built after it.
  _engine->flushAndWait();

  for (int i = 0; i < 6; i++) {
    if (probe.faces[i] != nullptr) _engine->destroy(probe.faces[i]);
    probe.faces[i] = RenderTarget::Builder()
                         .texture(RenderTarget::AttachmentPoint::COLOR,
                                  probe.captured)
                         .face(RenderTarget::AttachmentPoint::COLOR,
                               faces[i].face)
                         .texture(RenderTarget::AttachmentPoint::DEPTH,
                                  probe.depth)
                         .build(*_engine);
    if (probe.faces[i] == nullptr) continue;
    camera->lookAt(probe.position, probe.position + faces[i].forward,
                   faces[i].up);
    view->setRenderTarget(probe.faces[i]);
    _engine->flushAndWait();
    _renderer->render(view);
  }

  // The filter reads the cube, so the faces have to be on it before it runs.
  // Filament records rather than performs, and the recorded draws above have
  // not happened yet.
  _engine->flushAndWait();

  // Built once and kept: the filter compiles its own materials and holds a
  // kernel texture, so one per renderer rather than one per capture.
  // The context may already exist, made by an environment picture's filter.
  if (_prefilter == nullptr) _prefilter = new IBLPrefilterContext(*_engine);
  if (_specularFilter == nullptr) {
    _specularFilter = new IBLPrefilterContext::SpecularFilter(*_prefilter);
  }
  if (probe.filtered != nullptr) {
    _engine->destroy(probe.filtered);
    probe.filtered = nullptr;
  }
  // The blurred chain a rough surface samples, convolved from the sharp
  // capture. This is the whole reason a probe can be taken while the scene
  // runs rather than baked by a tool beforehand.
  probe.filtered = (*_specularFilter)(probe.captured);

  if (probe.light != nullptr) {
    _engine->destroy(probe.light);
    probe.light = nullptr;
  }
  if (probe.filtered != nullptr) {
    // Reflections only. Filament works the diffuse out of the roughest level
    // of the same chain, so a captured probe lights matte surfaces without
    // anybody baking harmonics for it — which is the difference between a
    // probe a scene can take of itself and one a tool has to prepare.
    // Intensity one, and that is not an oversight. A baked environment is
    // stored relative to some reference and `intensity` is what turns it into
    // lux — thirty thousand for a sunny day. A probe is not stored relative
    // to anything: it is the scene's own light, rendered with the exposure
    // held at one, so it arrives already in the units the rest of the frame
    // is in. Scaling it again is the same light counted twice.
    probe.light = IndirectLight::Builder()
                      .reflections(probe.filtered)
                      .intensity(probe.intensity)
                      .build(*_engine);
  }
}

void Renderer::applyProbes(const int64_t *keys, const float *params, uint32_t count) {
  if (_disposed) return;

  const uint64_t generation = ++_lightGeneration;
  for (uint32_t i = 0; i < count; i++) {
    const float *p = params + i * kProbeStride;
    Probe &probe = _probes[keys[i]];
    probe.seen = generation;
    probe.position = {p[0], p[1], p[2]};
    probe.radius = p[3];

    const uint32_t resolution = uint32_t(std::max(p[4], 16.0f));
    const int32_t version = int32_t(p[5]);
    const uint8_t layers = uint8_t(int32_t(p[6]) & 0xFF);
    probe.intensity = p[7];
    if (probe.captured_at != version || probe.resolution != resolution) {
      probe.resolution = resolution;
      probe.capture_layers = layers;
      probe.wants_capture = true;
      probe.captured_at = version;
    }
  }

  for (auto it = _probes.begin(); it != _probes.end();) {
    if (it->second.seen == generation) {
      ++it;
      continue;
    }
    releaseProbe(it->second);
    it = _probes.erase(it);
  }
}

void Renderer::releaseProbe(Probe &probe) {
  for (int i = 0; i < 6; i++) {
    if (probe.faces[i] != nullptr) _engine->destroy(probe.faces[i]);
  }
  if (probe.depth != nullptr) _engine->destroy(probe.depth);
  if (probe.light != nullptr) _engine->destroy(probe.light);
  if (probe.filtered != nullptr) _engine->destroy(probe.filtered);
  if (probe.captured != nullptr) _engine->destroy(probe.captured);
  probe = Probe{};
}

/// Takes the photographs any probe is still owed.
///
/// The scene's own indirect light is put back to what it would be without any
/// probe for the duration, so that what a probe captures does not depend on
/// which probe happened to be lighting the room when it was taken. Without
/// that a re-capture photographs the room lit by the previous capture, and
/// each one is a little brighter than the last.
void Renderer::captureOwedProbes() {
  bool any = false;

  for (auto &entry : _probes) any = any || entry.second.wants_capture;
  if (!any) return;

  IndirectLight *restore = _activeProbe != 0 && _probes.count(_activeProbe)
                               ? _probes[_activeProbe].light
                               : nullptr;
  IndirectLight *base =
      _environmentLight != nullptr ? _environmentLight : _ambient;
  if (restore != nullptr) _scene->setIndirectLight(base);

  for (auto &entry : _probes) {
    if (!entry.second.wants_capture) continue;
    capture(entry.second, entry.second.capture_layers);
    entry.second.wants_capture = false;
  }

  if (restore != nullptr) _scene->setIndirectLight(restore);
}

/// Puts the probe the camera is standing in charge of lighting the scene.
///
/// Called every frame because the camera moves every frame; it costs a walk
/// over a handful of probes and sets nothing unless the answer changed.
void Renderer::chooseProbe() {
  if (_probes.empty()) {
    if (_activeProbe != 0) {
      _activeProbe = 0;
      // Back to whatever the scene had before a probe took over.
      _scene->setIndirectLight(_environmentLight != nullptr ? _environmentLight
                                                            : _ambient);
    }
    return;
  }

  const filament::math::float3 eye =
      filament::math::float3(_view->getCamera().getPosition());
  int64_t wanted = 0;
  float best = 0.0f;
  for (const auto &entry : _probes) {
    const Probe &probe = entry.second;
    if (probe.light == nullptr || probe.radius <= 0.0f) continue;
    const filament::math::float3 away = probe.position - eye;
    const float distance = std::sqrt(dot(away, away));
    if (distance > probe.radius) continue;
    // Nearest middle wins where two overlap, so a doorway joins wherever
    // their centres say rather than wherever the loop happened to look first.
    const float closeness = 1.0f - distance / probe.radius;
    if (wanted == 0 || closeness > best) {
      wanted = entry.first;
      best = closeness;
    }
  }

  if (wanted == _activeProbe) return;
  _activeProbe = wanted;
  if (wanted == 0) {
    _scene->setIndirectLight(_environmentLight != nullptr ? _environmentLight
                                                          : _ambient);
    return;
  }
  _scene->setIndirectLight(_probes[wanted].light);
}

/// Takes the field's numbers, and builds its atlases when their size changes.
void Renderer::applyField(const float *params, const std::string &from) {
  if (_disposed || params == nullptr) return;
  memcpy(_fieldParams, params, sizeof(_fieldParams));
  _fieldFrom = from;

  const uint32_t wanted = std::min(
      uint32_t(std::max(0.0f, params[7])) * uint32_t(std::max(0.0f, params[8])) *
          uint32_t(std::max(0.0f, params[9])),
      kFieldMaxProbes);

  if (_slimSurface) {
    // No fieldAtlas sampler to spare below feature level 3 — see
    // lit_slim.mat's header. Neither atlas is built, so runField and
    // bindFieldEverywhere stay the no-ops their own _fieldProbes == 0
    // guards already make them; only a scene that actually turns a field on
    // is told why it stays dark.
    if (wanted > 0 && params[0] > 0.0f) {
      _surfaceNotes["field"] =
          "The irradiance field does not light this scene: the slim "
          "surface, used because this device is below Filament's third "
          "feature level, has no sampler to spare for its atlas.";
    } else {
      _surfaceNotes.erase("field");
    }
    _fieldProbes = 0;
    return;
  }

  if (wanted == _fieldProbes) return;

  releaseField();
  _fieldProbes = wanted;
  if (wanted == 0) return;

  const uint32_t rows = (wanted + kFieldTilesPerRow - 1) / kFieldTilesPerRow;
  const uint32_t wide = kFieldTilesPerRow * kFieldTile;
  const uint32_t tall = rows * kFieldTile;

  for (int i = 0; i < 2; i++) {
    _fieldAtlas[i] = Texture::Builder()
                         .width(wide)
                         .height(tall)
                         .levels(1)
                         .format(Texture::InternalFormat::RGBA16F)
                         // Uploadable as well, only so it can be cleared
                         // once at the start. Filament refuses setImage on a
                         // texture without it.
                         .usage(Texture::Usage::COLOR_ATTACHMENT |
                                Texture::Usage::SAMPLEABLE |
                                Texture::Usage::UPLOADABLE)
                         .build(*_engine);
    _fieldTargets[i] = RenderTarget::Builder()
                           .texture(RenderTarget::AttachmentPoint::COLOR,
                                    _fieldAtlas[i])
                           .build(*_engine);
    // Cleared, because a texture Filament allocates holds whatever the
    // driver last had there. A surface reads this before the first pass has
    // written it, and what it found was a constant that looked like light —
    // this room came back green, a colour nowhere in it.
    const size_t floats = size_t(wide) * tall * 4;
    float *blank = static_cast<float *>(calloc(floats, sizeof(float)));
    _fieldAtlas[i]->setImage(
        *_engine, 0, 0, 0, wide, tall,
        Texture::PixelBufferDescriptor(
            blank, floats * sizeof(float),
            Texture::PixelBufferDescriptor::PixelDataFormat::RGBA,
            Texture::PixelBufferDescriptor::PixelDataType::FLOAT,
            [](void *buffer, size_t, void *) { free(buffer); }));
  }
  _fieldFront = 0;
  // Nothing to blend against on the first frame, so the first pass takes
  // what it finds rather than mixing it with an atlas that has never been
  // written. Otherwise a field fades in from whatever the allocation held.
  _fieldHasHistory = false;
}

/// Builds the triangle the field is drawn with, once.
bool Renderer::buildField() {
  if (_fieldScene != nullptr) return true;

  _fieldMaterial = materialFrom(Package::irradiance);
  if (_fieldMaterial == nullptr) return false;

  const ScreenTriangle triangle = makeScreenTriangle(*_engine);
  _fieldVertices = triangle.vertices;
  _fieldIndices = triangle.indices;
  _fieldInstance = _fieldMaterial->createInstance();
  _fieldEntity = utils::EntityManager::get().create();
  buildScreenRenderable(*_engine, _fieldEntity, _fieldInstance, triangle);

  _fieldScene = _engine->createScene();
  _fieldScene->addEntity(_fieldEntity);
  _fieldView = _engine->createView();
  _fieldView->setScene(_fieldScene);
  _fieldCamera = _engine->createCamera(utils::EntityManager::get().create());
  _fieldView->setCamera(_fieldCamera);
  _fieldView->setPostProcessingEnabled(false);
  return true;
}

/// Adds this frame's light to the field.
///
/// Runs after the scene has been drawn, because what it reads is the picture
/// the scene just made. The atlas it writes is therefore one frame behind the
/// surfaces that sample it, which is what every temporal method trades and is
/// invisible at anything above a few frames a second.
void Renderer::runField() {
  if (_fieldProbes == 0 || _fieldParams[0] <= 0.0f) return;
  if (!buildField()) return;

  // The picture to read. A field that names a target which does not exist
  // stays dark rather than sampling whatever was bound last.
  GraphTarget *source = nullptr;
  for (auto &target : _targets) {
    if (target.name == _fieldFrom && target.colour != nullptr &&
        target.depth != nullptr) {
      source = &target;
      break;
    }
  }
  if (source == nullptr) {
    // Said rather than left dark. A field whose target does not exist looks
    // exactly like a field that is not working, and the difference is a name
    // in a graph.
    _assetNotes["field"] = orblit::format("The irradiance field fills itself from a target "
                         "called \"%s\", which this graph has no colour and "
                         "depth for. No light is reaching it.",
                         _fieldFrom.c_str());
    return;
  }
  _assetNotes.erase("field");

  const int back = 1 - _fieldFront;
  const TextureSampler smooth(TextureSampler::MinFilter::LINEAR,
                              TextureSampler::MagFilter::LINEAR,
                              TextureSampler::WrapMode::CLAMP_TO_EDGE);
  const TextureSampler exact(TextureSampler::MinFilter::NEAREST,
                             TextureSampler::MagFilter::NEAREST,
                             TextureSampler::WrapMode::CLAMP_TO_EDGE);

  _fieldInstance->setParameter("source", source->colour, smooth);
  _fieldInstance->setParameter("depth", source->depth, exact);
  _fieldInstance->setParameter("history", _fieldAtlas[_fieldFront], exact);
  _fieldInstance->setParameter(
      "origin", float3{_fieldParams[1], _fieldParams[2], _fieldParams[3]});
  _fieldInstance->setParameter(
      "spacing", float3{_fieldParams[4], _fieldParams[5], _fieldParams[6]});
  _fieldInstance->setParameter(
      "counts", float3{_fieldParams[7], _fieldParams[8], _fieldParams[9]});
  _fieldInstance->setParameter("tilesPerRow", float(kFieldTilesPerRow));

  const uint32_t rows =
      (_fieldProbes + kFieldTilesPerRow - 1) / kFieldTilesPerRow;
  const float wide = float(kFieldTilesPerRow * kFieldTile);
  const float tall = float(rows * kFieldTile);
  _fieldInstance->setParameter("atlasSize", float2{wide, tall});

  const Camera &scene = _view->getCamera();
  _fieldInstance->setParameter(
      "clipFromWorld",
      mat4f(scene.getProjectionMatrix() * scene.getViewMatrix()));
  _fieldInstance->setParameter("near", float(scene.getNear()));
  const math::mat4 projection = scene.getProjectionMatrix();
  _fieldInstance->setParameter("tangents",
                               float2{float(1.0 / projection[0][0]),
                                      float(1.0 / projection[1][1])});
  _fieldInstance->setParameter("eye", float3(scene.getPosition()));
  _fieldInstance->setParameter("retention", _fieldParams[11]);
  _fieldInstance->setParameter("damping", kFieldDamping);
  _fieldInstance->setParameter("hasHistory", _fieldHasHistory ? 1.0f : 0.0f);

  _fieldCamera->setExposure(1.0f);
  _fieldView->setRenderTarget(_fieldTargets[back]);
  _fieldView->setViewport({0, 0, uint32_t(wide), uint32_t(tall)});
  _renderer->render(_fieldView);

  _fieldFront = back;
  _fieldHasHistory = true;
}

void Renderer::releaseField() {
  for (int i = 0; i < 2; i++) {
    if (_fieldTargets[i] != nullptr) {
      _engine->destroy(_fieldTargets[i]);
      _fieldTargets[i] = nullptr;
    }
    if (_fieldAtlas[i] != nullptr) {
      _engine->destroy(_fieldAtlas[i]);
      _fieldAtlas[i] = nullptr;
    }
  }
  _fieldProbes = 0;
  _fieldHasHistory = false;
}
}  // namespace orblit
