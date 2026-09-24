#include "OrblitRendererInternal.h"

// A renderer's life: made, started, resized, moved from one surface to
// another, and taken apart.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

Renderer::Renderer(OrblitSurface *surface, OrblitBackend backend) {
  // Made by the host rather than here: where a frame goes is the one part of
  // presenting that differs by platform, and the host is what knows which.
  _surface = surface;
  _backendAsked = backend;
}

Renderer::~Renderer() {
  dispose();
  // A renderer that never started still owns the surface it was given.
  delete _surface;
  _surface = nullptr;
}

bool Renderer::initWithWidth(uint32_t width, uint32_t height) {
  // Before Filament, because starting Filament allocates through it. The
  // host made it; a renderer given none has nowhere to present.
  if (_surface == nullptr) return false;

  // Filament reports misuse by throwing, and an uncaught throw here would take
  // the whole application down rather than the one viewport that failed. The
  // message is worth keeping: it names the precondition, which is most of the
  // diagnosis.
  try {
    startWithWidth(width, height);
  } catch (const std::exception &error) {
    orblit::log("[orblit] Filament refused to start: %s", error.what());
    return false;
  } catch (...) {
    orblit::log("[orblit] Filament refused to start for an unknown reason.");
    return false;
  }
  return true;
}

/// Says why Filament is about to abort.
///
/// Its preconditions throw, and the throw cannot be caught from here — not by
/// type and not by `...` — so the process goes down with only a stack to show
/// for it. This runs *before* the throw, which is the one place the reason
/// can be read.
static void orblitReportPanic(void *user, const utils::Panic &panic) {
  orblit::log("[orblit] Filament refused: %s\n  at %s (%s:%d)", panic.getReason(),
        panic.getFunction(), panic.getFile(), panic.getLine());
}

void Renderer::startWithWidth(uint32_t width, uint32_t height) {
  const double startedFrom = orblit::now();
  utils::Panic::setPanicHandler(orblitReportPanic, nullptr);
  _width = std::max(width, 1u);
  _height = std::max(height, 1u);
  _pendingWidth = _width;
  _pendingHeight = _height;
  _presentedIndex = -1;
  _pacing = getenv("ORBLIT_PACE") != nullptr;

  // Asked for at the highest the device will give, because the standard
  // surface binds twelve samplers and Filament rations them by feature level:
  // a material may have nine below the third, whatever the hardware could
  // manage. It is asked for rather than assumed, because an engine built
  // above what the device supports fails to build at all rather than falling
  // back.
  //
  // Raising it is not the same as getting it. Filament's Metal backend
  // reports the third level for MTLGPUFamilyApple6 and newer — A13, so an
  // iPhone 11 and later, and every Apple silicon Mac — and the second for
  // anything else. The iOS simulator's virtual GPU is anything else: it
  // reports MTLGPUFamilyApple2, so the clamp below settles on the second
  // level and the standard surface is refused when the first lit object is
  // built. That is an abort, not a degradation; there is no fallback surface
  // to drop to yet.
  //
  // Which backend is the platform's, or the host's if it named one: see
  // OrblitBackend.cpp. Tried in turn where there is something to fall back to
  // — Vulkan then OpenGL off Apple — because a machine with no Vulkan driver
  // should still draw rather than refuse to start. On Apple there is one
  // candidate, Metal, exactly as before.
  Engine::Builder builder;
  const std::vector<OrblitBackend> candidates =
      orblit::backendCandidates(_backendAsked);
  for (OrblitBackend candidate : candidates) {
    // A backend whose driver is not installed is passed over rather than
    // tried: Filament loads the driver on its own thread, and a missing
    // Vulkan loader is a panic there that no try here can catch.
    if (!orblit::backendLoadable(candidate)) {
      orblit::log("[orblit] %s has no driver on this machine.",
                 orblit::backendName(candidate));
      continue;
    }
    builder.backend(orblit::filamentBackend(candidate));
    try {
      _engine = builder.build();
    } catch (const std::exception &error) {
      orblit::log("[orblit] %s would not start: %s",
                 orblit::backendName(candidate), error.what());
      _engine = nullptr;
    }
    if (_engine != nullptr) {
      _backend = candidate;
      break;
    }
  }
  ASSERT_PRECONDITION(_engine != nullptr, "%s is unavailable.",
                      orblit::backendName(candidates.front()));

  // Diagnostic only, and unrelated to OrblitScene.batching: Orblit's own
  // batching draws manually-instanced groups (see reconcileBatchGroups) and
  // never touches this flag. Filament's *automatic* instancing — merging
  // draw commands after the fact, in RenderPass::instanceify() — is broken on
  // stock Filament 1.76: instanceify() compares a custom command's stale
  // leftover state as though it were a draw, and can fold the colour-grading
  // subpass into a neighbouring instanced run so it never executes, which
  // brings a frame back entirely black. ORBLIT_FORCE_INSTANCING=1 raises this
  // flag anyway, so that bug can still be reproduced or measured against on
  // demand — a later Filament re-checked, or the two merge strategies
  // compared — without anything in this renderer asking for it on its own.
  if (getenv("ORBLIT_FORCE_INSTANCING") != nullptr) {
    _engine->setAutomaticInstancingEnabled(true);
  }

  // Diagnostic only, both of them, and both default to what a release build
  // already does. ORBLIT_BATCH_CHUNK caps how many members share one instanced
  // draw, so a batched frame can be compared against an unbatched one with
  // the group size taken out of the question — at one member a chunk is one
  // crate, culled and fitted from exactly the numbers an unbatched crate
  // would be. ORBLIT_BATCH_BOX=exact then builds that one-member chunk's
  // bounding box the way Filament builds an unbatched object's. See
  // rebuildBatchGroup for what the difference between the two is.
  if (const char *chunk = getenv("ORBLIT_BATCH_CHUNK")) {
    const int wanted = atoi(chunk);
    if (wanted > 0) {
      _chunkSize = uint32_t(std::min<int>(wanted, int(kInstancesPerDraw)));
    }
  }
  const char *boxMode = getenv("ORBLIT_BATCH_BOX");
  _exactChunkBox = boxMode != nullptr && strcmp(boxMode, "exact") == 0;
  _objectChunkBox = boxMode != nullptr && strcmp(boxMode, "object") == 0;
  _loadTrace = getenv("ORBLIT_LOAD_TRACE") != nullptr;
  const char *rootMode = getenv("ORBLIT_BATCH_ROOT");
  _rootTransformChunks = rootMode != nullptr && strcmp(rootMode, "transform") == 0;

  // Raised after the fact rather than in the builder for the same reason:
  // this one clamps to what is supported instead of refusing, so a device
  // that cannot manage it keeps the surfaces it can compile rather than
  // getting a renderer that will not start.
  const Engine::FeatureLevel supported = _engine->getSupportedFeatureLevel();
  if (supported > Engine::FeatureLevel::FEATURE_LEVEL_1) {
    _engine->setActiveFeatureLevel(supported);
  }

  // What the device can do, asked once here beside the feature level and
  // handed out afterwards without touching the engine, so a host can ask
  // from any thread — and decide what to load before it loads it.
  measureCapabilities(supported);

  // The standard lit surface needs the third level for its twelve samplers.
  // A device that cannot reach it — the iOS simulator, anything older than
  // an A13, OpenGL ES 3.0, WebGL 2 — gets the slim surface instead: nine
  // samplers, chosen once here from what the engine just answered and never
  // revisited, because a GPU does not grow more mid-session. surfaceAt reads
  // this to build the slim five packages in place of the standard five, and
  // every place that would otherwise bind a sampler the slim surface does
  // not declare reads it too.
  _slimSurface = supported < Engine::FeatureLevel::FEATURE_LEVEL_3;
  if (_slimSurface) {
    _surfaceNotes["surface"] = orblit::format(
        "This device supports Filament feature level %d, below the "
        "standard lit surface's third, so the slim surface is used "
        "instead. Base colour, normal, metallic/roughness, occlusion and "
        "emissive maps, ground blending and decals all draw as usual; "
        "rectangular area lights are not shadowed and the irradiance "
        "field does not light this scene.",
        int(supported));
    if (!hasSlimSurface()) {
      _surfaceNotes["tier"] = orblit::format(
          "...except that this build's materials were generated with "
          "ORBLIT_TIERS=\"%s\", which leaves the slim surface out. Every lit "
          "package in it is the sixteen sampler one, which this device cannot "
          "build, so lit objects will not draw. Regenerate the materials with "
          "the slim tier, or run this build only where feature level 3 is.",
          materialTiers());
    }
  }

  // Asked once, beside the feature level and for the same reason: it is a
  // property of the device and the driver, and neither changes mid-session.
  // Unlike the feature level it is not Filament's own answer to a question
  // about capability but a prediction of what Filament will do — see
  // orblit::shadowComparisonAvailable.
  _shadowComparison = _backend != ORBLIT_BACKEND_METAL ||
                      orblit::shadowComparisonAvailable();

  _renderer = _engine->createRenderer();

  // Every frame starts from nothing. Filament's default is to discard rather
  // than clear, which is free only while something draws every pixel — and
  // the buffer a view draws into is pooled, so a pixel nothing covers is
  // whatever an earlier frame left there. With the camera moving that is the
  // scene printed over and over across the sky, which is what a lost backdrop
  // looked like in Bistro. Cleared, the same fault is a black sky: still
  // wrong, but plainly so. Transparent rather than black for a view that
  // lets Flutter show through; an opaque one resolves it to black anyway.
  filament::Renderer::ClearOptions clear;
  clear.clearColor = {0.0, 0.0, 0.0, 0.0};
  clear.clear = true;
  clear.discard = true;
  _renderer->setClearOptions(clear);

  _scene = _engine->createScene();
  _view = _engine->createView();

  _cameraEntity = utils::EntityManager::get().create();
  _camera = _engine->createCamera(_cameraEntity);
  _camera->lookAt({3.2, 2.4, 3.2}, {0, 0, 0}, {0, 1, 0});

  _view->setCamera(_camera);
  _view->setScene(_scene);

  // One layer is drawn and one is not, which is what hiding an object means
  // here. Later work — render layers a host can name — widens this mask; the
  // two-state version costs the same and is the half that is needed now.
  // Every author layer, and not the hidden one. A pass narrows this; a frame
  // with no graph never does.
  _view->setVisibleLayers(0xFF, kAllLayers);

  // Shadows are on before any host has said anything about them, so the
  // substitution has to be in place before the first frame rather than only
  // when a pipeline block arrives. No block at all is every default, which is
  // what an empty one means to applyViewShadows.
  if (orblit::applyViewShadows(*_view, nullptr, 0, _shadowComparison)) {
    _surfaceNotes["shadows"] =
        "Filament does not compare depth samples on this device, so the hard "
        "and soft shadow kinds would each return nought for every receiver "
        "and leave the scene lit by ambient alone. Variance shadows are "
        "drawn instead: they compare in the shader rather than in the "
        "sampler. Edges are softer than asked for, and a variance shadow can "
        "bleed light through a thin occluder. Seen only on the iOS "
        "simulator, whose virtual GPU declines the feature set Filament "
        "tests; a real device passes it.";
  }

  const float defaultSky[3] = {kDefaultAmbient.x, kDefaultAmbient.y,
                               kDefaultAmbient.z};
  setSkyColour(defaultSky, kDefaultAmbientIntensity, true);

  startAssetLoader();
  buildGeometry();
  allocateBuffers();
  applyViewportSize();

  // Something to look at until a host sends a scene, so an empty viewport is
  // recognisably working rather than indistinguishable from a broken one. It
  // goes in through the same door a host's scene does — a placeholder built by
  // a second path would be a second path to keep working.
  const int64_t key[1] = {kPlaceholderKey};
  const float identity[16] = {1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1};
  const float colour[3] = {0.85f, 0.28f, 0.18f};
  const int32_t noMesh[1] = {-1};
  const int32_t flags[1] = {kCastsShadows | kReceivesShadows | kVisible};
  const int32_t noMaterial[1] = {-1};
  const int32_t noShapes[1] = {0};
  const float noWeights[1] = {0};
  applyObjects(key, identity, colour, noMesh, flags, noMaterial, noShapes, noWeights, {}, 1);
  _sceneIsOwnedByHost = false;

  const int64_t sunKey[1] = {kPlaceholderKey};
  const int32_t sunKind[1] = {0};
  const int32_t sunFlags[1] = {1};
  const float sun[18] = {1.0f, 0.96f, 0.9f, 110000.0f, 0,     0,
                         0,     -0.6f, -1.0f, -0.8f,     0,     0,
                         0,     0.53f, 0.1f,  10.0f,     80.0f, 0};
  applyLights(sunKey, sunKind, sunFlags, sun, 1);

  orblit::log("[orblit] engine ready in %.0f ms, %s surface (feature level %d)",
        (orblit::now() - startedFrom) * 1000,
        _slimSurface ? "slim" : "standard", int(supported));
}

void Renderer::allocateBuffers() {
  // Null while detached (see detachSurface) — Apple and headless hosts never
  // see that state, since their surface is set once and lives for the whole
  // renderer, but a resize requested while Android has no Surface to attach
  // to would otherwise dereference nothing here.
  if (_surface == nullptr) return;
  _surface->allocate(_engine, _width, _height, _swapChains, kOrblitBufferCount);
  _backIndex = 0;
  _presentedIndex = -1;
}

void Renderer::releaseBuffers() {
  // See allocateBuffers: dispose() calls this too, and dispose() may run
  // while Android has already let go of its surface.
  if (_surface == nullptr) return;
  _surface->release(_engine, _swapChains, kOrblitBufferCount);
}

void Renderer::applyViewportSize() {
  _view->setViewport({0, 0, _width, _height});
  projectWith(_fieldOfView, _orthographic, _viewHeight);
}

void Renderer::resizeToWidth(uint32_t width, uint32_t height) {
  _presentLock.lock();
  _pendingWidth = std::max(width, 1u);
  _pendingHeight = std::max(height, 1u);
  _presentLock.unlock();
}

bool Renderer::attachSurface(OrblitSurface *surface, uint32_t width, uint32_t height) {
  // Ownership passes the instant this is called, whatever it returns — the
  // same contract the constructor documents for the first surface a renderer
  // is given, and for the same reason: the caller (the C ABI's
  // orblit_renderer_attach_surface) has nothing else to do with it either way.
  detachSurface();
  _surface = surface;
  if (_surface == nullptr || _engine == nullptr || _disposed) return false;

  _width = std::max(width, 1u);
  _height = std::max(height, 1u);
  _pendingWidth = _width;
  _pendingHeight = _height;
  _presentLock.lock();
  _presentedIndex = -1;
  _presentLock.unlock();

  allocateBuffers();
  applyViewportSize();
  return _swapChains[0] != nullptr;
}

void Renderer::detachSurface() {
  if (_surface == nullptr) return;
  releaseBuffers();
  // The ordering the Android spike found load-bearing: Engine::destroy()
  // (inside releaseBuffers, via OrblitSurface::release) only queues the swap
  // chain's destruction onto Filament's driver thread. A host that releases
  // the native window it came from — an ANativeWindow, on Android — before
  // that drains is a use-after-free the driver still holds. flushAndWait
  // blocks until it has actually let go, which is why this comes before the
  // surface object itself (and whatever it wraps) goes.
  if (_engine != nullptr) _engine->flushAndWait();
  delete _surface;
  _surface = nullptr;
}

void Renderer::setOutlineKeys(const int64_t *keys, uint32_t count, const float *params) {
  if (_disposed) return;
  _outlineKeys.assign(keys, keys + count);
  _outlineStyle = orblit::OutlineStyle::from(params);
  const float primary = std::isfinite(params[12]) ? params[12] : 0.0f;
  _outlinePrimaryCount =
      std::min(count, static_cast<uint32_t>(std::max(0.0f, primary)));
}

void *Renderer::copyPresentedBuffer() {
  std::lock_guard<std::mutex> lock(_presentLock);
  // Opaque on the way out of the surface, and concrete only in the host that
  // asked for it: on Apple the plugin hands it straight to Flutter's texture
  // registry, and the registry wants a CVPixelBuffer.
  if (_surface == nullptr) return nullptr;
  return _surface->retainPresented(_presentedIndex);
}

void Renderer::dispose() {
  if (_disposed) return;
  _disposed = true;
  // A renderer whose engine never started has nothing of Filament's to give
  // back, and the teardown below would reach for what was never made.
  if (_engine == nullptr) return;

  // Before anything that owns a texture goes: the decoders stop, and nothing
  // uploads into a texture after this.
  if (_textureQueue) _textureQueue->shutdown();

  // Filament asserts on anything still alive when the engine goes down, so the
  // teardown mirrors construction in reverse.
  removeEverything();
  // Its renderables, instances, textures and material, and its sorting
  // threads joined, while the engine they belong to is still there.
  _splats.reset();
  _sprites.reset();
  _terrain.reset();

  // The graph's own views, cameras and targets, before the scene they point
  // at goes. _disposed is already set, so releaseGraph has to be able to run
  // afterwards — it checks the engine rather than that flag for exactly this.
  releaseGraph();

  // Its views and scenes point at the camera and share the scene's entities,
  // so it goes before either of them.
  _outline.reset();
  _outlineKeys.clear();
  // Motion blur hook: its passes, targets and materials, before the engine.
  if (_motionBlur) {
    _motionBlur->release();
    _motionBlur.reset();
  }

  for (auto &pair : _meshes) {
    if (pair.second.asset) _assetLoader->destroyAsset(pair.second.asset);
  }
  _meshes.clear();

  if (_identityInstances != nullptr) {
    _engine->destroy(_identityInstances);
    _identityInstances = nullptr;
  }

  for (auto &pair : _effectMaterials) {
    if (pair.second != nullptr) _engine->destroy(pair.second);
  }
  _effectMaterials.clear();

  releaseField();
  if (_fieldScene != nullptr) {
    _engine->destroy(_fieldScene);
    _fieldScene = nullptr;
  }
  if (_fieldView != nullptr) {
    _engine->destroy(_fieldView);
    _fieldView = nullptr;
  }
  if (_fieldCamera != nullptr) {
    utils::Entity entity = _fieldCamera->getEntity();
    _engine->destroyCameraComponent(entity);
    utils::EntityManager::get().destroy(entity);
    _fieldCamera = nullptr;
  }
  if (_fieldInstance != nullptr) {
    _engine->destroy(_fieldInstance);
    _fieldInstance = nullptr;
  }
  if (_fieldMaterial != nullptr) {
    _engine->destroy(_fieldMaterial);
    _fieldMaterial = nullptr;
  }
  if (_fieldVertices != nullptr) {
    _engine->destroy(_fieldVertices);
    _fieldVertices = nullptr;
  }
  if (_fieldIndices != nullptr) {
    _engine->destroy(_fieldIndices);
    _fieldIndices = nullptr;
  }
  if (!_fieldEntity.isNull()) {
    _engine->destroy(_fieldEntity);
    utils::EntityManager::get().destroy(_fieldEntity);
    _fieldEntity = {};
  }

  if (_smaaArea != nullptr) {
    _engine->destroy(_smaaArea);
    _smaaArea = nullptr;
  }
  if (_smaaSearch != nullptr) {
    _engine->destroy(_smaaSearch);
    _smaaSearch = nullptr;
  }
  if (_lightData != nullptr) {
    _engine->destroy(_lightData);
    _lightData = nullptr;
  }

  for (auto &entry : _probes) releaseProbe(entry.second);
  _probes.clear();
  if (_captureCamera != nullptr) {
    utils::Entity cameraEntity = _captureCamera->getEntity();
    _engine->destroyCameraComponent(cameraEntity);
    utils::EntityManager::get().destroy(cameraEntity);
    _captureCamera = nullptr;
  }
  if (_captureView != nullptr) {
    _engine->destroy(_captureView);
    _captureView = nullptr;
  }
  // Before the context the filters were made from.
  releaseEnvironmentCache();
  delete _specularFilter;
  _specularFilter = nullptr;
  delete _prefilter;
  _prefilter = nullptr;

  delete _resourceLoader;
  _resourceLoader = nullptr;
  gltfio::AssetLoader::destroy(&_assetLoader);
  // After the loader, whose assets named their entities in it.
  delete _names;
  _names = nullptr;
  _materialProvider->destroyMaterials();
  delete _materialProvider;
  _materialProvider = nullptr;
  // After the loader, which asks its providers to finish when it goes.
  _modelTextures.reset();

  // Materials before their textures, and both before the engine goes: an
  // instance still pointing at a destroyed texture is a use-after-free the
  // next time anything is drawn.
  for (auto &entry : _materials) {
    if (entry.second.instance != nullptr) _engine->destroy(entry.second.instance);
  }
  for (MaterialInstance *spent : _materialsSpent) {
    _engine->destroy(spent);
  }
  _materialsSpent.clear();
  _materials.clear();
  _materialOrder.clear();
  _materialRebuilt.clear();
  for (auto &entry : _ownTextures) {
    if (entry.second != nullptr) _engine->destroy(entry.second);
  }
  _ownTextures.clear();
  _textureQueue.reset();
  for (auto &entry : _movies) close(entry.second);
  _movies.clear();
  _movieOrder.clear();
  if (_blankTexture != nullptr) _engine->destroy(_blankTexture);
  if (_decalData != nullptr) _engine->destroy(_decalData);
  if (_decalPictures != nullptr) _engine->destroy(_decalPictures);
  if (_decalBlankPictures != nullptr) _engine->destroy(_decalBlankPictures);
  _decalData = _decalPictures = _decalBlankPictures = nullptr;
  if (_blankExternal != nullptr) _engine->destroy(_blankExternal);
  // The instance before the material it came from: destroying a material
  // while an instance of it is still alive is a precondition failure rather
  // than an error code, and takes the process with it.
  if (_depthOnly != nullptr) {
    _engine->destroy(_depthOnly);
    _depthOnly = nullptr;
  }
  if (_depthMaterial != nullptr) {
    _engine->destroy(_depthMaterial);
    _depthMaterial = nullptr;
  }
  for (Material *surface : _surfaces) {
    if (surface != nullptr) _engine->destroy(surface);
  }

  auto &entities = utils::EntityManager::get();
  _engine->destroyCameraComponent(_cameraEntity);
  entities.destroy(_cameraEntity);
  for (auto &entry : _populations) clearPopulation(entry.second);
  _populations.clear();
  if (_instancedMaterial != nullptr) _engine->destroy(_instancedMaterial);

  _engine->destroy(_skybox);
  if (_ambient) _engine->destroy(_ambient);
  for (size_t sheet = 0; sheet < _mistEntities.size(); sheet++) {
    _scene->remove(_mistEntities[sheet]);
    _engine->destroy(_mistEntities[sheet]);
    entities.destroy(_mistEntities[sheet]);
    _engine->destroy(_mistInstances[sheet]);
  }
  _mistEntities.clear();
  _mistInstances.clear();

  if (_cloudEntity) {
    _scene->remove(_cloudEntity);
    _engine->destroy(_cloudEntity);
    entities.destroy(_cloudEntity);
    _cloudEntity = utils::Entity();
  }
  if (_cloudInstance) {
    _engine->destroy(_cloudInstance);
    _cloudInstance = nullptr;
  }
  if (_cloudMaterial) {
    _engine->destroy(_skyVertices);
    _engine->destroy(_skyIndices);
    _engine->destroy(_cloudMaterial);
    _cloudMaterial = nullptr;
  }

  for (size_t pane = 0; pane < _rainEntities.size(); pane++) {
    _scene->remove(_rainEntities[pane]);
    _engine->destroy(_rainEntities[pane]);
    entities.destroy(_rainEntities[pane]);
    _engine->destroy(_rainInstances[pane]);
  }
  _rainEntities.clear();
  _rainInstances.clear();

  if (_mistMaterial) {
    _engine->destroy(_mistMaterial);
    _mistMaterial = nullptr;
  }
  if (_rainMaterial) {
    _engine->destroy(_rainMaterial);
    _rainMaterial = nullptr;
  }
  if (_quadVertices) {
    _engine->destroy(_quadVertices);
    _engine->destroy(_quadIndices);
    _quadVertices = nullptr;
  }

  _engine->destroy(_vertexBuffer);
  _engine->destroy(_indexBuffer);
  releaseBuffers();
  _engine->destroy(_view);
  _engine->destroy(_scene);
  _engine->destroy(_renderer);
  Engine::destroy(&_engine);
  _engine = nullptr;

  // After the engine, because releasing the buffers needs it — the surface
  // owns the images and the engine owns the chains onto them.
  {
    // Under the lock, because the thread that samples frames asks the
    // surface for one without going through the engine's queue.
    std::lock_guard<std::mutex> lock(_presentLock);
    delete _surface;
    _surface = nullptr;
  }
}

Notes Renderer::notes() {
  // What is wrong with *this* scene.
  //
  // A file that could not be read is remembered for as long as the renderer
  // lives, because it is only read once and re-reading it every frame to
  // find out it is still missing would be four hundred failed opens a
  // second. But remembering it is not the same as reporting it: a scene that
  // does not name that file has nothing wrong with it, and saying otherwise
  // put "the file could not be read" over a street that had loaded perfectly,
  // because a different example had failed a minute earlier.
  //
  // So the memory is kept and the answer is filtered to the files the scene
  // in front of us actually asks for.
  std::set<std::string> asked;
  for (const auto &pair : _drawn) {
    if (!pair.second.path.empty()) asked.insert(pair.second.path);
  }
  // The environment's are kept under these two names rather than its paths,
  // and are about the scene for as long as it names an environment.
  if (!_environmentRadiancePath.empty()) asked.insert("environment");
  if (!_environmentSkyboxPath.empty()) asked.insert("skybox");

  Notes all;
  for (const auto &entry : _assetNotes) {
    if (asked.count(entry.first) != 0) all[entry.first] = entry.second;
  }

  // These are already about the scene as it stands rather than about a
  // file, so they are reported as they are. Later ones win a shared key, as
  // addEntriesFromDictionary: had it.
  for (const auto &entry : _objectNotes) all[entry.first] = entry.second;
  for (const auto &entry : _poseNotes) all[entry.first] = entry.second;
  for (const auto &entry : _lightNotes) all[entry.first] = entry.second;
  for (const auto &entry : _decalNotes) all[entry.first] = entry.second;
  for (const auto &entry : _splatNotes) all[entry.first] = entry.second;
  for (const auto &entry : _spriteNotes) all[entry.first] = entry.second;
  for (const auto &entry : _terrainNotes) all[entry.first] = entry.second;
  // A texture's problem, while what named it is still named: its model among
  // the objects, or its path among the materials' or the sprite layers'.
  // After the sprites', whose note for an image that did not load only says
  // that it did not; this says why.
  for (const auto &entry : _textureNotes) {
    const auto by = _textureNotedFor.find(entry.first);
    const std::string model = by != _textureNotedFor.end() ? by->second : "";
    const bool named = model.empty()
                           ? _materialTexturePaths.count(entry.first) != 0 ||
                                 _spriteTexturePaths.count(entry.first) != 0
                           : asked.count(model) != 0;
    if (named) all[entry.first] = entry.second;
  }
  for (const auto &entry : _videoNotes) all[entry.first] = entry.second;
  for (const auto &entry : _surfaceNotes) all[entry.first] = entry.second;
  // Not problems, and not about the scene as a whole: what each model built
  // by the last publish holds, keyed so the Dart side can take them out.
  for (const auto &entry : _modelInfo) all[entry.first] = entry.second;
  return all;
}


void Renderer::requestCapture() {
  std::lock_guard<std::mutex> lock(_captureLock);
  _captureWanted = true;
}

bool Renderer::capturedFrame(std::vector<uint8_t> &rgba, uint32_t &width,
                             uint32_t &height) {
  std::lock_guard<std::mutex> lock(_captureLock);
  if (!_captureReady) return false;
  rgba = _captured;
  width = _capturedWidth;
  height = _capturedHeight;
  return true;
}

void Renderer::readBackIfAsked() {
  {
    std::lock_guard<std::mutex> lock(_captureLock);
    if (!_captureWanted || _captureInFlight) return;
    _captureWanted = false;
    _captureInFlight = true;
  }

  // What arrives, and where it is going. Filament calls back with the
  // buffer and one pointer, so the size travels with the renderer.
  struct Arrival {
    Renderer *renderer;
    uint32_t width;
    uint32_t height;
  };
  const size_t bytes = size_t(_width) * _height * 4;
  auto *pixels = static_cast<uint8_t *>(malloc(bytes));
  // Filament's readPixels contract is top-row-first on every backend.
  // Its OpenGL driver already reverses glReadPixels rows; reversing them
  // again here turns captures (and CPU-copy presentation) upside down.
  auto *arrival = new Arrival{this, _width, _height};
  _renderer->readPixels(
      0, 0, _width, _height,
      backend::PixelBufferDescriptor(
          pixels, bytes, backend::PixelDataFormat::RGBA,
          backend::PixelDataType::UBYTE,
          [](void *buffer, size_t, void *user) {
            auto *arrival = static_cast<Arrival *>(user);
            Renderer *self = arrival->renderer;
            const size_t stride = size_t(arrival->width) * 4;
            std::lock_guard<std::mutex> lock(self->_captureLock);
            self->_captured.resize(stride * arrival->height);
            memcpy(self->_captured.data(), buffer, stride * arrival->height);
            self->_capturedWidth = arrival->width;
            self->_capturedHeight = arrival->height;
            self->_captureReady = true;
            self->_captureInFlight = false;
            free(buffer);
            delete arrival;
          },
          arrival));
}
}  // namespace orblit
