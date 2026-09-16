#include "OrblitSprites.h"

#include <filament/Box.h>
#include <filament/IndexBuffer.h>
#include <filament/MaterialInstance.h>
#include <filament/RenderableManager.h>
#include <filament/TextureSampler.h>
#include <filament/TransformManager.h>
#include <filament/VertexBuffer.h>
#include <math/mat4.h>
#include <math/vec3.h>
#include <math/vec4.h>
#include <utils/Entity.h>
#include <utils/EntityManager.h>

#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>

#include "sprite_material.h"

using namespace filament;
using namespace filament::math;

namespace orblit {

namespace {

/// Floats a corner: position, texture coordinate, colour.
constexpr size_t kCornerFloats = 9;

/// Room for this many sprites at first, doubled whenever a layer outgrows it,
/// so a layer that grows one sprite a frame rebuilds its buffers a handful of
/// times rather than on every frame.
constexpr size_t kFirstCapacity = 64;

/// A layer's order as Filament holds one: fifteen bits, nought first. The
/// scene's order is signed, so a layer can go behind the default as easily as
/// in front of it, and centred in that range.
uint16_t blendOrderOf(int32_t order) {
  return uint16_t(std::clamp<int64_t>(int64_t(order) + 16384, 0, 32767));
}

/// Four corners a sprite, and the box round all of them.
void buildCorners(const float *records, size_t count, float *corners,
                  float3 &least, float3 &most) {
  least = float3(std::numeric_limits<float>::max());
  most = float3(std::numeric_limits<float>::lowest());
  for (size_t s = 0; s < count; s++) {
    const float *r = records + s * kSpriteRecordFloats;
    const float cosine = std::cos(r[3]);
    const float sine = std::sin(r[3]);
    const float left = -r[6] * r[4];
    const float right = (1.0f - r[6]) * r[4];
    const float bottom = -r[7] * r[5];
    const float top = (1.0f - r[7]) * r[5];
    // Bottom left, bottom right, top right, top left. Row nought of an image
    // is its top, and so is v0 of a rectangle cut from it.
    const float local[4][2] = {
        {left, bottom}, {right, bottom}, {right, top}, {left, top}};
    const float uv[4][2] = {
        {r[8], r[11]}, {r[10], r[11]}, {r[10], r[9]}, {r[8], r[9]}};
    for (int k = 0; k < 4; k++) {
      float *v = corners + (s * 4 + size_t(k)) * kCornerFloats;
      v[0] = r[0] + local[k][0] * cosine - local[k][1] * sine;
      v[1] = r[1] + local[k][0] * sine + local[k][1] * cosine;
      v[2] = r[2];
      v[3] = uv[k][0];
      v[4] = uv[k][1];
      std::memcpy(v + 5, r + 12, 4 * sizeof(float));
      for (int axis = 0; axis < 3; axis++) {
        // A sprite somebody left at infinity is not drawn anywhere useful,
        // and a box stretched to reach it culls nothing.
        if (!std::isfinite(v[axis])) continue;
        least[axis] = std::min(least[axis], v[axis]);
        most[axis] = std::max(most[axis], v[axis]);
      }
    }
  }
  for (int axis = 0; axis < 3; axis++) {
    if (least[axis] > most[axis]) least[axis] = most[axis] = 0.0f;
  }
}

}  // namespace

/// One layer: its buffers, its renderable, and what it was last told.
class SpriteLayer {
 public:
  SpriteLayer(Engine &engine, Scene &scene, Material &material)
      : _engine(engine), _scene(scene) {
    _instance = material.createInstance();
    _entity = utils::EntityManager::get().create();
    engine.getTransformManager().create(_entity);
  }

  ~SpriteLayer() {
    if (_shown) _scene.remove(_entity);
    RenderableManager &renderables = _engine.getRenderableManager();
    if (renderables.hasComponent(_entity)) renderables.destroy(_entity);
    _engine.getTransformManager().destroy(_entity);
    utils::EntityManager::get().destroy(_entity);
    _engine.destroy(_instance);
    if (_indices != nullptr) _engine.destroy(_indices);
    if (_corners != nullptr) _engine.destroy(_corners);
  }

  SpriteLayer(const SpriteLayer &) = delete;
  SpriteLayer &operator=(const SpriteLayer &) = delete;

  /// Everything about the layer but its sprites: cheap, and sent every frame.
  void setLayer(const SpriteRequest &request, Texture *image) {
    TransformManager &transforms = _engine.getTransformManager();
    mat4f matrix;
    for (int column = 0; column < 4; column++) {
      for (int row = 0; row < 4; row++) {
        matrix[column][row] = request.params[column * 4 + row];
      }
    }
    transforms.setTransform(transforms.getInstance(_entity), matrix);
    _instance->setParameter(
        "tint", float4{request.params[16], request.params[17],
                       request.params[18], request.params[19]});

    if (request.flags != _flags || image != _image) {
      const bool sharp = (request.flags & kSpriteSharp) != 0;
      const TextureSampler sampler(
          sharp ? TextureSampler::MinFilter::NEAREST
                : TextureSampler::MinFilter::LINEAR_MIPMAP_LINEAR,
          sharp ? TextureSampler::MagFilter::NEAREST
                : TextureSampler::MagFilter::LINEAR,
          TextureSampler::WrapMode::CLAMP_TO_EDGE);
      _instance->setParameter("image", image, sampler);
      _instance->setParameter("snap", (request.flags & kSpriteSnap) != 0);
      _instance->setParameter("additive",
                              (request.flags & kSpriteAdditive) != 0);
      _flags = request.flags;
      _image = image;
    }

    if (request.order != _order) {
      _order = request.order;
      // Filament fixes a draw's blend order when the renderable is built, so
      // a new one is built. Orders change when somebody rearranges a scene,
      // not sixty times a second.
      if (_corners != nullptr) rebuild();
    }
  }

  void setSprites(const float *records, size_t count) {
    _count = count;
    if (count == 0) {
      show(false);
      return;
    }
    if (count > _capacity) grow(count);

    const size_t floats = count * 4 * kCornerFloats;
    auto *corners = new float[floats];
    float3 least;
    float3 most;
    buildCorners(records, count, corners, least, most);
    _corners->setBufferAt(
        _engine, 0,
        VertexBuffer::BufferDescriptor(
            corners, floats * sizeof(float), [](void *buffer, size_t, void *) {
              delete[] static_cast<float *>(buffer);
            }));

    // Flat sprites have a flat box, and a box with no depth at all is one
    // some culling arithmetic divides by; a sliver keeps it honest.
    _box = Box{(least + most) * 0.5f, max((most - least) * 0.5f, float3(1e-4f))};
    RenderableManager &renderables = _engine.getRenderableManager();
    const auto instance = renderables.getInstance(_entity);
    renderables.setGeometryAt(instance, 0,
                              RenderableManager::PrimitiveType::TRIANGLES,
                              _corners, _indices, 0, count * 6);
    renderables.setAxisAlignedBoundingBox(instance, _box);
    show(true);
  }

 private:
  void grow(size_t count) {
    size_t capacity = std::max(_capacity, kFirstCapacity);
    while (capacity < count) capacity *= 2;

    VertexBuffer *corners =
        VertexBuffer::Builder()
            .vertexCount(uint32_t(capacity * 4))
            .bufferCount(1)
            .attribute(VertexAttribute::POSITION, 0,
                       VertexBuffer::AttributeType::FLOAT3, 0,
                       uint8_t(kCornerFloats * sizeof(float)))
            .attribute(VertexAttribute::UV0, 0,
                       VertexBuffer::AttributeType::FLOAT2, 3 * sizeof(float),
                       uint8_t(kCornerFloats * sizeof(float)))
            .attribute(VertexAttribute::COLOR, 0,
                       VertexBuffer::AttributeType::FLOAT4, 5 * sizeof(float),
                       uint8_t(kCornerFloats * sizeof(float)))
            .build(_engine);

    const size_t indexCount = capacity * 6;
    auto *indices = new uint32_t[indexCount];
    for (size_t s = 0; s < capacity; s++) {
      const auto base = uint32_t(s * 4);
      uint32_t *at = indices + s * 6;
      at[0] = base;
      at[1] = base + 1;
      at[2] = base + 2;
      at[3] = base;
      at[4] = base + 2;
      at[5] = base + 3;
    }
    IndexBuffer *built = IndexBuffer::Builder()
                             .indexCount(uint32_t(indexCount))
                             .bufferType(IndexBuffer::IndexType::UINT)
                             .build(_engine);
    built->setBuffer(_engine, IndexBuffer::BufferDescriptor(
                                  indices, indexCount * sizeof(uint32_t),
                                  [](void *buffer, size_t, void *) {
                                    delete[] static_cast<uint32_t *>(buffer);
                                  }));

    VertexBuffer *oldCorners = _corners;
    IndexBuffer *oldIndices = _indices;
    _corners = corners;
    _indices = built;
    _capacity = capacity;
    // The renderable is pointed at the new buffers before the old ones go.
    rebuild();
    if (oldCorners != nullptr) _engine.destroy(oldCorners);
    if (oldIndices != nullptr) _engine.destroy(oldIndices);
  }

  void rebuild() {
    RenderableManager &renderables = _engine.getRenderableManager();
    if (renderables.hasComponent(_entity)) renderables.destroy(_entity);
    RenderableManager::Builder(1)
        .boundingBox(_box)
        .material(0, _instance)
        .geometry(0, RenderableManager::PrimitiveType::TRIANGLES, _corners,
                  _indices, 0, std::max<size_t>(_count, 1) * 6)
        // Layers draw by their order, not by how far their boxes are from
        // the camera, which for a 2D scene is the same distance for all.
        .blendOrder(0, blendOrderOf(_order))
        .globalBlendOrderEnabled(0, true)
        .castShadows(false)
        .receiveShadows(false)
        .build(_engine, _entity);
  }

  void show(bool shown) {
    if (shown == _shown) return;
    if (shown) {
      _scene.addEntity(_entity);
    } else {
      _scene.remove(_entity);
    }
    _shown = shown;
  }

  Engine &_engine;
  Scene &_scene;
  MaterialInstance *_instance = nullptr;
  utils::Entity _entity;
  VertexBuffer *_corners = nullptr;
  IndexBuffer *_indices = nullptr;
  size_t _capacity = 0;
  size_t _count = 0;
  Box _box{{0, 0, 0}, {1, 1, 1}};
  Texture *_image = nullptr;
  int32_t _flags = -1;
  int32_t _order = 0;
  bool _shown = false;
};

SpriteScene::SpriteScene(Engine &engine, Scene &scene, SpriteTextures textures)
    : _engine(engine), _scene(scene), _textures(std::move(textures)) {}

SpriteScene::~SpriteScene() { clear(); }

void SpriteScene::clear() {
  _layers.clear();
  if (_material != nullptr) {
    _engine.destroy(_material);
    _material = nullptr;
  }
  if (_white != nullptr) {
    _engine.destroy(_white);
    _white = nullptr;
  }
}

void SpriteScene::apply(
    const std::vector<SpriteRequest> &requests,
    std::vector<std::pair<std::string, std::string>> &notes) {
  if (_material == nullptr) {
    _material = Material::Builder()
                    .package(kspriteMaterial, kspriteMaterial_len)
                    .build(_engine);
    _white = Texture::Builder()
                 .width(1)
                 .height(1)
                 .levels(1)
                 .format(Texture::InternalFormat::RGBA8)
                 .sampler(Texture::Sampler::SAMPLER_2D)
                 .build(_engine);
    static const uint8_t kWhite[4] = {255, 255, 255, 255};
    _white->setImage(_engine, 0,
                     Texture::PixelBufferDescriptor(
                         kWhite, sizeof(kWhite), Texture::Format::RGBA,
                         Texture::Type::UBYTE));
  }

  const uint64_t generation = ++_generation;
  for (const SpriteRequest &request : requests) {
    Kept &kept = _layers[request.key];
    kept.seen = generation;
    if (!kept.layer) {
      kept.layer = std::make_unique<SpriteLayer>(_engine, _scene, *_material);
    }

    Texture *image = _white;
    if (!request.path.empty()) {
      const bool srgb = (request.flags & kSpriteLinearImage) == 0;
      Texture *found = _textures ? _textures(request.path, srgb) : nullptr;
      if (found != nullptr) {
        image = found;
      } else {
        notes.emplace_back(request.path,
                           "This image could not be read, so the sprites "
                           "that use it are drawn plain.");
      }
    }

    kept.layer->setLayer(request, image);
    if (request.arrived) kept.layer->setSprites(request.records, request.count);
  }

  for (auto it = _layers.begin(); it != _layers.end();) {
    if (it->second.seen != generation) {
      it = _layers.erase(it);
    } else {
      ++it;
    }
  }
}

}  // namespace orblit
