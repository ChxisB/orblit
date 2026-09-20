#include "OrblitRendererInternal.h"

// The compiled surfaces, and everything a material instance is dressed
// with: textures, samplers and video.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

/// Which compiled surface a set of flags asks for: shading first, then blend
/// mode.
///
/// The shadow catcher sits outside that grid rather than adding a fourth row
/// to it. Its blending is not a choice — a surface that is only its own
/// shadow is see-through by definition — so five variants of it would be four
/// packages compiled to be unreachable.
int Renderer::surfaceIndexFor(int32_t flags) {
  const int shading = flags & 3;
  const int blend = (flags >> 2) & 15;
  if (shading == 3) return kShadowCatcherSurface;
  if (shading < 0 || shading > 2 || blend < 0 || blend > 4) return 0;
  return shading * 5 + blend;
}

/// Builds a surface the first time something is made of it.
///
/// Ten compiled packages rather than one, because blending is the only thing
/// about a material that a uniform cannot change — and lazily, because a
/// scene of opaque lit objects should not compile the four blending variants
/// it never draws.
Material *Renderer::surfaceAt(int index) {
  if (index < 0 || index >= kSurfaceCount) index = 0;
  if (_surfaces[index] == nullptr) {
    const uint8_t *package = nullptr;
    size_t length = 0;
    surfacePackage(index, _slimSurface, &package, &length);
    _surfaces[index] =
        Material::Builder().package(package, length).build(*_engine);
  }
  return _surfaces[index];
}

/// Compiles one of the single-purpose materials.
Material *Renderer::materialFrom(Package which) {
  const uint8_t *package = nullptr;
  size_t length = 0;
  materialPackage(which, &package, &length);
  return Material::Builder().package(package, length).build(*_engine);
}

/// Hands a compiled material to the shader compiler before anything draws
/// with it.
///
/// Filament builds a material's GPU programs lazily and one variant at a
/// time, on the draw that first needs each — which puts a shader compile in
/// the middle of a frame, and a shader compile is milliseconds. A scene that
/// introduces eight materials pays that eight times over its first second,
/// which is exactly the second somebody is looking at it.
///
/// Asking at publish time spends the same work where no frame is being timed.
/// It is not free and it is not instant: the backend compiles on its own
/// thread and the callers flush afterwards so the commands actually leave.
///
/// Variance shadow maps and instanced stereo are left out because this
/// renderer configures neither, so their programs could only be compiled to
/// be unreachable. Everything else the renderer can turn on — the sun,
/// punctual lights, shadow receivers, skins, fog, screen-space reflections —
/// is included, because a variant left out here is a stall put back.
void Renderer::warmUp(Material *material) {
  if (material == nullptr) return;
  constexpr filament::UserVariantFilterMask kReachable =
      static_cast<filament::UserVariantFilterMask>(
          filament::UserVariantFilterBit::ALL) &
      ~static_cast<filament::UserVariantFilterMask>(
          filament::UserVariantFilterBit::VSM) &
      ~static_cast<filament::UserVariantFilterMask>(
          filament::UserVariantFilterBit::STE);
  // LOW, not HIGH: HIGH is "this draw is waiting on it", and on a platform
  // without parallel compilation it is compiled synchronously. Nothing is
  // waiting on these yet, and the whole point is to not block.
  material->compile(filament::backend::CompilerPriorityQueue::LOW, kReachable);
}

/// A single white pixel, for every sampler a material leaves empty.
///
/// Filament requires every sampler in a material to be bound whether the
/// shader reads it or not, and an unbound one is undefined rather than
/// ignored. One texture stands in for all of them; the `has` flag beside it
/// is what actually decides whether it is read.
Texture *Renderer::blankTexture() {
  if (_blankTexture != nullptr) return _blankTexture;
  _blankTexture = Texture::Builder()
                      .width(1)
                      .height(1)
                      .levels(1)
                      .format(Texture::InternalFormat::RGBA8)
                      .build(*_engine);
  uint8_t *pixel = new uint8_t[4]{255, 255, 255, 255};
  _blankTexture->setImage(
      *_engine, 0,
      Texture::PixelBufferDescriptor(
          pixel, 4, Texture::Format::RGBA, Texture::Type::UBYTE,
          [](void *buffer, size_t, void *) {
            delete[] static_cast<uint8_t *>(buffer);
          }));
  return _blankTexture;
}

/// Fills in every parameter of the standard surface with what a material
/// that says nothing would have.
///
/// Needed because Filament requires every sampler to be bound whether the
/// shader reads it or not — an object drawn in a plain colour still has five
/// maps, all of them the blank one, all of them switched off.
void Renderer::setDefaultsOn(MaterialInstance *instance) {
  Texture *blank = blankTexture();
  TextureSampler sampler(TextureSampler::MinFilter::LINEAR_MIPMAP_LINEAR,
                         TextureSampler::MagFilter::LINEAR);
  sampler.setAnisotropy(8.0f);
  instance->setParameter("baseColor", float4{0.8f, 0.8f, 0.8f, 1.0f});
  instance->setParameter("metallic", 0.0f);
  instance->setParameter("roughness", 0.4f);
  instance->setParameter("reflectance", 0.5f);
  instance->setParameter("emissive", float3{0.0f, 0.0f, 0.0f});
  instance->setParameter("emissiveIntensity", 0.0f);
  instance->setParameter("ambientOcclusion", 1.0f);
  instance->setParameter("normalScale", 1.0f);
  instance->setParameter("uvTransform", float4{1.0f, 1.0f, 0.0f, 0.0f});

  // No coat, no grain, no sheen — said explicitly, for the same reason as
  // the blend below: undefined is not nought, and a surface that came up
  // varnished because nobody said otherwise is a hard fault to place.
  instance->setParameter("clearCoat", 0.0f);
  instance->setParameter("clearCoatRoughness", 0.1f);
  instance->setParameter("anisotropy", 0.0f);
  instance->setParameter("sheenColor", float3{0.0f, 0.0f, 0.0f});
  instance->setParameter("sheenRoughness", 0.3f);
  instance->setParameter("wind", float4{0.0f, 0.0f, 0.0f, 0.0f});

  // Not blending, said explicitly. A material declares these whether or not
  // it uses them, and one left unset is undefined rather than nought.
  instance->setParameter("blendMode", int32_t{0});
  instance->setParameter("blendAmount", 0.0f);
  instance->setParameter("blendSharpness", 8.0f);
  instance->setParameter("blendUvTransform", float4{1.0f, 1.0f, 0.0f, 0.0f});

  // Every map, from the one list. A sampler a material declares and nobody
  // binds is reported on every draw — and the report is right: what it would
  // sample is undefined.
  for (size_t i = 0; i < kMaterialMaps; i++) {
    instance->setParameter(kMapNames[i], blank, sampler);
    instance->setParameter(kMapFlags[i], false);
  }

  // The rectangular area lights, which only a lit surface shades — full or
  // slim alike, and this runs for both. Bound once and never again: the
  // texture outlives every surface that reads it, because the tables never
  // change and the lights are rewritten in place.
  buildLtcTables();
  const TextureSampler tables(TextureSampler::MinFilter::LINEAR,
                              TextureSampler::MagFilter::LINEAR,
                              TextureSampler::WrapMode::CLAMP_TO_EDGE);
  // Filtered, which the fitted tables need. The rectangles in the rows below
  // are read with texelFetch, which ignores the sampler entirely, so they are
  // not interpolated into each other by sharing this one — nor, on the slim
  // surface, are the decal rows below them.
  instance->setParameter("lightData", _lightData, tables);

  // Decals, and the layer the surface is on — layer nought until an object
  // says otherwise, which is where every object that never mentions layers
  // lives. Both surfaces bind this: the slim one keeps the picture array and
  // reads its boxes out of lightData's own tail instead of a sampler of its
  // own — see bindDecalsTo.
  bindDecalsTo(instance);
  instance->setParameter("decalLayer", int32_t{1});

  if (_slimSurface) {
    // Nine samplers were spent above: five maps, two for ground blending,
    // lightData and decalImages. There is no tenth to give areaShadow or
    // fieldAtlas, so this material does not declare them and nothing past
    // here binds anything on it. What that costs a scene is said once in
    // notes(), and per-light in applyLights when one actually asks to cast.
    return;
  }

  // The rectangle's depth map. Built here if it does not exist yet rather
  // than left unbound: Filament reports a declared sampler nobody bound on
  // every draw, and it is right to — what it would read is undefined. A
  // scene with no casting rectangle still binds it and never looks at it,
  // because the flag in the light data is nought.
  buildAreaShadow();
  // Nearest, not linear. The lookup does its own filtering, and a linear tap
  // between two depths is a distance at which nothing stands; OpenGL ES also
  // refuses to filter a depth texture that has no comparison mode, and reads
  // it as nought — which here would be a shadow that silently never appears.
  const TextureSampler shadowSampler(TextureSampler::MinFilter::NEAREST,
                                     TextureSampler::MagFilter::NEAREST,
                                     TextureSampler::WrapMode::CLAMP_TO_EDGE);
  instance->setParameter("areaShadow", _areaShadow, shadowSampler);

  bindFieldTo(instance);
}

/// How much of the field reaches surfaces, held below where it feeds itself.
///
/// Reported rather than silently substituted: a host that asks for six and
/// quietly gets three has a scene that does not match its reference and no
/// way to find out why.
float Renderer::fieldStrength() {
  const float asked = _fieldParams[10];
  const float most = kFieldSafeGain / kFieldDamping;
  if (asked <= most) {
    _assetNotes.erase("fieldStrength");
    return asked;
  }
  _assetNotes["fieldStrength"] = orblit::format(
      "An irradiance field at a strength of %.1f feeds "
      "itself: it reads the picture it brightened, so the "
      "light goes round and drifts in hue rather than "
      "settling. Held at %.1f.",
      asked, most);
  return most;
}

/// Points every lit surface at the atlas holding this frame's answer.
///
/// Every frame, and it has to be: the two atlases are written in turn, so
/// which of them holds the answer changes with them, and a surface left
/// pointing at the one being written would read what is half-built. Cheap
/// because it is a handful of parameters over the surfaces that exist, and
/// skipped entirely by a scene with no field.
void Renderer::bindFieldEverywhere() {
  if (_fieldProbes == 0 || _fieldParams[0] <= 0.0f) return;
  for (auto &entry : _drawn) {
    if (entry.second.material != nullptr) {
      bindFieldTo(entry.second.material);
    }
  }
  // The batched cubes' shared surfaces are lit surfaces too. Left out, a
  // batched crate would read no bounced light, and batching would be visible.
  _colourPool.forEach([this](MaterialInstance *shared) { bindFieldTo(shared); });
  for (auto &entry : _materials) {
    if (entry.second.instance == nullptr) continue;
    if ((entry.second.flags & 3) != 0) continue;
    bindFieldTo(entry.second.instance);
  }
}

/// Gives one lit surface the field to read.
///
/// Every frame rather than once, because the two atlases are written in turn
/// and which of them holds the answer changes with them. A surface left
/// pointing at the one being written would read what is half-built.
void Renderer::bindFieldTo(MaterialInstance *instance) {
  const TextureSampler smooth(TextureSampler::MinFilter::LINEAR,
                              TextureSampler::MagFilter::LINEAR,
                              TextureSampler::WrapMode::CLAMP_TO_EDGE);
  Texture *atlas = _fieldAtlas[_fieldFront];
  const bool on =
      atlas != nullptr && _fieldProbes > 0 && _fieldParams[0] > 0.0f;
  // Bound whether or not there is a field: Filament refuses to draw a
  // material with a sampler nobody filled.
  instance->setParameter("fieldAtlas", on ? atlas : blankTexture(),
                         smooth);
  instance->setParameter(
      "fieldOrigin", float4{_fieldParams[1], _fieldParams[2], _fieldParams[3],
                            on ? 1.0f : 0.0f});
  instance->setParameter("fieldSpacing",
                         float4{_fieldParams[4], _fieldParams[5],
                                _fieldParams[6], fieldStrength()});
  instance->setParameter("fieldCounts",
                         float4{_fieldParams[7], _fieldParams[8],
                                _fieldParams[9], float(kFieldTilesPerRow)});
  const uint32_t rows =
      (std::max(_fieldProbes, 1u) + kFieldTilesPerRow - 1) / kFieldTilesPerRow;
  const float wide = float(kFieldTilesPerRow * kFieldTile);
  const float tall = float(std::max(rows, 1u) * kFieldTile);
  instance->setParameter(
      "fieldAtlasStep",
      float4{1.0f / wide, 1.0f / tall, _fieldParams[12], 0.0f});
}

/// Loads an image, or hands back the one already loaded for that path.
///
/// The colour space is part of the identity: the same file read as sRGB and
/// as linear are two different textures, and a normal map decoded as though
/// it were a colour bends every normal towards flat.
Texture *Renderer::textureAtPath(const std::string &path, bool srgb) {
  std::string identity = path + (srgb ? "|s" : "|l");

  // What a pass drew, rather than a file. Looked up every time rather than
  // cached: the target behind a name is rebuilt whenever the view is resized,
  // and a material still holding the old texture would be sampling something
  // the engine has destroyed.
  const std::string &wanted = path;
  if (wanted.rfind(kTargetScheme, 0) == 0) {
    return targetTextureNamed(wanted.substr(strlen(kTargetScheme)));
  }

  auto found = _ownTextures.find(identity);
  if (found != _ownTextures.end()) {
    if (found->second != nullptr) return found->second;
    const auto missing = _texturesMissingAt.find(identity);
    if (missing != _texturesMissingAt.end() &&
        missing->second == orblit::resourceGeneration()) {
      return nullptr;
    }
  }

  // A failure is cached as null too. Forty objects naming a file that is not
  // there would otherwise each read the disk, every frame, forever — until
  // bytes are provided, the one thing that can change the answer.
  const uint64_t generation = orblit::resourceGeneration();
  Texture *texture = nullptr;
  // A cooked set is chosen from here: `x.ktx2` is the best sibling this
  // device samples, or itself.
  std::string chosen;
  const orblit::SharedBytes cooked =
      _textureQueue->readCooked(path, &chosen, srgb ? 1 : 0);
  for (const std::string &line : _textureQueue->takePassedOver()) {
    orblit::log("[orblit] %s", line.c_str());
  }
  if (const orblit::SharedBytes data = cooked) {
    orblit::TextureQueue::Request request;
    request.shared = data;
    request.srgb = srgb;
    request.name = chosen;
    request.client = this;
    std::string why;
    // Usable now, holding transparent black until its levels arrive; see
    // OrblitTextures.h for what a texture shows while they do.
    texture = _textureQueue->push(request, why);
    if (texture == nullptr) {
      _textureNotes[path] = "This texture could not be loaded: " + why;
      _textureNotedFor[path].clear();
      orblit::log("[orblit] texture %s refused: %s", chosen.c_str(),
                  why.c_str());
    } else {
      _textureNotes.erase(path);
    }
  }
  _ownTextures[identity] = texture;
  if (texture == nullptr) _texturesMissingAt[identity] = generation;
  return texture;
}

/// Takes what has finished arriving off the queue, and what went wrong.
///
/// The levels themselves were uploaded by pumpTextures; this is only what the
/// renderer hears about it — which for a texture that arrived whole is
/// nothing, and for one that did not is a note.
void Renderer::pollTextures() {
  if (!_textureQueue) return;
  orblit::TextureQueue::Popped popped;
  while (_textureQueue->pop(this, popped)) {
    if (popped.failure.empty()) continue;
    // Named by the file actually read, which for a cooked set is the
    // sibling; noted against the path the scene asked for.
    std::string asked = popped.name;
    for (const auto &entry : _ownTextures) {
      if (entry.second == popped.texture) {
        asked = entry.first.substr(0, entry.first.size() - 2);
        break;
      }
    }
    _textureNotes[asked] = "This texture arrived only in part: " +
                           popped.failure + " It draws at the levels that did.";
    _textureNotedFor[asked].clear();
  }
  for (const auto &note : _modelTextures->takeNotes()) {
    std::string model;
    for (const auto &mesh : _meshes) {
      if (mesh.second.asset == note.owner) model = mesh.first;
    }
    // An image inside the model has no file of its own to be about.
    const std::string about =
        note.texture.empty() ? model + " (an embedded image)" : note.texture;
    _textureNotes[about] = "This texture could not be loaded: " + note.sentence;
    _textureNotedFor[about] = model;
  }
}

/// Uploads the texture levels that have been decoded, under this frame's
/// budget. Before the resource loader looks, so a model's textures that
/// finish this frame are popped this frame.
void Renderer::pumpTextures() {
  if (!_textureQueue) return;
  _textureQueue->pump();
  if (_meshesWaiting) showPrimedMeshes();
}

/// Draws the models whose every texture is now safe to sample.
void Renderer::showPrimedMeshes() {
  _meshesWaiting = false;
  for (auto &entry : _meshes) {
    Mesh &mesh = entry.second;
    if (mesh.shown || mesh.asset == nullptr) continue;
    if (_textureQueue->unprimed(mesh.asset) > 0) {
      _meshesWaiting = true;
      continue;
    }
    mesh.shown = true;
    orblit::log("[orblit] %s: drawn once its textures were safe to sample, "
                "%.0f ms into the load",
                orblit::lastPathComponent(entry.first).c_str(),
                (orblit::now() - mesh.loadedAt) * 1000.0);
    for (auto &pair : _drawn) {
      Drawn &drawn = pair.second;
      if (drawn.mesh == &mesh && drawn.flags != -1) {
        applyFlags(drawn.flags, drawn);
      }
    }
  }
}

/// Builds the sampler a material's wrap and filter settings describe.
TextureSampler Renderer::samplerFor(int32_t flags) {
  const int wrap = (flags >> 10) & 3;
  const bool sharp = ((flags >> 12) & 1) != 0;
  TextureSampler sampler(
      sharp ? TextureSampler::MinFilter::NEAREST
            : TextureSampler::MinFilter::LINEAR_MIPMAP_LINEAR,
      sharp ? TextureSampler::MagFilter::NEAREST
            : TextureSampler::MagFilter::LINEAR);
  TextureSampler::WrapMode mode = TextureSampler::WrapMode::REPEAT;
  if (wrap == 1) mode = TextureSampler::WrapMode::CLAMP_TO_EDGE;
  if (wrap == 2) mode = TextureSampler::WrapMode::MIRRORED_REPEAT;
  sampler.setWrapModeS(mode);
  sampler.setWrapModeT(mode);
  // Anisotropy, unless the texture asked to be sharp.
  //
  // What it fixes is ground seen at a glancing angle, which is most of what a
  // camera at head height sees: a road or a floor stretching away is sampled
  // across a long thin footprint, and a mipmap chain can only pick one level
  // for it. Too fine and it crawls, too coarse and it is mud a few metres
  // out. Eight samples is the usual place to stop — past that the cost keeps
  // climbing and nobody can see the difference.
  if (!sharp) sampler.setAnisotropy(8.0f);
  return sampler;
}

/// Writes everything about one material into its instance.
void Renderer::write(Surfaced &surface, const float *params, const int32_t *maps, const std::vector<std::string> &texturePaths, const int32_t *textureSrgb, int32_t video) {
  MaterialInstance *instance = surface.instance;
  const int shading = surface.flags & 3;
  const bool unlit = shading == 1;
  const TextureSampler sampler = samplerFor(surface.flags);

  // A catcher has one parameter and no maps. Everything else the writer
  // sets below would be a parameter this material does not declare, and
  // Filament treats that as a mistake rather than ignoring it.
  if (shading == 3) {
    instance->setParameter("baseColor",
                           float4{params[0], params[1], params[2], params[3]});
    return;
  }

  // A screen has its own short list: a tint, a transform, and the frame.
  if (shading == 2) {
    instance->setParameter("baseColor",
                           float4{params[0], params[1], params[2], params[3]});
    instance->setParameter(
        "uvTransform", float4{params[13], params[14], params[15], params[16]});
    Movie *movie = (video >= 0 && video < static_cast<int32_t>(_movieOrder.size()))
                       ? _movieOrder[video]
                       : nullptr;
    Texture *frame = movie != nullptr ? movie->texture : nullptr;
    // An external image only ever clamps, and only ever filters linearly.
    // Asking for anything else is not refused, it is ignored.
    const TextureSampler screen(TextureSampler::MinFilter::LINEAR,
                                TextureSampler::MagFilter::LINEAR,
                                TextureSampler::WrapMode::CLAMP_TO_EDGE);
    if (frame == nullptr) {
      if (_blankExternal == nullptr) {
        _blankExternal = Texture::Builder()
                             .sampler(Texture::Sampler::SAMPLER_EXTERNAL)
                             .format(Texture::InternalFormat::RGBA8)
                             .build(*_engine);
      }
      frame = _blankExternal;
    }
    instance->setParameter("videoTexture", frame, screen);
    instance->setParameter("hasVideo", movie != nullptr && movie->texture != nullptr);
    return;
  }

  instance->setParameter("baseColor",
                         float4{params[0], params[1], params[2], params[3]});
  if (unlit) {
    // Projected from the camera rather than wrapped on the surface, which is
    // what makes a reflection target a mirror instead of a decal.
    instance->setParameter("screenMapped",
                           ((surface.flags >> 13) & 1) != 0);
  }
  instance->setParameter("emissive", float3{params[7], params[8], params[9]});
  instance->setParameter("emissiveIntensity", params[10]);
  instance->setParameter(
      "uvTransform", float4{params[13], params[14], params[15], params[16]});

  if (!unlit) {
    instance->setParameter("metallic", params[4]);
    instance->setParameter("roughness", params[5]);
    instance->setParameter("reflectance", params[6]);
    instance->setParameter("ambientOcclusion", params[11]);
    instance->setParameter("normalScale", params[12]);

    // The three extra lobes. Every one is nought by default, so a material
    // that asked for none is shaded as though they did not exist — but they
    // still have to be pushed, because an instance keeps whatever it was last
    // given and a surface that stopped being varnished would otherwise stay
    // varnished for the rest of its life.
    instance->setParameter("clearCoat", params[26]);
    instance->setParameter("clearCoatRoughness", params[27]);
    instance->setParameter("anisotropy", params[28]);
    instance->setParameter("sheenColor",
                           float3{params[29], params[30], params[31]});
    instance->setParameter("sheenRoughness", params[32]);

    // Wind. Direction on the ground, speed, and how much this surface
    // answers — the last is nought for anything rigid, which is the early
    // return in the vertex stage and therefore the cost of this feature for
    // every surface that does not use it.
    instance->setParameter(
        "wind", float4{params[33], params[34], params[35], params[36]});

    // The second surface. Only the lit material declares these, which is why
    // they are inside this branch rather than beside baseColor — Filament
    // treats a parameter a material has not declared as a mistake rather
    // than ignoring it.
    instance->setParameter("blendMode", static_cast<int32_t>(params[19]));
    instance->setParameter("blendAmount", params[20]);
    instance->setParameter("blendSharpness", params[21]);
    instance->setParameter(
        "blendUvTransform",
        float4{params[22], params[23], params[24], params[25]});
  }

  // kMapNames and kMapFlags are in the order the Dart side packs them. Only
  // the first is set for an unlit surface, which has nothing to do with the
  // rest.

  // What a pass drew has one level and is never tiled, so it is bound with a
  // sampler of its own rather than the material's.
  //
  // Not a nicety. The ordinary sampler asks for LINEAR_MIPMAP_LINEAR, and
  // minifying a texture that has no mips through it is undefined — which on
  // Metal comes out as flat magenta across the whole surface, with nothing
  // logged. A mirror that is entirely the missing-texture colour is a long
  // afternoon if the sampler is not the first place you look.
  const TextureSampler drawn(TextureSampler::MinFilter::LINEAR,
                             TextureSampler::MagFilter::LINEAR,
                             TextureSampler::WrapMode::CLAMP_TO_EDGE);

  const size_t count = unlit ? 1 : kMaterialMaps;
  for (size_t i = 0; i < count; i++) {
    Texture *texture = nullptr;
    bool fromPass = false;
    const int32_t index = maps[i];
    if (index >= 0 && index < static_cast<int32_t>(texturePaths.size())) {
      fromPass = orblit::hasPrefix(texturePaths[index], kTargetScheme);
      texture = textureAtPath(texturePaths[index], textureSrgb[index] != 0);
    }
    const bool present = texture != nullptr;
    instance->setParameter(kMapNames[i],
                           present ? texture : blankTexture(),
                           present && fromPass ? drawn : sampler);
    instance->setParameter(kMapFlags[i], present);

    if (fromPass) {
      const std::string path(texturePaths[index]);
      _targetBindings.push_back({instance, kMapNames[i],
                                 path.substr(strlen(kTargetScheme))});
    }
  }
}

/// Sets up the parts of a material that are rasteriser state rather than
/// shader input.
void Renderer::applyRasterState(Surfaced &surface, float threshold, float bias) {
  MaterialInstance *instance = surface.instance;
  const int culling = (surface.flags >> 6) & 3;
  const bool doubleSided = ((surface.flags >> 8) & 1) != 0;
  const bool depthWrite = ((surface.flags >> 9) & 1) != 0;

  // Order matters: turning double-sided lighting on disables culling as a
  // side effect, so the culling mode is set afterwards and wins.
  instance->setDoubleSided(doubleSided);
  MaterialInstance::CullingMode mode = MaterialInstance::CullingMode::BACK;
  if (culling == 1) mode = MaterialInstance::CullingMode::FRONT;
  if (culling == 2 || doubleSided) mode = MaterialInstance::CullingMode::NONE;
  instance->setCullingMode(mode);
  instance->setDepthWrite(depthWrite);
  // Pushed away in the depth test only, without moving where it is drawn:
  // which of two things sharing a plane is behind. The slope term goes with
  // the constant one, or a surface seen nearly edge-on needs a bias so large
  // that it separates visibly when seen face-on.
  instance->setPolygonOffset(bias, bias * 1000.0f);
  // Only where it means anything: Filament asserts rather than ignores a
  // threshold set on a material that does not punch pixels out.
  if (((surface.flags >> 2) & 15) == 3) instance->setMaskThreshold(threshold);
}


/// Opens a file and starts a decoder for it.
///
/// The decoder is the platform's: AVFoundation on Apple, where the pixel
/// format Filament's external images need is asked for, and nothing yet
/// elsewhere, which the notes then say.
void Renderer::open(Movie &movie, const std::string &path) {
  close(movie);
  movie.path = path;
  if (path.empty()) return;

  // The platform's decoder, or none where there is not one yet — which is
  // said rather than failed: a scene with a screen in it still draws, and
  // the screen is blank.
  movie.decoder = orblit::createVideoDecoder();
  if (movie.decoder == nullptr) {
    _videoNotes[path] =
        "Video is not supported on this platform yet, so this screen is "
        "blank.";
    return;
  }
  if (!movie.decoder->open(path)) {
    movie.decoder.reset();
    return;
  }

  // The external image is the decoder's own buffer, so the texture is a
  // handle rather than storage: no width, no height, no format, and nothing
  // uploaded when the picture changes.
  movie.texture = Texture::Builder()
                      .sampler(Texture::Sampler::SAMPLER_EXTERNAL)
                      .format(Texture::InternalFormat::RGBA8)
                      .build(*_engine);
}

/// Stops a video and gives back everything it was holding.
void Renderer::close(Movie &movie) {
  // The decoder stops first — its end-of-file observer, its player, its
  // output — then the texture goes, and only then the last frame it showed,
  // which the decoder keeps until it is destroyed: releasing it while the
  // texture still pointed at it would pull the picture out from under a draw.
  if (movie.decoder != nullptr) movie.decoder->stop();
  if (movie.texture != nullptr) {
    _engine->destroy(movie.texture);
    movie.texture = nullptr;
  }
  movie.decoder.reset();
  movie.flags = -1;
  movie.seekToken = -1;
}
}  // namespace orblit
