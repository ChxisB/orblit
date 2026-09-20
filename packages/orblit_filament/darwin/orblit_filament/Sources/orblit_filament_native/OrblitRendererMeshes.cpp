#include "OrblitRendererInternal.h"

// What the device can do, the asset loader, and the models it loads.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

void Renderer::startAssetLoader() {
  _materialProvider = gltfio::createUbershaderProvider(
      _engine, UBERARCHIVE_DEFAULT_DATA, UBERARCHIVE_DEFAULT_SIZE);

  // Names, so a model's joints, lights and cameras can be reported by what the
  // file calls them. gltfio only keeps them when it is given somewhere to.
  _names = new utils::NameComponentManager(utils::EntityManager::get());

  gltfio::AssetConfiguration assetConfig{};
  assetConfig.engine = _engine;
  assetConfig.materials = _materialProvider;
  assetConfig.names = _names;
  _assetLoader = gltfio::AssetLoader::create(assetConfig);

  gltfio::ResourceConfiguration resourceConfig{};
  resourceConfig.engine = _engine;
  // Well-formed files do not need this; a file exported by something careless
  // does, and a character whose weights do not sum to one deforms subtly
  // wrongly in a way that is very hard to trace back to the exporter.
  resourceConfig.normalizeSkinningWeights = true;
  _resourceLoader = new gltfio::ResourceLoader(resourceConfig);

  // After the capabilities, which say how large a texture may be and which
  // formats the device samples; see measureCapabilities.
  _textureQueue = std::make_unique<orblit::TextureQueue>(
      *_engine,
      uint32_t(std::max(0, _capabilities[ORBLIT_CAPABILITY_MAX_TEXTURE_SIZE])),
      uint32_t(std::max(1, _capabilities[ORBLIT_CAPABILITY_WORKER_THREADS])));
  applyTextureLimits();
  _modelTextures =
      std::make_unique<orblit::QueuedTextureProvider>(*_textureQueue);
  _resourceLoader->addTextureProvider("image/png", _modelTextures.get());
  _resourceLoader->addTextureProvider("image/jpeg", _modelTextures.get());
  _resourceLoader->addTextureProvider("image/ktx2", _modelTextures.get());
}

/// The largest texture and the upload budget, from the pipeline block when
/// the host sent them and from the device when it did not.
///
/// The device's own are OrblitDeviceProfile's, worked out from the same
/// measurements, so an application that never mentions textures still loads
/// them at the size its tier should — the point of measuring the device at
/// all.
void Renderer::applyTextureLimits() {
  if (!_textureQueue) return;
  const orblit::DeviceTier tier = orblit::deviceTier(
      _capabilities[ORBLIT_CAPABILITY_FEATURE_LEVEL],
      _capabilities[ORBLIT_CAPABILITY_MAX_TEXTURE_SIZE],
      _capabilities[ORBLIT_CAPABILITY_WORKER_THREADS],
      _capabilities[ORBLIT_CAPABILITY_SYSTEM_MEMORY_MEGABYTES]);
  const int32_t largest = _capabilities[ORBLIT_CAPABILITY_MAX_TEXTURE_SIZE];

  uint32_t side = std::min(
      orblit::kTierTextureSides[size_t(tier)],
      largest > 0 ? uint32_t(largest) : orblit::kTierTextureSides[0]);
  uint64_t kilobytes = 0;
  const size_t sideAt = orblit::pipeline::kTextureSide;
  const size_t uploadAt = orblit::pipeline::kTextureUploadKilobytes;
  if (_pipelineCount > uploadAt) {
    if (_pipelineParams[sideAt] >= 1.0f) {
      side = uint32_t(_pipelineParams[sideAt]);
    }
    if (_pipelineParams[uploadAt] >= 1.0f) {
      kilobytes = uint64_t(_pipelineParams[uploadAt]);
    }
  }
  _textureQueue->setTrace(_loadTrace);
  if (kilobytes > 0) {
    // The application's own, held where it put it.
    _textureQueue->setLimits(side, kilobytes * 1024);
    return;
  }
  // The device's: measured frame by frame, from where its tier starts and
  // between the least and most its tier allows.
  _textureQueue->setMaxSide(side);
  const size_t t = size_t(tier);
  _textureQueue->adaptUploads(uint64_t(orblit::kTierUploadKilobytes[t]) << 10,
                              uint64_t(orblit::kTierUploadLeastKilobytes[t]) << 10,
                              uint64_t(orblit::kTierUploadMostKilobytes[t]) << 10);
}

void Renderer::measureCapabilities(Engine::FeatureLevel supported) {
  using Format = Texture::InternalFormat;
  const auto has = [this](Format format) {
    return Texture::isTextureFormatSupported(*_engine, format);
  };
  const auto clamped = [](size_t value) {
    return int32_t(std::min<size_t>(value, size_t(INT32_MAX)));
  };

  // Families rather than single formats, and a family only when its colour
  // and its sRGB forms are both there: a cooked texture set is chosen per
  // family, and one that can hold normals but not albedo is no use to it.
  int32_t formats = 0;
  if (has(Format::ETC2_EAC_RGBA8) && has(Format::ETC2_EAC_SRGBA8)) {
    formats |= ORBLIT_FORMAT_ETC2;
  }
  if (has(Format::RGBA_ASTC_4x4) && has(Format::SRGB8_ALPHA8_ASTC_4x4)) {
    formats |= ORBLIT_FORMAT_ASTC;
  }
  if (has(Format::DXT5_RGBA) && has(Format::DXT5_SRGBA)) {
    formats |= ORBLIT_FORMAT_BC1_3;
  }
  if (has(Format::RED_RGTC1) && has(Format::RED_GREEN_RGTC2)) {
    formats |= ORBLIT_FORMAT_BC4_5;
  }
  if (has(Format::RGB_BPTC_UNSIGNED_FLOAT)) formats |= ORBLIT_FORMAT_BC6H;
  if (has(Format::RGBA_BPTC_UNORM) && has(Format::SRGB_ALPHA_BPTC_UNORM)) {
    formats |= ORBLIT_FORMAT_BC7;
  }

  _capabilities.fill(-1);
  _capabilities[ORBLIT_CAPABILITY_BACKEND] = int32_t(_backend);
  _capabilities[ORBLIT_CAPABILITY_FEATURE_LEVEL] = int32_t(supported);
  _capabilities[ORBLIT_CAPABILITY_MAX_TEXTURE_SIZE] = clamped(
      Texture::getMaxTextureSize(*_engine, Texture::Sampler::SAMPLER_2D));
  _capabilities[ORBLIT_CAPABILITY_MAX_ARRAY_TEXTURE_LAYERS] =
      clamped(Texture::getMaxArrayTextureLayers(*_engine));
  _capabilities[ORBLIT_CAPABILITY_COMPRESSED_FORMATS] = formats;
  // Sampled and mipmapped both: an environment prefiltered at run time
  // renders into its own levels, so a half-float texture that can only be
  // read is not the answer that question needs.
  _capabilities[ORBLIT_CAPABILITY_HALF_FLOAT_TEXTURES] =
      has(Format::RGBA16F) &&
              Texture::isTextureFormatMipmappable(*_engine, Format::RGBA16F)
          ? 1
          : 0;
  _capabilities[ORBLIT_CAPABILITY_WORKER_THREADS] =
      int32_t(orblit::workerThreads());
  _capabilities[ORBLIT_CAPABILITY_SYSTEM_MEMORY_MEGABYTES] =
      clamped(size_t(orblit::systemMemoryBytes() / (1024u * 1024u)));
  _capabilitiesMeasured = true;
}

int32_t Renderer::capability(orblit_capability which) const {
  if (!_capabilitiesMeasured || _engine == nullptr) return -1;
  if (which < 0 || which >= ORBLIT_CAPABILITY_COUNT) return -1;
  return _capabilities[size_t(which)];
}

bool Renderer::meshMayHaveArrived(const std::string &path) const {
  const auto found = _meshes.find(path);
  return found != _meshes.end() && found->second.asset == nullptr &&
         found->second.missingAt != orblit::resourceGeneration();
}

/// Loads a glTF or glb file, once.
///
/// Returns null and records why if it cannot be read, so the caller draws the
/// placeholder rather than nothing at all.
Mesh *Renderer::meshAtPath(const std::string &path) {
  auto found = _meshes.find(path);
  if (found != _meshes.end()) {
    if (found->second.asset) return &found->second;
    // Still missing, unless bytes have been provided since it was looked
    // for: a host that fetches a model and then names it should get the
    // model, not the answer from the frame before it arrived.
    if (found->second.missingAt == orblit::resourceGeneration()) {
      return nullptr;
    }
  }

  // Recorded either way, so a missing file is read from disk once rather than
  // on every frame of a drag.
  Mesh &entry = _meshes[path];

  const std::string &native = path;
  const double readFrom = orblit::now();
  // Taken before the read, so bytes provided while it is under way count as
  // arriving after it and are looked for again.
  const uint64_t generation = orblit::resourceGeneration();
  orblit::SharedBytes data = orblit::readResource(native);
  if (!data) {
    entry.missingAt = generation;
    orblit::log("[orblit] mesh unreadable: %s", native.c_str());
    _assetNotes[native] = "The file could not be read.";
    return nullptr;
  }

  // An FBX or an OBJ is converted to a GLB first, and from here on is one.
  // The path stays the original's, so the textures it names are looked for
  // beside it.
  std::string carried;
  if (orblit::importsAsGlb(native)) {
    data = convertedModel(native, data, carried);
    if (!data) {
      entry.missingAt = generation;
      return nullptr;
    }
  }

  const double parsedFrom = orblit::now();
  gltfio::FilamentInstance *first = nullptr;
  entry.asset = _assetLoader->createInstancedAsset(
      data->data(), static_cast<uint32_t>(data->size()), &first, 1);

  if (entry.asset == nullptr) {
    entry.missingAt = generation;
    orblit::log("[orblit] mesh not glTF: %s (%lu bytes)", native.c_str(),
               (unsigned long)data->size());
    _assetNotes[native] = "This is not a glTF file that Filament can read.";
    return nullptr;
  }
  // Whatever an earlier attempt said about it is no longer true — except what
  // converting it left behind, which is.
  _assetNotes.erase(native);
  if (!carried.empty()) _assetNotes[native] = carried;

  const double providedFrom = orblit::now();

  // The glTF's own path, so it can find the .bin and the textures sitting
  // beside it. A .glb carries everything and does not need it.
  //
  // The file, not the directory it is in. Filament takes the last component
  // off this to get the directory, so handing it a directory throws away the
  // real one: a scene at assets/bistro/Bistro.gltf looked for its textures in
  // assets/Textures, found none of the four hundred, and drew every surface
  // black. Nothing failed — loadResources still returned true — so the scene
  // rendered in the right shape with no colour in it, and in daylight at a
  // hundred thousand lux it was still black, which is what finally said this
  // was not a lighting problem.
  _resourceLoader->setConfiguration({
      .engine = _engine,
      .gltfPath = path.c_str(),
      .normalizeSkinningWeights = true,
  });

  // Begun rather than waited for.
  //
  // loadResources decodes every texture before it returns, and this scene has
  // four hundred of them — so the application stopped dead for several seconds
  // on a mesh that was, geometrically, ready almost at once. Filament will
  // decode them on its own threads instead, and the frame loop nudges it along
  // by calling asyncUpdateLoad until it says it is finished.
  //
  // What that buys is that the scene appears immediately. The geometry is
  // there on the next frame and the textures arrive over the following ones,
  // which is a scene assembling itself rather than an application that has
  // hung.
  // Which of the files it names are actually there.
  //
  // Worth doing before the load rather than trusting the result of it: the
  // loader reports success whether or not a texture opened. A scene of four
  // hundred images once failed every one of them — the base path was wrong by
  // a directory — and still returned true, so the geometry appeared with no
  // colour on it and nothing anywhere said why. Two hours of that is what
  // this loop is for.
  // For ORBLIT_LOAD_TRACE: when reading the files began, and handing them
  // over.
  double readingFrom = orblit::now();
  double handingFrom = readingFrom;
  {
    const char *const *uris = entry.asset->getResourceUris();
    const size_t count = entry.asset->getResourceUriCount();
    std::vector<std::string> sample;
    size_t missing = 0;

    // Which files this model names, resolved to where they are.
    //
    // Worked out first and read second, because reading four hundred files
    // one after another spends nearly all of its time waiting: the disk can
    // serve many at once and a single-file-at-a-time loop asks it for one.
    const std::string beside = orblit::deletingLastPathComponent(native);
    std::vector<Wanted> wanted;
    wanted.reserve(count);
    for (size_t i = 0; i < count; i++) {
      if (uris[i] == nullptr) continue;
      const std::string uri(uris[i]);
      // Data URIs carry their own bytes and embedded resources have no URI at
      // all; only a file on disk can be missing.
      if (orblit::hasPrefix(uri, "data:")) continue;

      // A glTF URI is a URI, so a space in a file name arrives as %20. The
      // path has to be the decoded form or the file is looked for under a
      // name nothing on disk has — and the answer would be "missing", which
      // is the one kind of wrong that sounds authoritative.
      const std::string name = orblit::removingPercentEncoding(uri);
      wanted.push_back(
          {uris[i], orblit::appendingPathComponent(beside, name), nullptr, 0});
    }

    // Read them all at once. The reads touch nothing shared — each writes
    // only its own slot — so this needs no lock, and the files come back in
    // whatever order the disk finds convenient.
    if (!wanted.empty()) {
      // The body captures the pointer, not the vector: capturing the vector
      // copies it, and a copy is not where the bytes are wanted.
      //
      // A KTX 2 texture is read from its cooked set: the glTF names `x.ktx2`,
      // and what is handed over under that URI is whichever sibling this
      // device samples best.
      readingFrom = orblit::now();
      Wanted *slots = wanted.data();
      orblit::TextureQueue *queue = _textureQueue.get();
      orblit::parallelFor(wanted.size(), [slots, queue](size_t i) {
        Wanted &one = slots[i];
        if (orblit::ktx2::namesCookedSet(one.path)) {
          orblit::SharedBytes cooked = queue->readCooked(one.path, nullptr);
          if (cooked && !cooked->empty()) {
            one.size = cooked->size();
            one.shared = std::move(cooked);
          }
          return;
        }
        readWholeFile(one);
      });
    }

    handingFrom = orblit::now();
    for (const std::string &line : _textureQueue->takePassedOver()) {
      orblit::log("[orblit] %s: %s", orblit::lastPathComponent(native).c_str(),
                  line.c_str());
    }
    // Handed over one at a time, because Filament is not being called from
    // several threads at once and this is not where the time was.
    for (const Wanted &one : wanted) {
      if (one.bytes == nullptr && !one.shared) {
        missing++;
        // A few names, not four hundred. The count is the number that
        // matters and the names are only there to recognise them by.
        if (sample.size() < 3) {
          sample.push_back(orblit::lastPathComponent(one.path));
        }
        continue;
      }
      // Named, so a texture that will not load is reported by its file.
      _modelTextures->nameBytes(
          one.shared ? one.shared->data() : static_cast<uint8_t *>(one.bytes),
          one.path, one.shared);
      if (one.shared) {
        // Shared, not copied: the store keeps its bytes and Filament holds a
        // reference to them until it has finished with them.
        auto *holder = new orblit::SharedBytes(one.shared);
        _resourceLoader->addResourceData(
            one.uri, filament::backend::BufferDescriptor(
                         (*holder)->data(), (*holder)->size(),
                         [](void *, size_t, void *user) {
                           delete static_cast<orblit::SharedBytes *>(user);
                         },
                         holder));
        continue;
      }
      _resourceLoader->addResourceData(
          one.uri, filament::backend::BufferDescriptor(
                       one.bytes, one.size,
                       [](void *buffer, size_t, void *) { free(buffer); }));
    }

    if (missing > 0) {
      std::string names;
      for (size_t i = 0; i < sample.size(); i++) {
        names += (i == 0 ? "" : ", ") + sample[i];
      }
      _assetNotes[native] = orblit::format(
          "%lu of its %lu files are missing, starting with "
          "%s. It will draw untextured.",
          (unsigned long)missing, (unsigned long)count, names.c_str());
      orblit::log("[orblit] %s: %s", native.c_str(), _assetNotes[native].c_str());
    }
  }

  // Every texture the load pushes belongs to this asset, so it can be
  // forgotten if the asset goes before they have all arrived.
  const double beginningFrom = orblit::now();
  const double pushedBefore = _textureQueue->pushSeconds();
  _modelTextures->setOwner(entry.asset);
  const bool began = _resourceLoader->asyncBeginLoad(entry.asset);
  if (_loadTrace) {
    orblit::log("[orblit] trace: %s's publish: reading %.0f ms, handing over "
                "%.0f ms, beginning the load %.0f ms of which pushing "
                "textures %.0f ms",
                orblit::lastPathComponent(native).c_str(),
                (handingFrom - readingFrom) * 1000.0,
                (beginningFrom - handingFrom) * 1000.0,
                (orblit::now() - beginningFrom) * 1000.0,
                (_textureQueue->pushSeconds() - pushedBefore) * 1000.0);
  }
  _modelTextures->setOwner(nullptr);
  _modelTextures->forgetNames();
  if (began) {
    _loadingAsset = entry.asset;
    entry.loadedAt = orblit::now();
    entry.shown = _textureQueue->unprimed(entry.asset) == 0;
    if (!entry.shown) _meshesWaiting = true;
  }
  if (!began) {
    orblit::log("[orblit] mesh resources failed: %s", native.c_str());
    _assetNotes[native] = "Its geometry or textures could not be loaded.";
  } else {
    _loadingResources = true;
    // What the load cost, in the three parts it is actually made of.
    //
    // "It takes a few seconds" is not a thing anybody can act on: reading the
    // file, parsing it, and decoding its textures are three different costs
    // with three different fixes, and until they are separated the only
    // available move is to guess. Printed rather than measured on request
    // because a load happens once and the number is wanted the first time,
    // not after somebody has reproduced it.
    _loadingName = native;
    _loadingResourceCount = entry.asset->getResourceUriCount();
    _loadingFrom = orblit::now();
    orblit::log("[orblit] %s: read %.0f ms, parsed %.0f ms, %zu files handed over "
               "in %.0f ms",
               orblit::lastPathComponent(native).c_str(),
               (parsedFrom - readFrom) * 1000,
               (providedFrom - parsedFrom) * 1000, _loadingResourceCount,
               (_loadingFrom - providedFrom) * 1000);
  }

  // What the file holds and how its nodes stand, taken before anything moves
  // them — and after the load has begun, which is when gltfio makes the
  // animator the clips are read from. Its lights are described before they
  // are taken out.
  readModel(entry, first);
  describeModel(entry, first, native, data->data(), data->size());
  dropFileLights(first);

  // Deliberately not calling releaseSourceData: more instances can only be
  // made while it is still there, and a second object using this mesh is the
  // ordinary case rather than the exception.
  entry.all.push_back(first);
  entry.spare.push_back(first);
  return &entry;
}

/// A copy of a mesh to give an object, from the pool if one is spare.
gltfio::FilamentInstance *Renderer::takeInstanceOf(Mesh *mesh) {
  if (!mesh->spare.empty()) {
    auto *spare = mesh->spare.back();
    mesh->spare.pop_back();
    return spare;
  }
  auto *extra = _assetLoader->createInstance(mesh->asset);
  // A refusal means no more instances are possible; the object falls back to
  // the placeholder rather than vanishing.
  if (extra == nullptr) return nullptr;
  dropFileLights(extra);
  mesh->all.push_back(extra);
  return extra;
}

/// Takes an object out of the scene, keeping whatever can be used again.
void Renderer::recycle(Drawn &drawn) {
  // Before anything else: the prepass entity is this object's and nothing
  // else points at it. Its material is the shared depth-only instance, which
  // is the engine's and stays.
  dropPrepass(drawn);
  if (drawn.instance != nullptr) {
    // Back onto the materials the file brought with it, before it goes in the
    // pool. An instance pooled while still pointing at an overriding material
    // outlives that material — the material is swept the moment nothing is
    // made of it — and the next object to take the instance out draws with a
    // pointer to something destroyed. Which is a crash, and the way to get
    // one is to turn a material off and on again.
    if (!drawn.ownMaterials.empty()) {
      dress(drawn, -1);
      drawn.ownMaterials.clear();
    }
    // And back in the file's own pose and materials, for the same reason: the
    // next object to take this copy out expects the model the file describes,
    // not one stopped mid-stride in another object's variant.
    restPose(drawn);
    wearVariant(drawn, -1);
    drawn.surface = -2;
    _scene->removeEntities(drawn.instance->getEntities(),
                           drawn.instance->getEntityCount());
    auto found = _meshes.find(drawn.path);
    if (found != _meshes.end()) found->second.spare.push_back(drawn.instance);
    drawn.instance = nullptr;
  }
  if (drawn.entity) {
    _scene->remove(drawn.entity);
    _engine->destroy(drawn.entity);
    utils::EntityManager::get().destroy(drawn.entity);
    drawn.entity = utils::Entity();
  }
  if (drawn.material != nullptr) {
    _engine->destroy(drawn.material);
    drawn.material = nullptr;
  }
}

/// Empties the scene of everything a host put in it.
void Renderer::removeEverything() {
  for (auto &pair : _drawn) recycle(pair.second);
  _drawn.clear();
  _posed.clear();
  _varied.clear();

  // The groups, which wear pooled colour instances exactly as the objects
  // above do. They did not exist when this function was written and nothing
  // here took them down, so a renderer disposed with a group alive destroyed
  // a MaterialInstance that a chunk renderable still pointed at — which
  // Filament refuses outright, with "destroying MaterialInstance which is
  // still in use by Renderable", and then aborts. It was reachable before
  // only by a host that turned batching on and then closed its window; with
  // batching on by default it is the ordinary way every scene shuts down.
  for (auto &pair : _groups) destroyBatchGroup(pair.second);
  _groups.clear();
  _batchedObjects = 0;
  _batchGroups = 0;

  // After the objects and their groups, which were the only things wearing
  // these.
  _colourPool.clear([this](MaterialInstance *spent) { _engine->destroy(spent); });

  auto &entities = utils::EntityManager::get();
  for (auto &pair : _lit) {
    if (!pair.second.entity) continue;
    _scene->remove(pair.second.entity);
    _engine->destroy(pair.second.entity);
    entities.destroy(pair.second.entity);
  }
  _lit.clear();
}

/// Applies the shadow and visibility flags to one renderable.
void Renderer::applyFlags(int32_t flags, utils::Entity entity) {
  auto &renderables = _engine->getRenderableManager();
  auto instance = renderables.getInstance(entity);
  // Not every entity in a glTF file is renderable — a joint or an empty
  // carries no geometry — so the ones without a component are skipped.
  if (!instance) return;
  renderables.setCastShadows(instance, (flags & kCastsShadows) != 0);
  renderables.setReceiveShadows(instance, (flags & kReceivesShadows) != 0);
  // Contact shadows are asked for twice in Filament: by the light, and by
  // every surface that is to receive them. Without the second the light's
  // switch does nothing at all — measured: the frame was byte-identical with
  // it on and off. Every receiver says yes here, so the pipeline's contact
  // switch is the one that decides; with it off, no light marches anything.
  renderables.setScreenSpaceContactShadows(
      instance, (flags & kReceivesShadows) != 0);
  renderables.setLayerMask(
      instance, 0xFF, (flags & kVisible) ? layerBitOf(flags) : kHiddenLayer);
}

/// Dials a mesh's shapes in, on every renderable the model is made of.
///
/// A glTF's morph targets belong to its primitives, and one model is usually
/// several — so the weights go to each of them rather than to the asset. A
/// renderable that has no shapes is skipped rather than refused: a scene that
/// sets a weight on the wrong object should do nothing, not stop.
void Renderer::morph(const Drawn &drawn, const float *weights, size_t count) {
  if (drawn.instance == nullptr || count == 0) return;

  auto &renderables = _engine->getRenderableManager();
  const utils::Entity *entities = drawn.instance->getEntities();
  const size_t parts = drawn.instance->getEntityCount();

  for (size_t part = 0; part < parts; part++) {
    auto instance = renderables.getInstance(entities[part]);
    if (!instance) continue;

    // Filament refuses more weights than the primitive was built with, and
    // that is a precondition rather than an error code — it takes the process
    // with it. A model with four shapes told about six gets four.
    const size_t room = renderables.getMorphTargetCount(instance);
    if (room == 0) continue;
    renderables.setMorphWeights(instance, weights, std::min(count, room), 0);
  }
}

/// Applies them to a whole object, which for a mesh is every part of it.
void Renderer::applyFlags(int32_t flags, const Drawn &drawn) {
  // Hidden while its textures' memory is made; shown by showPrimedMeshes.
  if (drawn.mesh != nullptr && !drawn.mesh->shown) flags &= ~kVisible;
  if (drawn.instance != nullptr) {
    const utils::Entity *entities = drawn.instance->getEntities();
    const size_t count = drawn.instance->getEntityCount();
    for (size_t i = 0; i < count; i++) {
      applyFlags(flags, entities[i]);
    }
    return;
  }
  applyFlags(flags, drawn.entity);
}

/// Builds one object: a mesh instance if it names a file that loads, and the
/// placeholder cube otherwise.
void Renderer::build(Drawn &drawn, const std::string &path) {
  drawn.path = path;

  if (!path.empty()) {
    Mesh *mesh = meshAtPath(path);
    if (mesh != nullptr) drawn.instance = takeInstanceOf(mesh);
    if (drawn.instance != nullptr) {
      drawn.mesh = mesh;
      _scene->addEntities(drawn.instance->getEntities(),
                          drawn.instance->getEntityCount());
      // A skin's bones as its nodes stand, rather than gltfio's starting
      // identity. Identity draws the mesh as it was bound, which is not
      // always the pose the file rests in — a character bound in a T and
      // saved standing in an A draws in the T until something animates it —
      // and it is also not what putting an animated copy back at rest gives,
      // so the two would differ.
      if (drawn.instance->getSkinCount() > 0) {
        if (auto *animator = drawn.instance->getAnimator()) {
          animator->updateBoneMatrices();
        }
      }
      // Something new is made of this file, so the host hears what the file
      // holds — once per object built rather than on every publish.
      if (!mesh->info.empty()) {
        _modelInfo[std::string(kModelInfoPrefix) + path] = mesh->info;
      }
      return;
    }
    // Fell through: the file is missing or unreadable, so the object is drawn
    // as the placeholder cube. Somewhere visible beats nowhere.
  }

  // One material instance per object, because the colour is a parameter on it
  // and sharing would make every object the last one's colour.
  drawn.material = surfaceAt(0)->createInstance();
  setDefaultsOn(drawn.material);

  drawn.entity = utils::EntityManager::get().create();
  RenderableManager::Builder(1)
      // Filament's Box is a centre and a half-extent, not a minimum and a
      // maximum. This used to say {{-1,-1,-1},{1,1,1}}, which reads as a
      // {min,max} pair and declares a cube centred on (-1,-1,-1) reaching the
      // origin — a box that does not contain the geometry it stands for, since
      // kPositions spans -1..+1 about the origin. Filament fits the directional
      // shadow camera to the casters' world boxes (ShadowMap::visitScene, then
      // computeLightFrustumBounds) and culls from the same box, so a box in the
      // wrong place moved every shadow in every scene that draws the
      // placeholder cube. The batched path works its own world box out from the
      // members' transforms and was always right; this is what makes the two
      // agree, and agree on the correct answer rather than in the middle.
      .boundingBox({{0, 0, 0}, {1, 1, 1}})
      .material(0, drawn.material)
      .geometry(0, RenderableManager::PrimitiveType::TRIANGLES, _vertexBuffer,
                _indexBuffer, 0, 36)
      .receiveShadows(true)
      .castShadows(true)
      .build(*_engine, drawn.entity);
  _scene->addEntity(drawn.entity);
}

double Renderer::gpuMilliseconds() {
  if (_disposed) return 0;

  const auto history = _renderer->getFrameInfoHistory(16);
  std::vector<double> costs;
  costs.reserve(history.size());

  for (const auto &frame : history) {
    if (frame.gpuFrameDuration > 0) {
      costs.push_back(double(frame.gpuFrameDuration) / 1.0e6);
    }
  }

  if (costs.empty()) return 0;
  std::sort(costs.begin(), costs.end());
  return costs[costs.size() / 2];
}

/// What recent frames cost this renderer on the CPU, in milliseconds.
///
/// The other half of the answer. A frame has two costs and they fail
/// differently: the GPU number moves when the picture gets more expensive to
/// draw, and this one moves when the renderer gets more expensive to *drive* —
/// a scene reconciled less carefully, an allocation per frame that was not
/// there before, work done per object that used to be done per scene. A change
/// that leaves the picture identical can double this and never touch the GPU.
///
/// Filament already records beginFrame and endFrame, so this costs nothing to
/// collect. Median rather than mean, for the same reason as the GPU number: a
/// mean is dragged about by the one frame in thirty that hit a hitch, and what
/// anybody wants to know is what a frame usually costs.
double Renderer::cpuMilliseconds() {
  if (_disposed) return 0;

  const auto history = _renderer->getFrameInfoHistory(16);
  std::vector<double> costs;
  costs.reserve(history.size());

  for (const auto &frame : history) {
    // Both ends have to be real. A frame still in flight reports PENDING, and
    // treating that as a timestamp gives a duration of minus several years.
    if (frame.beginFrame > 0 && frame.endFrame > frame.beginFrame) {
      costs.push_back(double(frame.endFrame - frame.beginFrame) / 1.0e6);
    }
  }

  if (costs.empty()) return 0;
  std::sort(costs.begin(), costs.end());
  return costs[costs.size() / 2];
}
}  // namespace orblit
