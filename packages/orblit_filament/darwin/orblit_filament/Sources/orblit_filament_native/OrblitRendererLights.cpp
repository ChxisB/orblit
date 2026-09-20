#include "OrblitRendererInternal.h"

// Lights, and the decals projected onto what they light.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

/// Writes one light's parameters into Filament.
///
/// Each setter is guarded by the kind that gives it meaning: a falloff radius
/// on a directional light or a cone angle on a point light are not harmless
/// no-ops inside Filament, they are questions it was never asked.
void Renderer::writeLight(const Lit &lit) {
  auto &lights = _engine->getLightManager();
  auto instance = lights.getInstance(lit.entity);
  if (!instance) return;

  const float *p = lit.params;
  lights.setColor(instance, LinearColor{p[0], p[1], p[2]});
  lights.setIntensity(instance, p[3]);

  const float3 direction = {p[7], p[8], p[9]};
  // A zero direction would normalise to NaN and take the frame with it.
  if (lit.kind != 1 && length(direction) > 1e-6f) {
    lights.setDirection(instance, normalize(direction));
  }

  if (lit.kind == 0) {
    lights.setSunAngularRadius(instance, p[13]);
    // The disk in the sky is drawn at the light's own colour and brightness,
    // and the halo is the glow around it. A wide soft one reads as a sun
    // through air; a tight one reads as a moon on a clear night.
    lights.setSunHaloSize(instance, p[15]);
    lights.setSunHaloFalloff(instance, p[16]);
  } else {
    lights.setPosition(instance, float3{p[4], p[5], p[6]});
    lights.setFalloff(instance, p[10]);
    if (lit.kind == 2) lights.setSpotLightCone(instance, p[11], p[12]);
  }

  // Read only by percentage-closer soft shadows, so for now this is a value
  // carried faithfully rather than one that shows. It is what the penumbra
  // will be made of when the shadow type becomes somebody's to choose.
  LightManager::ShadowOptions options = lights.getShadowOptions(instance);
  // How big the light actually is, which is what area shadows work their
  // penumbra out from. Carried faithfully whatever the shadow kind, because
  // switching to area shadows should not need every light touched again.
  options.shadowBulbRadius = p[14];
  lights.setShadowOptions(instance, options);
  // And the pipeline's own settings, which a light that has just been built
  // has never been told.
  shadowOptionsFor(lit.entity);
}

void Renderer::applyLights(const int64_t *keys, const int32_t *kinds, const int32_t *flags, const float *params, uint32_t count) {
  if (_disposed) return;

  const uint64_t generation = ++_lightGeneration;
  auto &lights = _engine->getLightManager();
  auto &entities = utils::EntityManager::get();

  Notes notes;
  uint32_t directional = 0;
  uint32_t punctual = 0;

  // This frame's rectangles, gathered as they are met and uploaded once at
  // the end. They never become Filament lights, so they take no entity, cast
  // no shadow, and do not count against the punctual budget below.
  buildLtcTables();
  float rectangles[kAreaLightBudget * kAreaLightTexels * 4] = {};
  uint32_t rectangleCount = 0;
  uint32_t rectanglesAsked = 0;
  // Cleared every frame, so the rectangle that casts is decided by this
  // frame's scene rather than by whichever one happened to be first the last
  // time the lights changed.
  _areaShadowCasting = false;

  for (uint32_t i = 0; i < count; i++) {
    const int32_t kind = kinds[i];
    const float *p = params + i * kLightStride;

    if (kind == 3) {
      rectanglesAsked++;
      if (rectangleCount < kAreaLightBudget) {
        // One map, so the first rectangle that asks to cast gets it. The
        // rest are shaded without one rather than refused: a fill light with
        // no shadow is what a fill light looks like anyway, and dropping it
        // would take its light away as well as its shadow.
        bool casting = false;
        if ((flags[i] & 1) != 0) {
          if (_slimSurface) {
            // No depth map sampler to spare below feature level 3 — see
            // lit_slim.mat. The rectangle still lights the surface, just
            // without a shadow, the same as any rectangle that does not ask
            // to cast.
            notes["areaShadows"] =
                "Rectangular lights are not shadowed on this device: the "
                "slim surface, used because it is below Filament's third "
                "feature level, has no sampler to spare for the depth map.";
          } else if (!_areaShadowCasting) {
            buildAreaShadow();
            if (aimAreaShadowAt(p)) {
              _areaShadowCasting = true;
              casting = true;
            }
          } else {
            notes["areaShadows"] =
                "Only one rectangular light casts a shadow. The others are "
                "lit without one.";
          }
        }
        packRectangle(p, rectangles + rectangleCount * kAreaLightTexels * 4, casting);
        rectangleCount++;
      }
      continue;
    }

    // Filament shades one directional light per view. A second is dropped
    // rather than blended, and being told is the difference between a scene
    // that looks wrong and a scene that says why.
    if (kind == 0 && ++directional > 1) {
      notes["directional"] =
          "Only one directional light is drawn. The others are ignored.";
      continue;
    }
    if (kind != 0) punctual++;

    Lit &lit = _lit[keys[i]];

    // The kind is fixed when a light is built, so changing it is a rebuild.
    // Only changing it is: a light being dragged keeps its entity, and with it
    // its shadow map, which is what stops the shadow flickering as it moves.
    if (lit.kind != kind) {
      if (lit.entity) {
        _scene->remove(lit.entity);
        _engine->destroy(lit.entity);
        entities.destroy(lit.entity);
      }
      lit = Lit{};
      lit.kind = kind;
      lit.entity = entities.create();
      LightManager::Builder(kind == 0   ? LightManager::Type::SUN
                            : kind == 2 ? LightManager::Type::FOCUSED_SPOT
                                        : LightManager::Type::POINT)
          .build(*_engine, lit.entity);
      _scene->addEntity(lit.entity);
    }
    lit.seen = generation;

    if (!lit.applied ||
        std::memcmp(p, lit.params, sizeof(lit.params)) != 0) {
      std::memcpy(lit.params, p, sizeof(lit.params));
      lit.applied = true;
      writeLight(lit);
    }

    if (flags[i] != lit.flags) {
      lit.flags = flags[i];
      auto instance = lights.getInstance(lit.entity);
      if (instance) lights.setShadowCaster(instance, (flags[i] & 1) != 0);
    }
  }

  if (rectanglesAsked > kAreaLightBudget) {
    notes["area"] = orblit::format("%u rectangular lights is past the %u this view "
                         "shades. The ones past it light nothing.",
                         rectanglesAsked, kAreaLightBudget);
  }
  uploadRectangles(rectangles, rectangleCount);

  if (punctual > kPunctualLightBudget) {
    notes["punctual"] = orblit::format("%u point and spot lights is past the %u this view "
                         "shades. The ones furthest from the camera stop "
                         "lighting anything.",
                         punctual, kPunctualLightBudget);
  }

  for (auto it = _lit.begin(); it != _lit.end();) {
    if (it->second.seen == generation) {
      ++it;
      continue;
    }
    if (it->second.entity) {
      _scene->remove(it->second.entity);
      _engine->destroy(it->second.entity);
      entities.destroy(it->second.entity);
    }
    it = _lit.erase(it);
  }

  _lightNotes = notes;
}

// ---- Decals ----
//
// The arithmetic — sorting, the budget, the matrix into each box — is in
// OrblitDecals.cpp. What is here is handing textures to Filament; turning an
// image file into pixels is the platform layer's orblit::readPicture, which is
// ImageIO on Apple and stb_image everywhere else.

/// The rows the decals live in, built empty.
void Renderer::buildDecalData() {
  if (_decalData != nullptr) return;
  _decalData = Texture::Builder()
                   .width(orblit::kDecalTexels)
                   .height(orblit::kDecalBudget)
                   .levels(1)
                   .format(Texture::InternalFormat::RGBA32F)
                   .sampler(Texture::Sampler::SAMPLER_2D)
                   .usage(Texture::Usage::SAMPLEABLE |
                          Texture::Usage::UPLOADABLE)
                   .build(*_engine);
  // Zeros, so a surface drawn before the first scene reads a count of nought
  // rather than whatever the driver had there.
  const size_t floats =
      size_t(orblit::kDecalTexels) * orblit::kDecalBudget * 4;
  float *blank = static_cast<float *>(calloc(floats, sizeof(float)));
  _decalData->setImage(
      *_engine, 0,
      Texture::PixelBufferDescriptor(
          blank, floats * sizeof(float),
          Texture::PixelBufferDescriptor::PixelDataFormat::RGBA,
          Texture::PixelBufferDescriptor::PixelDataType::FLOAT,
          [](void *buffer, size_t, void *) { free(buffer); }));
  _decalsOnGpu.assign(floats, 0.0f);

  // One white texel in an array of one, for surfaces to bind until a scene
  // names a picture. An array, not the blank 2D texture: the sampler's type
  // is part of the material, and a 2D texture in an array's slot is refused.
  _decalBlankPictures = Texture::Builder()
                            .width(1)
                            .height(1)
                            .depth(1)
                            .levels(1)
                            .sampler(Texture::Sampler::SAMPLER_2D_ARRAY)
                            .format(Texture::InternalFormat::RGBA8)
                            .build(*_engine);
  uint8_t *white = new uint8_t[4]{255, 255, 255, 255};
  _decalBlankPictures->setImage(
      *_engine, 0, 0, 0, 0, 1, 1, 1,
      Texture::PixelBufferDescriptor(
          white, 4, Texture::Format::RGBA, Texture::Type::UBYTE,
          [](void *buffer, size_t, void *) {
            delete[] static_cast<uint8_t *>(buffer);
          }));

  // The slim surface still gets this texture built — cheap, and simpler
  // than teaching every caller two shapes for the CPU-side data — but reads
  // its decals out of lightData's tail instead, because there was no
  // sampler left to give this one of its own. Zeroed the same way, so a
  // decal-less scene reads a count of nought there too.
  if (_slimSurface) {
    buildLtcTables();
    float *slimBlank = static_cast<float *>(calloc(floats, sizeof(float)));
    _lightData->setImage(
        *_engine, 0, 0, orblit::kSlimDecalRow, orblit::kDecalTexels,
        orblit::kDecalBudget,
        Texture::PixelBufferDescriptor(
            slimBlank, floats * sizeof(float),
            Texture::PixelBufferDescriptor::PixelDataFormat::RGBA,
            Texture::PixelBufferDescriptor::PixelDataType::FLOAT,
            [](void *buffer, size_t, void *) { free(buffer); }));
  }
}

/// Gives one lit surface the decals to read.
void Renderer::bindDecalsTo(MaterialInstance *instance) {
  buildDecalData();
  // The standard surface has a sampler to spare for this; the slim one
  // spent its ninth on decalImages below and reads the same rows out of
  // lightData's tail instead — bound already, in setDefaultsOn, and kept
  // in step by buildDecalData and applyDecals writing both textures.
  if (!_slimSurface) {
    // Read with texelFetch, which ignores filtering; nearest says so anyway.
    const TextureSampler exact(TextureSampler::MinFilter::NEAREST,
                               TextureSampler::MagFilter::NEAREST,
                               TextureSampler::WrapMode::CLAMP_TO_EDGE);
    instance->setParameter("decalData", _decalData, exact);
  }

  // Clamped, so the picture's edge texels are not wrapped round to meet the
  // opposite edge where the box ends. Anisotropic, because a floor decal is
  // nearly always seen at a grazing angle.
  TextureSampler smooth(TextureSampler::MinFilter::LINEAR_MIPMAP_LINEAR,
                        TextureSampler::MagFilter::LINEAR,
                        TextureSampler::WrapMode::CLAMP_TO_EDGE);
  smooth.setAnisotropy(8.0f);
  instance->setParameter(
      "decalImages",
      _decalPictures != nullptr ? _decalPictures : _decalBlankPictures,
      smooth);
}

/// Points every lit surface at the decal textures again — once, when the
/// pictures' array replaces the stand-in.
void Renderer::bindDecalsEverywhere() {
  for (auto &entry : _drawn) {
    if (entry.second.material != nullptr) {
      bindDecalsTo(entry.second.material);
    }
  }
  for (auto &entry : _materials) {
    if (entry.second.instance == nullptr) continue;
    if ((entry.second.flags & 3) != 0) continue;
    bindDecalsTo(entry.second.instance);
  }
}

/// The array layer a picture is in, reading it into the next free one the
/// first time it is named. Negative when it could not be read or there was
/// no room, and the reason goes in `notes`.
int32_t Renderer::decalLayerFor(const std::string &path, Notes &notes,
                                bool *uploaded) {
  const std::string &identity = path;
  auto found = _decalPictureLayer.find(identity);
  int32_t layer = found != _decalPictureLayer.end() ? found->second : -3;

  if (layer == -3) {
    if (_decalPictureCount >= kDecalPictureLayers) {
      layer = -2;
    } else {
      const orblit::SharedBytes file = orblit::readResource(path);
      std::vector<uint8_t> pixels =
          file ? orblit::readPicture(file->data(), file->size(),
                                     kDecalPictureSide)
               : std::vector<uint8_t>();
      if (pixels.empty()) {
        layer = -1;
      } else {
        if (_decalPictures == nullptr) {
          _decalPictures =
              Texture::Builder()
                  .width(kDecalPictureSide)
                  .height(kDecalPictureSide)
                  .depth(kDecalPictureLayers)
                  .levels(kDecalPictureLevels)
                  .sampler(Texture::Sampler::SAMPLER_2D_ARRAY)
                  // sRGB, so the hardware decodes to linear before it
                  // filters. The pictures are premultiplied in sRGB, which is
                  // exact wherever they are opaque and slightly dark in a
                  // soft edge; drawing them into a linear 8-bit bitmap
                  // instead would band every dark picture.
                  .format(Texture::InternalFormat::SRGB8_A8)
                  .usage(Texture::Usage::SAMPLEABLE |
                         Texture::Usage::UPLOADABLE |
                         Texture::Usage::GEN_MIPMAPPABLE)
                  .build(*_engine);
          bindDecalsEverywhere();
        }
        layer = int32_t(_decalPictureCount++);
        const size_t bytes = pixels.size();
        uint8_t *copy = static_cast<uint8_t *>(malloc(bytes));
        memcpy(copy, pixels.data(), bytes);
        _decalPictures->setImage(
            *_engine, 0, 0, 0, uint32_t(layer), kDecalPictureSide,
            kDecalPictureSide, 1,
            Texture::PixelBufferDescriptor(
                copy, bytes, Texture::Format::RGBA, Texture::Type::UBYTE,
                [](void *buffer, size_t, void *) { free(buffer); }));
        *uploaded = true;
      }
    }
    _decalPictureLayer[identity] = layer;
  }

  if (layer == -1) {
    notes[path] = "This decal's picture could not be read. It is painted "
                  "as its tint alone.";
  } else if (layer == -2) {
    notes["decalPictures"] = orblit::format(
        "More than %u different decal pictures. The ones "
        "past it are painted as their tint alone.",
        kDecalPictureLayers);
  }
  return layer;
}

void Renderer::applyDecals(const float *params, const int32_t *images, const std::vector<std::string> &paths, uint32_t count) {
  if (_disposed) return;
  buildDecalData();

  Notes notes;

  // Pictures first, only for the decals that will be painted: reading a file
  // for one past the budget would spend a layer on something never shown.
  std::vector<int32_t> layers(std::max(count, 1u), -1);
  bool uploaded = false;
  const uint32_t painted = std::min(count, orblit::kDecalBudget);
  for (uint32_t i = 0; i < painted; i++) {
    const int32_t index = images[i];
    if (index < 0 || index >= static_cast<int32_t>(paths.size())) continue;
    layers[i] = decalLayerFor(paths[index], notes, &uploaded);
    if (layers[i] < 0) layers[i] = -1;
  }
  // Once for however many arrived, rather than once each: it rebuilds every
  // layer's chain, and a scene's first frame usually names several at once.
  if (uploaded) _decalPictures->generateMipmaps(*_engine);

  const size_t floats =
      size_t(orblit::kDecalTexels) * orblit::kDecalBudget * 4;
  std::vector<float> wanted(floats);
  const orblit::DecalPacking packing =
      orblit::packDecals(params, layers.data(), count, wanted.data());
  if (packing.asked > packing.packed) {
    notes["decals"] = orblit::format("%u decals is past the %u this view paints. The "
                         "ones listed after that are not painted.",
                         packing.asked, packing.packed);
  }
  _decalNotes = notes;

  // A scene standing still uploads nothing.
  if (wanted == _decalsOnGpu) return;
  _decalsOnGpu = wanted;
  float *copy = static_cast<float *>(malloc(floats * sizeof(float)));
  memcpy(copy, wanted.data(), floats * sizeof(float));
  _decalData->setImage(
      *_engine, 0,
      Texture::PixelBufferDescriptor(
          copy, floats * sizeof(float),
          Texture::PixelBufferDescriptor::PixelDataFormat::RGBA,
          Texture::PixelBufferDescriptor::PixelDataType::FLOAT,
          [](void *buffer, size_t, void *) { free(buffer); }));

  // The same rows again, into lightData's tail, for the slim surface's own
  // decalTexel to read — see kSlimDecalRow and bindDecalsTo. A second small
  // upload rather than a second code path: the packing above already did
  // the only part that is not mechanical.
  if (_slimSurface) {
    buildLtcTables();
    float *slimCopy = static_cast<float *>(malloc(floats * sizeof(float)));
    memcpy(slimCopy, wanted.data(), floats * sizeof(float));
    _lightData->setImage(
        *_engine, 0, 0, orblit::kSlimDecalRow, orblit::kDecalTexels,
        orblit::kDecalBudget,
        Texture::PixelBufferDescriptor(
            slimCopy, floats * sizeof(float),
            Texture::PixelBufferDescriptor::PixelDataFormat::RGBA,
            Texture::PixelBufferDescriptor::PixelDataType::FLOAT,
            [](void *buffer, size_t, void *) { free(buffer); }));
  }
}
}  // namespace orblit
