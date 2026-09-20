#include "OrblitRendererInternal.h"

// Effect passes: the material each runs, the triangle it draws, and what
// it reads and writes.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

/// The compiled material one effect runs, built the first time it is asked
/// for. An effect nobody has a material for draws nothing rather than
/// drawing wrongly.
filament::Material *Renderer::materialForEffect(int effect) {
  auto found = _effectMaterials.find(effect);
  if (found != _effectMaterials.end()) return found->second;

  const uint8_t *package = nullptr;
  size_t length = 0;
  if (!effectPackage(effect, &package, &length)) {
    if (effect == kEffectMotionBlur) {
      // Motion blur hook: the gather is this pass's own material, and its
      // bytes live with the rest of motion blur.
      package = orblit::MotionBlur::gatherPackage();
      length = orblit::MotionBlur::gatherPackageSize();
    } else if (!orblit::screenEffectPackage(effect, &package, &length)) {
      // Hook (screen effects): god rays and distortion keep their compiled
      // materials in ScreenEffects.cpp. Anything else has no material, and an
      // effect with no material draws nothing rather than drawing wrongly.
      _effectMaterials[effect] = nullptr;
      return nullptr;
    }
  }

  Material *built = Material::Builder().package(package, length).build(*_engine);
  _effectMaterials[effect] = built;
  return built;
}

/// Builds the one triangle an effect pass draws, and dresses it.
bool Renderer::buildEffect(GraphPass &pass) {
  if (pass.effectScene != nullptr) return true;

  Material *material = materialForEffect(pass.effect);
  if (material == nullptr) return false;

  const ScreenTriangle triangle = makeScreenTriangle(*_engine);
  pass.effectMaterial = material->createInstance();
  pass.effectEntity = utils::EntityManager::get().create();
  buildScreenRenderable(*_engine, pass.effectEntity, pass.effectMaterial,
                        triangle);

  // Its own scene, holding nothing else. The world's scene would put the
  // whole landscape behind a triangle covering the screen.
  pass.effectScene = _engine->createScene();
  pass.effectScene->addEntity(pass.effectEntity);

  // Held on to, because building a renderable out of them does not hand them
  // over: they stay the pass's to give back, exactly like the scene above.
  pass.effectVertices = triangle.vertices;
  pass.effectIndices = triangle.indices;
  return true;
}

/// Gives a view the display side of the scene's post-processing.
///
/// Only the part that turns finished linear light into a picture: the tone
/// mapper and the grade, which live together in Filament's ColorGrading, plus
/// the dithering that stops a smooth gradient banding once it is eight bits.
///
/// Deliberately not bloom, depth of field or anti-aliasing. Those read the
/// scene's own depth and history, and this view has neither — it is one
/// triangle holding a photograph of the scene. Running them here would be
/// running them on the wrong image; they belong to the pass that drew the
/// world.
void Renderer::applyPostTo(View *view) {
  if (_colorGrading != nullptr) view->setColorGrading(_colorGrading);
  view->setDithering(_view->getDithering());
  view->setAntiAliasing(AntiAliasing::NONE);
}

/// Runs one effect pass: the image it reads, over the target it writes.
void Renderer::runEffect(GraphPass &pass, GraphTarget *into) {
  if (!buildEffect(pass)) return;

  // What it sharpens. A pass that names no readable source has nothing to do,
  // and doing it anyway would sample whatever was in the sampler last.
  GraphTarget *from = nullptr;
  for (int r = 0; r < 4; r++) {
    if (pass.reads[r] < 0) continue;
    GraphTarget &candidate = _targets[pass.reads[r]];
    if (candidate.colour != nullptr) {
      from = &candidate;
      break;
    }
  }
  if (from == nullptr) return;

  const TextureSampler smooth(TextureSampler::MinFilter::LINEAR,
                              TextureSampler::MagFilter::LINEAR);
  // Every effect but the weights one calls its input `source`; that one calls
  // it `edges`, and setting a parameter a material does not declare is a
  // Filament precondition, which ends the process rather than the frame.
  if (pass.effect != kEffectSmaaWeights) {
    pass.effectMaterial->setParameter("source", from->colour, smooth);
  }

  // What each effect needs beyond the image. The first of the plane's four
  // numbers is the effect's one dial — a reflection uses those for its
  // mirror and an effect has no mirror.
  const float dial = pass.plane[0];
  const float wide = float(from->builtWidth);
  const float tall = float(from->builtHeight);
  switch (pass.effect) {
    case kEffectSharpen:
      pass.effectMaterial->setParameter("amount", dial > 0.0f ? dial : 0.6f);
      break;
    case kEffectSmaaEdges:
      pass.effectMaterial->setParameter(
          "step", filament::math::float2{1.0f / wide, 1.0f / tall});
      pass.effectMaterial->setParameter("threshold", dial > 0.0f ? dial : 0.1f);
      break;
    case kEffectSmaaWeights: {
      buildSmaaTables();
      const TextureSampler tables(TextureSampler::MinFilter::LINEAR,
                                  TextureSampler::MagFilter::LINEAR);
      // The edges are what this pass reads; `source` above already bound them.
      pass.effectMaterial->setParameter("edges", from->colour, smooth);
      pass.effectMaterial->setParameter("area", _smaaArea, tables);
      pass.effectMaterial->setParameter("search", _smaaSearch, tables);
      pass.effectMaterial->setParameter(
          "step", filament::math::float2{1.0f / wide, 1.0f / tall});
      pass.effectMaterial->setParameter("reach", dial > 0.0f ? dial : 16.0f);
      break;
    }
    case kEffectSmaaBlend: {
      // Two inputs, and the order is the graph's to state: the picture first,
      // the weights second. A blend given them the other way round mixes the
      // weights together and outputs something that looks like a fault in the
      // renderer rather than a mistake in the graph.
      GraphTarget *weights = nullptr;
      for (int r = 1; r < 4; r++) {
        if (pass.reads[r] < 0) continue;
        if (_targets[pass.reads[r]].colour == nullptr) continue;
        weights = &_targets[pass.reads[r]];
        break;
      }
      if (weights == nullptr) return;
      pass.effectMaterial->setParameter("weights", weights->colour, smooth);
      pass.effectMaterial->setParameter(
          "step", filament::math::float2{1.0f / wide, 1.0f / tall});
      break;
    }
    case kEffectBounce: {
      // Depth as well as colour, from the same target. A graph names that
      // target once and gets both, because asking a host to list the depth
      // of a thing it has already listed is a way of getting the two out of
      // step.
      if (from->depth == nullptr) return;
      // Nearest, and it matters: a linear tap between two depths is a
      // distance at which nothing stands, and the march would find a surface
      // in mid-air at every silhouette.
      const TextureSampler exact(TextureSampler::MinFilter::NEAREST,
                                 TextureSampler::MagFilter::NEAREST,
                                 TextureSampler::WrapMode::CLAMP_TO_EDGE);
      pass.effectMaterial->setParameter("depth", from->depth, exact);
      pass.effectMaterial->setParameter(
          "step", filament::math::float2{1.0f / wide, 1.0f / tall});

      // The scene's camera, not this pass's. An effect draws through a camera
      // of its own — that is what puts a triangle over the whole screen — so
      // the projection that made the depth has to be handed over rather than
      // read from the frame.
      const Camera &scene = _view->getCamera();
      const filament::math::mat4 clipFromView = scene.getProjectionMatrix();
      pass.effectMaterial->setParameter("near", float(scene.getNear()));
      // The half field of view as tangents, which turn a place on the screen
      // and a distance into a position. Read off the projection so an
      // orthographic or an off-centre camera cannot disagree with it.
      pass.effectMaterial->setParameter(
          "tangents",
          filament::math::float2{float(1.0 / clipFromView[0][0]),
                                 float(1.0 / clipFromView[1][1])});

      pass.effectMaterial->setParameter("radius", dial > 0.0f ? dial : 1.5f);
      pass.effectMaterial->setParameter("intensity", pass.plane[1] > 0.0f
                                                         ? pass.plane[1]
                                                         : 1.0f);
      pass.effectMaterial->setParameter(
          "thickness", pass.plane[2] > 0.0f ? pass.plane[2] : 0.35f);
      // Four slices of eight steps is the shape that holds up while staying
      // affordable; the shader's loops are bounded at eight and sixteen.
      const int slices = pass.plane[3] > 0.0f ? int(pass.plane[3]) : 4;
      pass.effectMaterial->setParameter("slices", int32_t(std::clamp(slices, 1, 8)));
      pass.effectMaterial->setParameter("steps", int32_t(8));
      break;
    }
    case orblit::kEffectGodRays:
    case orblit::kEffectDistortion: {
      // Hook (screen effects). The colour is `from`, as for every effect;
      // the depth is the first read that kept one, which is what lets a
      // distortion bend the god rays' output by the world's depth. The
      // scene's camera, not this pass's, for the same reason as the bounce.
      Texture *depth = nullptr;
      for (int r = 0; r < 4 && depth == nullptr; r++) {
        if (pass.reads[r] >= 0) depth = _targets[pass.reads[r]].depth;
      }
      if (depth == nullptr) return;
      if (pass.effect == orblit::kEffectGodRays) {
        _screenEffects.applyGodRays(*pass.effectMaterial, _view->getCamera(),
                                    uint32_t(wide), uint32_t(tall), depth);
      } else {
        _screenEffects.applyDistortion(*pass.effectMaterial,
                                       _view->getCamera(), uint32_t(wide),
                                       uint32_t(tall), depth);
      }
      break;
    }
    case kEffectMotionBlur:
      // Motion blur hook: the velocity, resolve and tile passes run here,
      // and the gather — this pass's own material — is dressed for the draw
      // below, which is what keeps the frame's tone mapping on it.
      if (_motionBlur) {
        _motionBlur->prepare(*_renderer, _view->getCamera(), from->colour,
                             from->depth, from->builtWidth, from->builtHeight,
                             pass.plane, *pass.effectMaterial);
      }
      break;
    default:
      break;
  }

  View *view = viewForPass(pass);
  view->setScene(pass.effectScene);

  // Neutral exposure, and it is not cosmetic. Filament scales what an unlit
  // material writes by the camera's exposure, which is right for a surface
  // being photographed and wrong for a pass whose output is *data*: an edge
  // written as one lands in the target as a thousandth, and the pass that
  // reads it back finds nothing there. It looked correct on screen only
  // because tone mapping was undoing the same scale on the way out.
  pass.camera->setExposure(1.0f);

  if (into != nullptr) {
    view->setRenderTarget(into->target);
    view->setViewport({0, 0, into->builtWidth, into->builtHeight});
    // Another pass will sample this, so it stays linear light. Tone-mapping
    // it here would bake a display curve into something still being worked
    // on, and the next effect in the chain would sharpen a picture of a
    // picture.
    view->setPostProcessingEnabled(false);
  } else {
    // The frame, which is the end of the chain and the only place a display
    // curve belongs. Post is *on* here, and that is what carries tone
    // mapping, grading and the rest across an effect chain — without it a
    // scene that went through one came out cooler and darker than the same
    // scene drawn straight to the screen, because the linear light was never
    // converted for a display.
    view->setRenderTarget(nullptr);
    view->setViewport({0, 0, _width, _height});
    view->setPostProcessingEnabled(true);
    applyPostTo(view);
  }
  _renderer->render(view);
}

View *Renderer::viewForPass(GraphPass &pass) {
  if (pass.view != nullptr) return pass.view;

  pass.view = _engine->createView();
  pass.view->setScene(_scene);

  pass.cameraEntity = utils::EntityManager::get().create();
  pass.camera = _engine->createCamera(pass.cameraEntity);
  pass.view->setCamera(pass.camera);

  // No post on an off-screen pass. What another pass will sample has to stay
  // linear light: tone-mapping it here would bake a display curve into a
  // reflection and then light the scene with it.
  pass.view->setPostProcessingEnabled(false);
  return pass.view;
}

/// Points a pass's camera where it should be looking.
void Renderer::aimPass(GraphPass &pass, uint32_t wide, uint32_t tall) {
  const double aspect = double(wide) / double(std::max(1u, tall));
  mat4 model = _camera->getModelMatrix();

  if (pass.kind == kPassReflection) {
    model = mat4(reflectionAbout(pass.plane)) * model;
    // Mirroring the world turns every triangle inside out, so what was the
    // front face is now the back. Without this a reflection is a view of the
    // insides of everything in it.
    pass.view->setFrontFaceWindingInverted(true);
  } else {
    pass.view->setFrontFaceWindingInverted(false);
  }

  pass.camera->setModelMatrix(mat4f(model));
  pass.camera->setProjection(_fieldOfView > 0 ? _fieldOfView : 50.0, aspect,
                             0.1, 1000.0, fovAxisFor(aspect));
  pass.camera->setExposure(_camera->getAperture(), _camera->getShutterSpeed(),
                           _camera->getSensitivity());
}

/// Gives up a target's render target now and its textures later.
///
/// Later because a material may be sampling one. A window being dragged
/// rebuilds every target that follows the view, and the materials pointing at
/// them are not re-bound until the host publishes again — so destroying the
/// texture here would leave the driver reading freed memory for however many
/// frames that takes. The same pattern the spent material instances use, and
/// for the same reason.
void Renderer::releaseTarget(GraphTarget &target) {
  if (_engine == nullptr) return;
  if (target.target != nullptr) {
    // Nothing samples a render target, only the textures behind it, so this
    // one can go immediately.
    _engine->destroy(target.target);
    target.target = nullptr;
  }
  if (target.colour != nullptr) {
    _retiredTextures.push_back({target.colour, _materialGeneration});
    target.colour = nullptr;
  }
  if (target.depth != nullptr) {
    _retiredTextures.push_back({target.depth, _materialGeneration});
    target.depth = nullptr;
  }
  target.builtWidth = 0;
  target.builtHeight = 0;
}

/// Destroys the textures nothing can still be bound to.
///
/// A texture retired before the last publish has had a publish to re-bind
/// every material that was sampling it, so nothing points at it any more.
/// One retired *during* the current publish has not, and waits.
void Renderer::sweepRetiredTextures() {
  if (_engine == nullptr) return;
  auto it = _retiredTextures.begin();
  while (it != _retiredTextures.end()) {
    if (it->afterGeneration < _materialGeneration) {
      _engine->destroy(it->texture);
      it = _retiredTextures.erase(it);
    } else {
      ++it;
    }
  }
}

/// Gives back everything the graph was holding.
///
/// Per pass that is the view and camera it drew through, and the triangle an
/// effect pass drew — its own scene, entity, material instance and buffers.
/// All of it is built on first use and all of it belongs to the pass, so all
/// of it goes when the pass does: _passes.clear() below is the last reference
/// to any of it, and whatever is not given back here can never be given back
/// at all. That leak stayed invisible until the engine went down, and then
/// arrived as Filament refusing to destroy a material with instances still
/// alive — one instance for every graph the renderer had been given.
///
/// The compiled Material an effect runs is deliberately not here: that is
/// shared between passes, cached in _effectMaterials across graph changes, and
/// given back in dispose.
void Renderer::releaseGraph() {
  if (_engine == nullptr) {
    _passes.clear();
    _targets.clear();
    return;
  }

  for (GraphPass &pass : _passes) {
    if (pass.view != nullptr) {
      _engine->destroy(pass.view);
      pass.view = nullptr;
    }
    if (!pass.cameraEntity.isNull()) {
      _engine->destroyCameraComponent(pass.cameraEntity);
      utils::EntityManager::get().destroy(pass.cameraEntity);
      pass.cameraEntity = utils::Entity();
      pass.camera = nullptr;
    }
    // The renderable before anything it was drawn with: Filament refuses to
    // destroy a material instance or a buffer that something still points at.
    if (!pass.effectEntity.isNull()) {
      _engine->destroy(pass.effectEntity);
      utils::EntityManager::get().destroy(pass.effectEntity);
      pass.effectEntity = utils::Entity();
    }
    if (pass.effectMaterial != nullptr) {
      _engine->destroy(pass.effectMaterial);
      pass.effectMaterial = nullptr;
    }
    if (pass.effectVertices != nullptr) {
      _engine->destroy(pass.effectVertices);
      pass.effectVertices = nullptr;
    }
    if (pass.effectIndices != nullptr) {
      _engine->destroy(pass.effectIndices);
      pass.effectIndices = nullptr;
    }
    // Last, because it is what held the entity above.
    if (pass.effectScene != nullptr) {
      _engine->destroy(pass.effectScene);
      pass.effectScene = nullptr;
    }
  }
  for (GraphTarget &target : _targets) releaseTarget(target);

  _passes.clear();
  _targets.clear();
}

/// The texture a pass drew, by the name the graph gave it.
Texture *Renderer::targetTextureNamed(const std::string &name) {
  for (GraphTarget &target : _targets) {
    if (target.name == name) return target.colour;
  }
  return nullptr;
}
}  // namespace orblit
