#pragma once

// Flat pictures, drawn in the order they are given.
//
// Sprites, tiles, a scrolling backdrop: anything two-dimensional. A layer is
// one image and any number of rectangles cut from it, and it is one draw —
// its corners are worked out here, on the processor, into a single vertex
// buffer. So a layer of ten thousand sprites costs the GPU what one mesh does,
// and draws them in exactly the order they were given, which is the order a
// 2D scene means.
//
// Built rather than instanced for one more reason: nothing here needs more
// of a GPU than OpenGL ES 3.0 offers, so a phone that cannot draw the
// standard lit surface, and WebGL 2, draw sprites exactly as a desktop does.
//
// C++ against Filament's public API and nothing else, as OrblitSplatSet is.

#include <filament/Engine.h>
#include <filament/Material.h>
#include <filament/Scene.h>
#include <filament/Texture.h>

#include <cstddef>
#include <cstdint>
#include <functional>
#include <memory>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

namespace orblit {

/// Floats per layer in the scene message: a column-major transform, then an
/// RGBA tint. Must match OrblitSprites.layerStride in Dart and
/// spriteLayerStride in OrblitSpriteMessage.swift.
constexpr size_t kSpriteLayerParams = 20;

/// Floats per sprite: x, y, depth, rotation, width, height, pivot x and y,
/// the image rectangle u0 v0 u1 v1, and a linear RGBA colour. Must match
/// OrblitSprites.stride and spriteStride.
constexpr size_t kSpriteRecordFloats = 16;

/// A layer's flags, in the bits OrblitSprites.flags packs them into.
constexpr int32_t kSpriteSharp = 1 << 0;
constexpr int32_t kSpriteSnap = 1 << 1;
constexpr int32_t kSpriteLinearImage = 1 << 2;
constexpr int32_t kSpriteAdditive = 1 << 3;

/// What the scene says about one layer this frame.
struct SpriteRequest {
  int32_t key = 0;
  int32_t flags = 0;
  int32_t order = 0;
  int32_t revision = 0;
  const float *params = nullptr;  // kSpriteLayerParams floats
  std::string path;               // the image; empty for plain white
  /// This layer's sprites, when they came with the message. `arrived` rather
  /// than a null test, because a layer emptied to nought sprites arrived too.
  const float *records = nullptr;
  size_t count = 0;
  bool arrived = false;
};

/// Where a layer's image comes from.
using SpriteTextures =
    std::function<filament::Texture *(const std::string &path, bool srgb)>;

class SpriteLayer;

/// Every sprite layer in a scene, kept by key between messages.
class SpriteScene {
 public:
  SpriteScene(filament::Engine &engine, filament::Scene &scene,
              SpriteTextures textures);
  ~SpriteScene();

  SpriteScene(const SpriteScene &) = delete;
  SpriteScene &operator=(const SpriteScene &) = delete;

  /// The complete list, as every scene message states it. Layers not named
  /// are removed. What could not be drawn as asked is reported in `notes`, as
  /// pairs of what it was and why.
  void apply(const std::vector<SpriteRequest> &requests,
             std::vector<std::pair<std::string, std::string>> &notes);

  bool empty() const { return _layers.empty(); }
  void clear();

 private:
  struct Kept {
    std::unique_ptr<SpriteLayer> layer;
    uint64_t seen = 0;
  };

  filament::Engine &_engine;
  filament::Scene &_scene;
  SpriteTextures _textures;
  filament::Material *_material = nullptr;
  /// One white texel, for a layer with no image: its sprites are their
  /// colours and nothing else.
  filament::Texture *_white = nullptr;
  std::unordered_map<int32_t, Kept> _layers;
  uint64_t _generation = 0;
};

}  // namespace orblit
