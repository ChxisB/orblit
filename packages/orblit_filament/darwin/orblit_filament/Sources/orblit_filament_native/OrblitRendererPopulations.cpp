#include "OrblitRendererInternal.h"

// Populations, splats and sprites: the things drawn by the thousand.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

bool Renderer::hasPopulations() {
  return !_populations.empty();
}

/// Takes a population apart. Every buffer it holds is its own.
void Renderer::clearPopulation(Grown &grown) {
  // Order matters, and getting it wrong is fatal rather than untidy.
  //
  // A renderable holds its material instance and a material instance holds
  // the book it samples, so they have to go in that order: the renderables
  // first, then the instances nothing is wearing any more, then the texture
  // nothing is sampling. Destroying an instance while a renderable still
  // uses it trips a Filament precondition, and a precondition here is not an
  // error code — it aborts the process.
  //
  // It went the other way round, so a population large enough to leave a
  // window between the two took the app down whenever one was cleared: on a
  // change of size, on leaving the example, on the sweep that drops a
  // population the scene has stopped mentioning.
  for (auto entity : grown.entities) {
    _scene->remove(entity);
    _engine->destroy(entity);
    utils::EntityManager::get().destroy(entity);
  }
  for (auto *material : grown.materials) _engine->destroy(material);
  if (grown.book != nullptr) _engine->destroy(grown.book);
  grown.book = nullptr;
  grown.materials.clear();
  grown.entities.clear();
  grown.count = 0;
  grown.revision = INT32_MIN;
}

/// The instance buffer every population draw is given.
///
/// It holds identities and is never written to again. See the note where it
/// is bound for why a buffer that carries nothing is not optional.
filament::InstanceBuffer *Renderer::identityInstances() {
  if (_identityInstances == nullptr) {
    filament::math::mat4f nothing[kInstancesPerDraw];
    _identityInstances = filament::InstanceBuffer::Builder(kInstancesPerDraw)
                             .localTransforms(nothing)
                             .build(*_engine);
  }
  return _identityInstances;
}

/// Builds the renderables one population needs, in chunks of what Filament
/// will draw at once.
void Renderer::growPopulation(Grown &grown, uint32_t count, const float *bounds, int32_t flags) {
  clearPopulation(grown);
  if (count == 0) return;

  if (_instancedMaterial == nullptr) {
    _instancedMaterial = materialFrom(Package::instanced);
  }

  // Dart sends the public minimum/maximum pair. Filament's Box is a
  // centre/half-extent pair, so passing those six values straight through
  // puts the population's box at its minimum instead of around the lot.
  const float3 minimum{bounds[0], bounds[1], bounds[2]};
  const float3 maximum{bounds[3], bounds[4], bounds[5]};
  const Box box{(minimum + maximum) * 0.5f,
                (maximum - minimum) * 0.5f};

  const uint32_t texels = count * kTexelsPerMember;
  const uint32_t rows = (texels + kBookWidth - 1) / kBookWidth;

  // Four channels rather than three: Metal has no three-channel float
  // texture, and asking for one gets it padded somewhere less visible.
  grown.book = Texture::Builder()
                   .width(kBookWidth)
                   .height(rows)
                   .levels(1)
                   .sampler(Texture::Sampler::SAMPLER_2D)
                   .format(Texture::InternalFormat::RGBA32F)
                   .build(*_engine);

  const TextureSampler nearest(TextureSampler::MinFilter::NEAREST,
                               TextureSampler::MagFilter::NEAREST);

  for (uint32_t at = 0; at < count; at += kInstancesPerDraw) {
    const uint32_t chunk = std::min(kInstancesPerDraw, count - at);

    MaterialInstance *material = _instancedMaterial->createInstance();
    material->setParameter("book", grown.book, nearest);
    material->setParameter("base", int32_t(at));
    material->setParameter("range", grown.range);
    material->setParameter("fadeFrom", grown.range * kFadeFrom);
    // Bits two and three: how a member goes at the range. Sinking suits
    // anything planted, shrinking anything scattered, and neither suits a
    // continuous surface — so the population says which it is.
    material->setParameter("fadeMode", int32_t((flags >> 2) & 3));

    utils::Entity entity = utils::EntityManager::get().create();
    RenderableManager::Builder(1)
        // Every member is culled by this one box, so it has to cover all of
        // them. A box around the mesh rather than around the population would
        // make the lot disappear as soon as the camera left the origin.
        .boundingBox(box)
        .material(0, material)
        .geometry(0, RenderableManager::PrimitiveType::TRIANGLES, _vertexBuffer,
                  _indexBuffer, 0, 36)
        // Sixty-four identities, shared by every draw in every population.
        //
        // A member's real transform comes out of the book, so this buffer
        // carries nothing — and it still has to be here. Filament indexes a
        // block of per-renderable uniforms by `instance_index` to build the
        // world position the vertex shader is handed, and asking for copies
        // *without* an instance buffer leaves every slot but the first
        // undefined: they hold whatever the renderable drawn before them left
        // there. The shader reads that position back to recover where the
        // camera is, so a stale slot puts its cube somewhere else entirely —
        // and since what was drawn before depends on the order draws are
        // submitted in, the cube moves when the camera turns. Which is what
        // "blocks floating in random places when rotating" was.
        .instances(chunk, identityInstances())
        .receiveShadows((flags & 2) != 0)
        .castShadows((flags & 1) != 0)
        .build(*_engine, entity);

    _scene->addEntity(entity);

    grown.entities.push_back(entity);
    grown.materials.push_back(material);
  }

  grown.count = count;
  grown.flags = flags;
  grown.order.clear();
  grown.shown.assign(grown.entities.size(), true);
}

/// Puts the members in an order that keeps neighbours together.
///
/// Only when the size changes, not on every write: a hundred thousand members
/// is a hundred thousand keys to sort, which is worth doing once for a forest
/// and not sixty times a second for one that is swaying. Members drift a
/// little between sorts and the draws' boxes are recomputed every time
/// anyway, so a slightly stale order costs nothing but a slightly looser box.
void Renderer::sortPopulation(Grown &grown, const float *transforms) {
  grown.order.resize(grown.count);
  if (grown.count == 0) return;

  float3 least{std::numeric_limits<float>::max()};
  float3 most{std::numeric_limits<float>::lowest()};
  for (uint32_t i = 0; i < grown.count; i++) {
    const float *m = transforms + size_t(i) * 16;
    const float3 at{m[12], m[13], m[14]};
    least = min(least, at);
    most = max(most, at);
  }

  const float3 span = max(most - least, float3{1e-4f});
  std::vector<std::pair<uint64_t, uint32_t>> keys(grown.count);

  for (uint32_t i = 0; i < grown.count; i++) {
    const float *m = transforms + size_t(i) * 16;
    const float3 at = (float3{m[12], m[13], m[14]} - least) / span;
    keys[i] = {mortonOf(uint32_t(std::clamp(at.x, 0.0f, 1.0f) * 1023.0f),
                        uint32_t(std::clamp(at.y, 0.0f, 1.0f) * 1023.0f),
                        uint32_t(std::clamp(at.z, 0.0f, 1.0f) * 1023.0f)),
               i};
  }

  std::sort(keys.begin(), keys.end());
  for (uint32_t i = 0; i < grown.count; i++) grown.order[i] = keys[i].second;
}

/// Writes a population's transforms and colours into the book it already has,
/// and works out where each draw's own members are.
///
/// Only ever called when the revision has moved.
void Renderer::fillPopulation(Grown &grown, const float *transforms, const float *colours) {
  if (grown.book == nullptr || grown.count == 0) return;
  if (grown.order.size() != grown.count) {
    sortPopulation(grown, transforms);
  }

  const uint32_t texels = grown.count * kTexelsPerMember;
  const uint32_t rows = (texels + kBookWidth - 1) / kBookWidth;
  const size_t pixels = size_t(rows) * kBookWidth;

  auto *page = new float[pixels * 4];
  std::fill(page, page + pixels * 4, 0.0f);

  const size_t draws = grown.entities.size();
  grown.middles.assign(draws, float3{0.0f});
  grown.radii.assign(draws, 0.0f);

  std::vector<float3> least(draws, float3{std::numeric_limits<float>::max()});
  std::vector<float3> most(draws, float3{std::numeric_limits<float>::lowest()});

  for (uint32_t slot = 0; slot < grown.count; slot++) {
    const uint32_t member = grown.order[slot];

    // Column-major coming in, rows going out: element (row, column) of a
    // column-major sixteen is at column * 4 + row, and the shader wants the
    // rows so that each one carries a component of the translation in its
    // fourth place.
    const float *m = transforms + size_t(member) * 16;
    float *to = page + size_t(slot) * kTexelsPerMember * 4;

    for (int row = 0; row < 3; row++) {
      to[row * 4 + 0] = m[0 * 4 + row];
      to[row * 4 + 1] = m[1 * 4 + row];
      to[row * 4 + 2] = m[2 * 4 + row];
      to[row * 4 + 3] = m[3 * 4 + row];
    }

    const float *colour = colours + size_t(member) * 3;
    to[12] = colour[0];
    to[13] = colour[1];
    to[14] = colour[2];
    to[15] = 1.0f;

    // How far a member reaches from where it stands, taken from the longest
    // of its three axes. A box drawn round the positions alone clips whatever
    // is tall.
    const float reach =
        std::max({length(float3{m[0], m[1], m[2]}),
                  length(float3{m[4], m[5], m[6]}),
                  length(float3{m[8], m[9], m[10]})});
    const float3 at{m[12], m[13], m[14]};

    const size_t draw = std::min(size_t(slot / kInstancesPerDraw), draws - 1);
    least[draw] = min(least[draw], at - reach);
    most[draw] = max(most[draw], at + reach);
  }

  auto &renderables = _engine->getRenderableManager();
  for (size_t draw = 0; draw < draws; draw++) {
    if (least[draw].x > most[draw].x) continue;

    const float3 middle = (least[draw] + most[draw]) * 0.5f;
    const float3 half = (most[draw] - least[draw]) * 0.5f;

    grown.middles[draw] = middle;
    grown.radii[draw] = length(half);

    // Each draw is culled by its own box now, rather than by one drawn round
    // the whole population. That is the difference between a camera in one
    // corner of a map paying for that corner and paying for the map.
    auto instance = renderables.getInstance(grown.entities[draw]);
    if (instance) {
      renderables.setAxisAlignedBoundingBox(instance, Box{middle, half});
    }
  }

  grown.book->setImage(
      *_engine, 0,
      Texture::PixelBufferDescriptor(
          page, pixels * 4 * sizeof(float),
          Texture::PixelBufferDescriptor::PixelDataFormat::RGBA,
          Texture::PixelBufferDescriptor::PixelDataType::FLOAT,
          [](void *buffer, size_t, void *) {
            delete[] static_cast<float *>(buffer);
          }));
}

/// Takes out of the scene whatever is further away than it is drawn from.
///
/// Called once a frame, because it depends on where the camera is. Filament
/// culls by what is in front of the camera; this is the other half of it —
/// what is close enough to be worth drawing at all. A map is mostly things
/// too far away to see, and a range is what lets one be loaded whole.
void Renderer::rangePopulations() {
  const float3 eye = _camera->getPosition();

  for (auto &entry : _populations) {
    Grown &grown = entry.second;

    // Where the camera is, told to the material rather than left for it to
    // work out. It has to reach every population, ranged or not, because it
    // is what every member's position is now measured from — see the note on
    // `cameraAt` in instanced.mat.
    for (auto *material : grown.materials) {
      material->setParameter("cameraAt", filament::math::float3{eye});
    }

    if (grown.range <= 0 || grown.middles.size() != grown.entities.size()) {
      continue;
    }

    for (size_t draw = 0; draw < grown.entities.size(); draw++) {
      // Measured to the nearest part of the draw rather than to its middle,
      // so a large group does not vanish while part of it is still close.
      // To the nearest part of the draw rather than to its middle, so a
      // large group does not vanish while part of it is still close — and
      // only once every member in it has finished sinking, or taking it out
      // is the pop the sinking exists to avoid.
      // Flat, like the shader's own test, and for the same reason: a draw
      // judged on height leaves while the ground it stands on stays.
      const float3 apart = grown.middles[draw] - eye;
      const float across = std::sqrt(apart.x * apart.x + apart.z * apart.z);
      const float away = std::max(across - grown.radii[draw], 0.0f);

      // A cell of slack, because the shader measures to the middle of a
      // sixteen-block cell and a member can stand eleven from it. Dropping a
      // draw the shader would still have drawn from is the one mistake this
      // cannot make: it is a hole, and the holes are what this was.
      const bool wanted = away <= grown.range + kCellSide;

      if (draw >= grown.shown.size()) grown.shown.resize(draw + 1, true);
      if (wanted == grown.shown[draw]) continue;

      if (wanted) {
        _scene->addEntity(grown.entities[draw]);
      } else {
        _scene->remove(grown.entities[draw]);
      }
      grown.shown[draw] = wanted;
    }
  }
}

bool Renderer::hasSplats() {
  return _splats != nullptr && !_splats->empty();
}

void Renderer::applySplats(const int32_t *keys, const int32_t *flags, const int32_t *revisions, const float *params, const std::vector<std::string> &paths, const int32_t *changed, const int32_t *changedCounts, uint32_t changedCount, const uint8_t *data, size_t dataLength, uint32_t count) {
  if (_disposed) return;
  if (_splats == nullptr) {
    if (count == 0) return;
    _splats = std::make_unique<orblit::SplatScene>(*_engine, *_scene);
  }

  // Where each changed cloud's records begin in the packed bytes.
  std::unordered_map<int32_t, std::pair<size_t, size_t>> arriving;
  size_t at = 0;
  for (uint32_t c = 0; c < changedCount; c++) {
    const size_t bytes = size_t(std::max(changedCounts[c], 0)) *
                         orblit::kSplatRecordBytes;
    if (at + bytes > dataLength) break;
    arriving[changed[c]] = {at, bytes};
    at += bytes;
  }

  std::vector<orblit::SplatRequest> requests(count);
  for (uint32_t i = 0; i < count; i++) {
    orblit::SplatRequest &request = requests[i];
    request.key = keys[i];
    request.flags = flags[i];
    request.revision = revisions[i];
    request.params = params + size_t(i) * orblit::kSplatParams;
    request.path = i < paths.size() ? std::string(paths[i]) : "";
    auto found = arriving.find(keys[i]);
    if (found != arriving.end()) {
      request.data = data + found->second.first;
      request.bytes = found->second.second;
    }
  }

  std::vector<std::pair<std::string, std::string>> notes;
  _splats->apply(requests, notes);

  _splatNotes.clear();
  for (const auto &note : notes) {
    _splatNotes[note.first] = note.second;
    orblit::log("[orblit] splats: %s: %s", note.first.c_str(), note.second.c_str());
  }
}

bool Renderer::hasSprites() {
  return _sprites != nullptr && !_sprites->empty();
}

void Renderer::applySprites(const int32_t *keys, const int32_t *flags,
                            const int32_t *orders, const int32_t *revisions,
                            const float *params,
                            const std::vector<std::string> &paths,
                            const int32_t *changed,
                            const int32_t *changedCounts,
                            uint32_t changedCount, const float *records,
                            size_t recordFloats, uint32_t count) {
  if (_disposed) return;
  _spriteTexturePaths.clear();
  _spriteTexturePaths.insert(paths.begin(), paths.end());
  if (_sprites == nullptr) {
    if (count == 0) return;
    // Images through the renderer's own cache, so one shared with a material
    // is loaded once, and bytes provided by name are found before the disk.
    _sprites = std::make_unique<orblit::SpriteScene>(
        *_engine, *_scene, [this](const std::string &path, bool srgb) {
          return textureAtPath(path, srgb);
        });
  }

  // Where each changed layer's sprites begin in the packed floats.
  std::unordered_map<int32_t, std::pair<size_t, size_t>> arriving;
  size_t at = 0;
  for (uint32_t c = 0; c < changedCount; c++) {
    const size_t sprites = size_t(std::max(changedCounts[c], 0));
    const size_t floats = sprites * orblit::kSpriteRecordFloats;
    if (at + floats > recordFloats) break;
    arriving[changed[c]] = {at, sprites};
    at += floats;
  }

  std::vector<orblit::SpriteRequest> requests(count);
  for (uint32_t i = 0; i < count; i++) {
    orblit::SpriteRequest &request = requests[i];
    request.key = keys[i];
    request.flags = flags[i];
    request.order = orders[i];
    request.revision = revisions[i];
    request.params = params + size_t(i) * orblit::kSpriteLayerParams;
    request.path = i < paths.size() ? paths[i] : std::string();
    auto found = arriving.find(keys[i]);
    if (found != arriving.end()) {
      request.records = records + found->second.first;
      request.count = found->second.second;
      request.arrived = true;
    }
  }

  // Returned rather than logged: a scene is published every frame, and a
  // missing image would otherwise say so sixty times a second.
  std::vector<std::pair<std::string, std::string>> notes;
  _sprites->apply(requests, notes);
  _spriteNotes.clear();
  for (const auto &note : notes) _spriteNotes[note.first] = note.second;
}

bool Renderer::hasTerrain() {
  return _terrain != nullptr && !_terrain->empty();
}

void Renderer::applyTerrain(
    const std::vector<orblit::TerrainRequest> &requests) {
  if (_disposed) return;
  if (_terrain == nullptr) {
    if (requests.empty()) return;
    _terrain = std::make_unique<orblit::TerrainScene>(*_engine, *_scene);
  }
  std::vector<std::pair<std::string, std::string>> notes;
  _terrain->apply(requests, notes);
  _terrainNotes.clear();
  for (const auto &note : notes) _terrainNotes[note.first] = note.second;
}

void Renderer::applyPopulations(const int32_t *keys, const int32_t *counts, const int32_t *meshes, const int32_t *flags, const int32_t *revisions, const float *ranges, const float *bounds, const std::vector<std::string> &paths, const int32_t *changed, uint32_t changedCount, const float *transforms, const float *colours, uint32_t count) {
  if (_disposed) return;

  const uint64_t generation = ++_populationGeneration;

  // Where in the packed buffers each changed population's members begin. The
  // sender packs them end to end in the order it names them.
  std::unordered_map<int32_t, size_t> arriving;
  size_t at = 0;
  for (uint32_t c = 0; c < changedCount; c++) {
    for (uint32_t i = 0; i < count; i++) {
      if (keys[i] != changed[c]) continue;
      arriving[changed[c]] = at;
      at += size_t(counts[i]);
      break;
    }
  }

  for (uint32_t i = 0; i < count; i++) {
    Grown &grown = _populations[keys[i]];
    grown.seen = generation;

    const uint32_t wanted = uint32_t(std::max(counts[i], 0));

    // A different size, a different mesh or different flags is a different
    // set of renderables. Anything else is a write into the ones there are.
    if (grown.count != wanted || grown.flags != flags[i] ||
        grown.entities.empty()) {
      growPopulation(grown, wanted, bounds + i * 6, flags[i]);
    }

    if (grown.range != ranges[i]) {
      grown.range = ranges[i];
      for (auto *material : grown.materials) {
        material->setParameter("range", grown.range);
        material->setParameter("fadeFrom", grown.range * kFadeFrom);
      }
    }

    auto found = arriving.find(keys[i]);
    if (found != arriving.end() && wanted > 0) {
      fillPopulation(grown, transforms + found->second * 16, colours + found->second * 3);
      grown.revision = revisions[i];
    }
  }

  // Anything not named this time has gone.
  for (auto it = _populations.begin(); it != _populations.end();) {
    if (it->second.seen == generation) {
      ++it;
      continue;
    }
    clearPopulation(it->second);
    it = _populations.erase(it);
  }
}
}  // namespace orblit
