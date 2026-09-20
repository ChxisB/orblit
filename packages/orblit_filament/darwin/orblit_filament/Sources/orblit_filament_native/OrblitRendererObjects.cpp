#include "OrblitRendererInternal.h"

// Publishing a scene: the pipeline settings, the videos and materials the
// objects wear, and the objects themselves.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

void Renderer::setPipeline(const float *params, size_t count) {
  static_assert(orblit::pipeline::kPipelineStride <= 32,
                "the pipeline block has outgrown _pipelineParams: widen the "
                "array and the clamp below, or the newest dials are dropped");
  if (_disposed || _view == nullptr) return;
  if (count > 32) count = 32;

  // Nothing moved. Worth checking first: half of what follows dirties a
  // render target or a shadow map, and a scene republished on every frame of
  // a drag changes none of it.
  if (count == _pipelineCount &&
      std::memcmp(_pipelineParams, params, sizeof(float) * count) == 0) {
    return;
  }
  const bool shadowsChanged =
      orblit::shadowSettingsDiffer(_pipelineParams, _pipelineCount, params, count);
  std::memcpy(_pipelineParams, params, sizeof(float) * count);
  _pipelineCount = count;
  applyTextureLimits();

  // On or off, which kind, and the dials of the soft and variance kinds. The
  // note is left exactly as startWithWidth set it: whether a variance shadow
  // was substituted cannot change within a session, because what decides it
  // is the device.
  orblit::applyViewShadows(*_view, params, count, _shadowComparison);

  // The multisample count is the one setting here that reallocates every
  // buffer in the view, so it is set through the same comparison as the rest
  // rather than every frame.
  MultiSampleAntiAliasingOptions msaa;
  msaa.enabled = params[14] > 1.0f;
  msaa.sampleCount = static_cast<uint8_t>(params[14] < 1 ? 1 : params[14]);
  _view->setMultiSampleAntiAliasingOptions(msaa);

  // Render scale. The host sends a low and a high end, and asks with
  // params[10] for the renderer to move between them under load. It does not
  // get to: a scale that moves between frames leaves most of the buffer
  // undrawn on this backend, so the range is collapsed to a single value here
  // and the view renders at one fixed size.
  //
  // Filament says why, in Options.h: dynamic resolution "is only supported on
  // platforms where the time to render a frame can be measured accurately. On
  // platforms where this is not supported, Dynamic Resolution can't be enabled
  // unless minScale == maxScale". Orblit calls neither setFrameRateOptions nor
  // setDisplayInfo, so Filament has no target frame time to aim at, and asking
  // it to move the scale anyway is a configuration its own header rules out.
  //
  // What that looked like before this: Bistro asked for 0.6 to 1.0, and seven
  // frames in nine came back with the scene drawn into a 0.82-by-0.82 corner
  // of a full-size buffer, one of them with almost nothing drawn at all. The
  // rest of the buffer is never written, and Filament's default ClearOptions
  // discard the swapchain rather than clearing it, so unwritten means
  // undefined — which on Metal samples as white or magenta (see the note on
  // the uncompressed formats in OrblitTextures.cpp). Temporal anti-aliasing
  // then blends each frame into the next and smears that forward, which is the
  // streaking that looks like the model being painted over itself.
  //
  // Pinning is not a workaround for a fault nobody found: a fixed scale is
  // clean and a moving one is not, measured both ways twice. Nine frames each
  // across a night-to-day switch, which is the load spike that moves the
  // scale: fixed 0.6 -> 0 corrupt, fixed 1.0 -> 0, fixed 0.6 again -> 0, and
  // 0.6-to-1.0 -> 7.
  //
  // The high end is the one to keep. A host's maxScale is the quality it
  // actually wants; dropping below it was only ever a concession to load, and
  // a concession this renderer cannot make safely is not one to make quietly
  // at the cost of every frame's sharpness. A host that would rather trade
  // sharpness for headroom can still say so outright, with a fixed scale below
  // one, and that path is measured clean too.
  const float low = std::min(params[11], params[12]);
  const float high = std::max(params[11], params[12]);
  const float scale = params[10] != 0.0f ? high : low;

  DynamicResolutionOptions resolution;
  // Not "what the host asked for". Filament ignores minScale and maxScale
  // unless this is on, so any scale that is not full size needs it on as well
  // — off means full size, not "use the scale I sent". That covers a scale
  // above one too, which is supersampling rather than a saving.
  resolution.enabled = scale != 1.0f;
  resolution.homogeneousScaling = true;
  resolution.minScale = filament::math::float2{scale, scale};
  resolution.maxScale = filament::math::float2{scale, scale};
  resolution.sharpness = params[13];
  resolution.quality = View::QualityLevel::HIGH;

  _view->setDynamicResolutionOptions(resolution);

  const int flags = static_cast<int>(params[15]);
  View::RenderQuality quality;
  quality.hdrColorBuffer =
      (flags & 1) != 0 ? View::QualityLevel::ULTRA : View::QualityLevel::HIGH;
  _view->setRenderQuality(quality);

  // Where the light grid is anchored. Only worth setting when the host has
  // sent the two numbers — a shorter pipeline block is one from before they
  // existed, and Filament's own defaults are the right answer for it.
  if (_pipelineCount >= 18) {
    const float near = _pipelineParams[16] > 0 ? _pipelineParams[16] : 5.0f;
    // Far has to be beyond near or the grid has no depth to divide.
    const float far =
        _pipelineParams[17] > near ? _pipelineParams[17] : near + 1.0f;
    _view->setDynamicLightingOptions(near, far);
  }
  _view->setFrustumCullingEnabled((flags & 2) != 0);
  _view->setScreenSpaceRefractionEnabled((flags & 4) != 0);

  // Shadow options live on the light, not on the view, so every light that
  // already exists has to be told again. Only when something about shadows
  // actually changed: this walks every light in the scene.
  if (shadowsChanged) refreshShadowOptions();
}

/// Writes the pipeline's shadow settings onto one light.
void Renderer::shadowOptionsFor(utils::Entity entity) {
  auto &lights = _engine->getLightManager();
  auto instance = lights.getInstance(entity);
  if (!instance) return;
  if (_pipelineCount == 0) return;

  LightManager::ShadowOptions options = lights.getShadowOptions(instance);
  orblit::applyLightShadows(options, _pipelineParams, _pipelineCount);
  lights.setShadowOptions(instance, options);
}

/// Tells every light in the scene about a change to the shadow settings.
void Renderer::refreshShadowOptions() {
  for (auto &entry : _lit) shadowOptionsFor(entry.second.entity);
}

void Renderer::applyVideos(const int64_t *keys, const int32_t *flags,
                           const float *params,
                           const std::vector<std::string> &paths,
                           uint32_t count) {
  if (_disposed) return;

  const uint64_t generation = ++_videoGeneration;
  _movieOrder.clear();
  _movieOrder.reserve(count);
  // Said again by this publish if it is still true, so a scene that stops
  // naming a video stops being told it cannot have it.
  _videoNotes.clear();

  for (uint32_t i = 0; i < count; i++) {
    Movie &movie = _movies[keys[i]];
    movie.seen = generation;
    const float *values = params + i * kVideoParams;
    const std::string path = i < paths.size() ? paths[i] : std::string();

    // A different file is a different video, whatever the key says. Anything
    // else — rate, volume, playing — is a change to this one.
    if (movie.decoder == nullptr || movie.path != path) open(movie, path);
    if (movie.decoder == nullptr) {
      _movieOrder.push_back(&movie);
      continue;
    }

    movie.looping = (flags[i] & 2) != 0;
    movie.decoder->setLooping(movie.looping);

    // The seek is reconciled by its token rather than by its target, so
    // saying the same seek sixty times a second is one seek and not sixty.
    const int32_t token = static_cast<int32_t>(values[3]);
    if (token != movie.seekToken) {
      movie.seekToken = token;
      if (values[2] >= 0) movie.decoder->seek(values[2]);
    }

    if (values[1] != movie.volume) {
      movie.volume = values[1];
      movie.decoder->setVolume(values[1]);
    }

    const bool playing = (flags[i] & 1) != 0;
    if (flags[i] != movie.flags || values[0] != movie.rate) {
      movie.flags = flags[i];
      movie.rate = values[0];
      if (playing) {
        movie.decoder->play(movie.rate);
      } else {
        movie.decoder->pause();
      }
    }

    _movieOrder.push_back(&movie);
  }

  for (auto it = _movies.begin(); it != _movies.end();) {
    if (it->second.seen == generation) {
      ++it;
      continue;
    }
    close(it->second);
    it = _movies.erase(it);
  }
}

/// Takes whatever frame each decoder has ready and puts it on the GPU.
///
/// Called once a frame. A video that has not advanced hands back nothing and
/// costs a single comparison; the picture already on the texture stays.
void Renderer::pumpVideos() {
  if (_movies.empty()) return;
  for (auto &entry : _movies) {
    Movie &movie = entry.second;
    if (movie.decoder == nullptr || movie.texture == nullptr) continue;
    // The decoder puts its newest frame on the texture where it lies, and
    // keeps it until the next one replaces it.
    movie.decoder->pump(*_engine, movie.texture);
  }
}

void Renderer::applyMaterials(const int64_t *keys, const int32_t *flags, const float *params, const int32_t *maps, const std::vector<std::string> &texturePaths, const int32_t *textureSrgb, const int32_t *videos, uint32_t count) {
  if (_disposed) return;

  // Last publish's leavings, now that everything has been re-dressed.
  for (MaterialInstance *spent : _materialsSpent) {
    _engine->destroy(spent);
  }
  _materialsSpent.clear();

  bool warmed = false;
  const uint64_t generation = ++_materialGeneration;
  _materialTexturePaths.clear();
  _materialTexturePaths.insert(texturePaths.begin(), texturePaths.end());
  _materialOrder.clear();
  // Rebuilt by the writes below, so an instance that has gone does not
  // outlive its entry here.
  _targetBindings.clear();
  _materialOrder.reserve(count);
  _materialRebuilt.clear();
  _materialRebuilt.reserve(count);

  for (uint32_t i = 0; i < count; i++) {
    Surfaced &surface = _materials[keys[i]];
    surface.seen = generation;
    const float *values = params + i * kMaterialParams;
    const int32_t *entries = maps + i * kMaterialMaps;

    // A change of blend mode or shading is a different compiled material, so
    // the instance is replaced rather than reconfigured. Everything else is
    // set on the instance in place.
    const int wanted = surfaceIndexFor(flags[i]);
    const bool rebuild =
        surface.instance == nullptr ||
        surfaceIndexFor(surface.flags) != wanted ||
        surface.flags == -1;
    if (rebuild) {
      if (surface.instance != nullptr) {
        _materialsSpent.push_back(surface.instance);
      }
      surface.instance = surfaceAt(wanted)->createInstance();
      if ((flags[i] & 3) == 0) setDefaultsOn(surface.instance);
      surface.written = false;
    }

    if (rebuild || surface.flags != flags[i]) {
      surface.flags = flags[i];
      applyRasterState(surface, values[17], values[18]);
      // A new sampler means every map has to be bound again, so the numbers
      // are rewritten with them rather than compared.
      surface.written = false;
    }

    const bool sameParams =
        (surface.flags & 3) != 2 && surface.written &&
        std::memcmp(surface.params, values, sizeof(float) * kMaterialParams) == 0 &&
        std::memcmp(surface.maps, entries, sizeof(int32_t) * kMaterialMaps) == 0;
    if (!sameParams) {
      std::memcpy(surface.params, values, sizeof(float) * kMaterialParams);
      std::memcpy(surface.maps, entries, sizeof(int32_t) * kMaterialMaps);
      surface.written = true;
      write(surface, values, entries, texturePaths, textureSrgb, videos[i]);
      if (((surface.flags >> 2) & 15) == 3) {
        surface.instance->setMaskThreshold(values[17]);
      }
      surface.instance->setPolygonOffset(values[18], values[18] * 1000.0f);
    }

    _materialOrder.push_back(surface.instance);
    _materialRebuilt.push_back(rebuild);

    // The first scene to be made of a surface is the one that pays for it.
    // Done here rather than in surfaceAt because that is also called from
    // inside a frame, where issuing sixteen variants' worth of compiles is
    // the stall this is meant to prevent.
    const uint32_t bit = 1u << wanted;
    if ((_surfacesWarmed & bit) == 0) {
      _surfacesWarmed |= bit;
      warmUp(_surfaces[wanted]);
      warmed = true;
    }
  }

  // A material nothing is made of any more. Its instance goes; the textures
  // it used stay, because the next scene almost always wants them again and
  // an image is expensive to read twice.
  for (auto it = _materials.begin(); it != _materials.end();) {
    if (it->second.seen == generation) {
      ++it;
      continue;
    }
    if (it->second.instance != nullptr) {
      _materialsSpent.push_back(it->second.instance);
    }
    it = _materials.erase(it);
  }

  // Filament queues the compile commands rather than sending them, so without
  // this the backend would not see them until whatever flushes next — which
  // is the first frame, the one they exist to keep clear.
  if (warmed) _engine->flush();
}

/// Puts one object onto a material, or back onto the ones it came with.
void Renderer::dress(Drawn &drawn, int32_t index) {
  auto &renderableManager = _engine->getRenderableManager();
  MaterialInstance *instance =
      (index >= 0 && index < static_cast<int32_t>(_materialOrder.size()))
          ? _materialOrder[index]
          : nullptr;

  if (drawn.instance != nullptr) {
    const utils::Entity *entities = drawn.instance->getEntities();
    const size_t entityCount = drawn.instance->getEntityCount();

    // The file's own materials, kept the first time one is overridden. Every
    // primitive in order, so putting them back is the same walk.
    if (instance != nullptr && drawn.ownMaterials.empty()) {
      for (size_t i = 0; i < entityCount; i++) {
        auto renderable = renderableManager.getInstance(entities[i]);
        if (!renderable) continue;
        for (size_t p = 0; p < renderableManager.getPrimitiveCount(renderable); p++) {
          drawn.ownMaterials.push_back(
              renderableManager.getMaterialInstanceAt(renderable, p));
        }
      }
    }

    size_t slot = 0;
    for (size_t i = 0; i < entityCount; i++) {
      auto renderable = renderableManager.getInstance(entities[i]);
      if (!renderable) continue;
      for (size_t p = 0; p < renderableManager.getPrimitiveCount(renderable); p++) {
        MaterialInstance *chosen = instance;
        if (chosen == nullptr) {
          if (slot >= drawn.ownMaterials.size()) { slot++; continue; }
          chosen = drawn.ownMaterials[slot];
        }
        slot++;
        if (chosen != nullptr) {
          renderableManager.setMaterialInstanceAt(renderable, p, chosen);
        }
      }
    }
    return;
  }

  if (!drawn.entity) return;
  auto renderable = renderableManager.getInstance(drawn.entity);
  if (!renderable) return;
  // No material named: back to the object's own instance, which is what its
  // colour is written into.
  MaterialInstance *chosen = instance != nullptr ? instance : drawn.material;
  if (chosen != nullptr) {
    renderableManager.setMaterialInstanceAt(renderable, 0, chosen);
  }
}

void Renderer::applyObjects(const int64_t *keys, const float *transforms, const float *colours, const int32_t *meshes, const int32_t *flags, const int32_t *materials, const int32_t *morphCounts, const float *morphWeights, const std::vector<std::string> &paths, uint32_t count) {
  if (_disposed) return;

  // Where this object's shapes begin in the weights, walked alongside the
  // objects: the sender packs them end to end in the order it names them.
  size_t morphAt = 0;

  const uint64_t generation = ++_objectGeneration;
  auto &transformManager = _engine->getTransformManager();
  // Only what this publish builds is described; see build.
  _modelInfo.clear();

  // Motion blur hook: a publish begins. Nothing is remembered unless a graph
  // blurs objects by their own motion.
  if (_motionBlur) _motionBlur->beginPublish();
  Notes notes;

  // Every layer each shared material is worn on, so a decal masked to a
  // layer can be tested against it. A shared instance is one set of
  // uniforms for every object wearing it, so the best it can say is all of
  // their layers at once.
  std::unordered_map<MaterialInstance *, int32_t> decalWearers;

  // Which objects a group's worth of merging was found for, in the order
  // they arrive — reconciled into BatchGroups once every object below has
  // been sorted into one or left out. Keyed the same way the census keys
  // them, so a group here is exactly a key the census counted four or more
  // eligible objects under.
  std::unordered_map<orblit::BatchKey, std::vector<uint32_t>, orblit::BatchKeyHash>
      groupIndices;

  // Who is like whom, counted before anything is built, because whether one
  // object batches depends on how many others share its key.
  //
  // Three kinds of object are counted out rather than in:
  //
  //  * A morphing one. Filament will not merge a renderable with morph
  //    targets, and its weights are its own anyway.
  //  * A hidden one. It is not drawn, so counting it could push a group over
  //    the threshold on the strength of objects that draw nothing.
  //  * A model wearing its own file's materials. gltfio gives every copy its
  //    own material instances, so two copies cannot be merged without being
  //    made to share one copy's instances — which is a change to what the
  //    other copies are made of, not just to how they are drawn. A model that
  //    wears a named Orblit material is a different matter and does batch: it
  //    already shares that material's one instance with everything else made
  //    of it, so there is nothing to arrange and merging just happens.
  // Counted up again from nothing as the objects are walked, the same way the
  // batching numbers are: what is wanted is what this publish did, not what
  // every publish since launch has done.
  _prepassObjects = 0;
  _census.clear();
  if (_batching) {
    for (uint32_t i = 0; i < count; i++) {
      const bool ownFileMaterials = meshes[i] >= 0 && materials[i] < 0;
      const bool eligible = morphCounts[i] <= 0 &&
                            (flags[i] & kVisible) != 0 && !ownFileMaterials;
      _census.add(orblit::BatchCensus::keyFor(meshes[i], materials[i],
                                             colours + i * 3, flags[i],
                                             meshes[i] < 0 && materials[i] < 0),
                  eligible);
    }
  }

  for (uint32_t i = 0; i < count; i++) {
    std::string path;
    const int32_t meshIndex = meshes[i];
    if (meshIndex >= 0 && meshIndex < static_cast<int32_t>(paths.size())) {
      path = paths[meshIndex];
    }

    // Default-constructed on first sight, which is how a new object announces
    // itself: there is no separate "added" message, only a key nobody has
    // seen before.
    Drawn &drawn = _drawn[keys[i]];

    // Two objects claiming one identity: the second would take the first's
    // place, and one of them would appear to have been deleted. Keys are the
    // host's to keep unique, and this is where that goes wrong.
    if (drawn.seen == generation) {
      notes["keys"] = "Two objects in this scene are sharing one key, so "
                       "only one of them is drawn.";
      continue;
    }

    // Manually instanced: this object is drawn as a slot in a shared group
    // renderable rather than as one of its own, and takes no further part in
    // this loop — reconcileBatchGroups, once every object here has been
    // sorted into a group or left out of one, does the rest.
    //
    // A named mesh is left out even where the census counts it as eligible:
    // gltfio gives every copy of a model its own hierarchy of entities, one
    // renderable per primitive, and merging that needs a manually-instanced
    // renderable per primitive per chunk rather than the single one built
    // below for the placeholder cube. Nothing here needs it yet — every
    // scene this renderer is proven against draws the placeholder cube — and
    // such an object still draws correctly on its own, just unmerged, so
    // leaving it out costs a saving rather than a picture.
    if (_batching && meshIndex < 0 && _census.batches(i)) {
      if (drawn.entity || drawn.instance != nullptr) {
        // Was its own renderable last publish; not any more.
        recycle(drawn);
        drawn = Drawn{};
      }
      drawn.seen = generation;
      groupIndices[orblit::BatchCensus::keyFor(meshIndex, materials[i],
                                              colours + i * 3, flags[i],
                                              materials[i] < 0)]
          .push_back(i);
      continue;
    }

    // A different file is a different object, so it is built again. Nothing
    // else is: the rest is written into what is already there.
    const bool exists = drawn.entity || drawn.instance != nullptr;
    // So is one drawn as the cube because its file was missing, once bytes
    // may have been provided under that name: a host that fetches a model
    // names it first and hands it over when it arrives, and restating the
    // same scene has to be enough to pick it up.
    const bool arrived = exists && drawn.instance == nullptr && !path.empty() &&
                         meshMayHaveArrived(path);
    if (exists && (drawn.path != path || arrived)) {
      recycle(drawn);
      drawn = Drawn{};
    }
    if (!drawn.entity && drawn.instance == nullptr) {
      build(drawn, path);
    }
    drawn.seen = generation;

    mat4f placement;
    std::memcpy(&placement, transforms + i * 16, sizeof(float) * 16);
    // Compared rather than written blindly. Setting a transform dirties the
    // node and everything under it, and a scene republished on every frame of
    // a drag is one object moving and the rest standing perfectly still.
    if (!drawn.placed ||
        std::memcmp(&placement, &drawn.transform, sizeof(mat4f)) != 0) {
      drawn.transform = placement;
      drawn.placed = true;
      const utils::Entity root =
          drawn.instance != nullptr ? drawn.instance->getRoot() : drawn.entity;
      transformManager.setTransform(transformManager.getInstance(root),
                                    placement);
      // The prepass entity stands exactly where the object does, or it would
      // write depth for a shape that is somewhere else. Its own transform
      // rather than a parenting, because a renderable built by Filament gets
      // a root transform of its own and re-parenting it would be a second
      // thing to keep right.
      if (drawn.prepass) {
        transformManager.setTransform(
            transformManager.getInstance(drawn.prepass), placement);
      }
    }

    // Motion blur hook: where this object stands in this publish and what it
    // draws with, so its motion can be measured against the last publish.
    if (_motionBlur && _motionBlur->wanted()) {
      if (drawn.instance != nullptr) {
        _motionBlur->place(keys[i], placement, drawn.instance->getRoot(),
                           drawn.instance->getEntities(),
                           drawn.instance->getEntityCount());
      } else {
        _motionBlur->place(keys[i], placement, drawn.entity, &drawn.entity, 1);
      }
    }

    // A mesh brings its own materials out of the file, so the object's colour
    // reaches the placeholder cube and nothing else. Tinting somebody's model
    // by a swatch they never chose is worse than ignoring the swatch.
    if (drawn.material != nullptr) {
      const float3 colour = {colours[i * 3], colours[i * 3 + 1],
                             colours[i * 3 + 2]};
      if (colour.x != drawn.colour.x || colour.y != drawn.colour.y ||
          colour.z != drawn.colour.z) {
        drawn.colour = colour;
        drawn.material->setParameter("baseColor",
                                     float4{colour.x, colour.y, colour.z, 1.0f});
      }
    }

    if (flags[i] != drawn.flags) {
      drawn.flags = flags[i];
      applyFlags(flags[i], drawn);
    }

    // Compared rather than written, because dressing an object walks every
    // primitive it has and a model can have hundreds. The instance behind the
    // index may have changed underneath, but that is a change to the material
    // and the renderable is already pointing at it.
    // Re-dressed when the index moved, and also when the material at that
    // index was built afresh: the renderable holds the instance, not the
    // material, so a material that changed its blend mode is a new instance
    // and the old one is about to go.
    const int32_t wearing = materials[i];
    const bool remade = wearing >= 0 &&
                        wearing < static_cast<int32_t>(_materialRebuilt.size()) &&
                        _materialRebuilt[wearing];

    if (wearing != drawn.surface || remade) {
      drawn.surface = wearing;
      dress(drawn, wearing);
    }

    // The depth-only twin: built, kept or dropped. After dressing, because
    // whether the prepass covers this object depends on what it is made of
    // now rather than on what it was made of when it arrived.
    syncPrepass(drawn, flags[i], wearing);

    // Which layer decals see this object on. Its own instance says exactly;
    // a shared one is gathered and written once the loop is done.
    const int32_t decalBit = int32_t(layerBitOf(flags[i]));
    if (drawn.material != nullptr && drawn.decalLayer != decalBit) {
      drawn.decalLayer = decalBit;
      drawn.material->setParameter("decalLayer", decalBit);
    }
    if (wearing >= 0 && wearing < static_cast<int32_t>(_materialOrder.size())) {
      decalWearers[_materialOrder[wearing]] |= decalBit;
    }

    // How far each of the mesh's shapes is dialled in. Written every publish
    // rather than compared first: a weight is what animates, so it is the one
    // number here that is expected to differ on every frame, and a memcmp to
    // find that out is work with a known answer.
    const size_t shapes = size_t(std::max(morphCounts[i], 0));
    if (shapes > 0) {
      morph(drawn, morphWeights + morphAt, shapes);
      // Kept for a frame of animation to lay back over whatever a clip does
      // to the same shapes. Assigned, which reuses the storage it already has.
      if (drawn.instance != nullptr) {
        drawn.morphWeights.assign(morphWeights + morphAt,
                                  morphWeights + morphAt + shapes);
      }
    } else if (!drawn.morphWeights.empty()) {
      drawn.morphWeights.clear();
    }
    morphAt += shapes;
  }

  // Every group the census found this time contributes its layer too, the
  // same way an unbatched object above already does — once per group rather
  // than once per member, since every member of a group is on one layer by
  // construction.
  for (const auto &group : groupIndices) {
    const orblit::BatchKey &key = group.first;
    if (key.surface >= 0 &&
        key.surface < static_cast<int32_t>(_materialOrder.size())) {
      decalWearers[_materialOrder[key.surface]] |= int32_t(layerBitOf(key.flags));
    }
  }

  // Only the lit surface paints decals, so only it has the parameter; the
  // unlit and video ones would refuse it. Compared first, because a uniform
  // write dirties the instance's whole block.
  for (const auto &worn : decalWearers) {
    MaterialInstance *instance = worn.first;
    if (!instance->getMaterial()->hasParameter("decalLayer")) continue;
    if (instance->getParameter<int32_t>("decalLayer") == worn.second) continue;
    instance->setParameter("decalLayer", worn.second);
  }

  // Whatever this publish did not mention has left the scene. Sweeping by
  // stamp rather than by a removal message means a host cannot leak an object
  // by forgetting to say it went.
  for (auto it = _drawn.begin(); it != _drawn.end();) {
    if (it->second.seen == generation) {
      ++it;
      continue;
    }
    recycle(it->second);
    it = _drawn.erase(it);
  }

  // Every group's chunks brought up to date with this publish — rebuilt if
  // its membership moved, written into if only a transform or the material
  // did, torn down if nobody asked for it this time — and this publish's
  // batching stats read off what was actually built. Before the colour pool
  // is swept: a group claims a colour-pool instance the same way an
  // unbatched placeholder cube does, and a group that only wrote a transform
  // this time still needs its colour kept rather than reclaimed.
  reconcileBatchGroups(groupIndices, keys, transforms, colours, generation);

  // Shared surfaces nobody asked for this time. After every object has been
  // re-dressed, every departed one recycled and every group reconciled, so
  // nothing still wears them.
  _colourPool.sweep(generation,
                    [this](MaterialInstance *spent) { _engine->destroy(spent); });

  sweepUnnamedMeshes();

  _objectNotes = notes;
  _sceneIsOwnedByHost = true;
}
}  // namespace orblit
