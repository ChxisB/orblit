#pragma once

// Ground: heights, what covers them, and a mesh that follows the camera.
//
// A terrain is regions, square pieces of ground a power of two texels a side,
// each with three maps: a height a texel, a cover word a texel, and a colour.
// They go to the GPU as layers of three array textures and are never turned
// into triangles here. The mesh is one flat grid, drawn a few times over at
// doubling sizes round the camera, and terrain.mat raises each vertex by the
// height under it. So painting the ground is uploading a layer, and however
// much ground there is, it is a handful of draws.
//
// The sets — the pictures a cover word names — are two more array textures,
// a layer a set, all one size.
//
// C++ against Filament's public API and nothing else, as OrblitSprites is.

#include <filament/Engine.h>
#include <filament/Material.h>
#include <filament/Scene.h>
#include <math/vec3.h>

#include <cstddef>
#include <cstdint>
#include <memory>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

namespace orblit {

/// Whole numbers at the head of each terrain in the message: key, flags,
/// region size, mesh size, levels, the automatic cover's steep and flat sets,
/// the set count, the pictures' size, whether the pictures came with this
/// message, the triplanar sets as a mask, and the region count. Must match
/// OrblitTerrain.headerInts in Dart.
constexpr size_t kTerrainInts = 12;

/// Whole numbers a region, after its terrain's head: x, z, and whether its
/// maps came with this message. Must match OrblitTerrain.regionInts.
constexpr size_t kTerrainRegionInts = 3;

/// Floats a terrain: spacing, blend sharpness, and the automatic cover's
/// slope and height falloff. Must match OrblitTerrain.stride.
constexpr size_t kTerrainParams = 4;

/// Floats a set, after its terrain's: the size one copy of its picture
/// covers. Must match OrblitTerrainSet.stride.
constexpr size_t kTerrainSetParams = 1;

/// A terrain's flags, in the bits OrblitTerrain.flags packs them into.
constexpr int32_t kTerrainCastsShadows = 1 << 0;
constexpr int32_t kTerrainReceivesShadows = 1 << 1;

/// Regions a terrain: the array layers OpenGL ES 3.0 promises.
constexpr int32_t kTerrainMaxRegions = 256;
/// Sets a terrain: what a cover word's five bits can name.
constexpr int32_t kTerrainMaxSets = 32;
/// How far apart, in regions, a terrain's regions can be on either axis.
/// The shader finds a region's layer in a map one texel a region wide.
constexpr int32_t kTerrainMaxSpan = 128;
constexpr int32_t kTerrainMinRegionSize = 16;
constexpr int32_t kTerrainMaxRegionSize = 2048;
/// The grid's half, in cells. Below sixteen the band where one level folds
/// into the next is gone.
constexpr int32_t kTerrainMinMeshSize = 16;
constexpr int32_t kTerrainMaxMeshSize = 256;
constexpr int32_t kTerrainMaxLevels = 12;
constexpr int32_t kTerrainMaxTextureSize = 4096;

/// One region, as the message gives it.
struct TerrainRegionRequest {
  int32_t x = 0;
  int32_t z = 0;
  /// Its maps, when they came with the message: region size squared heights
  /// as floats, as many cover words, then four colour bytes a texel, all
  /// little-endian and with no alignment promised.
  const uint8_t *maps = nullptr;
  bool arrived = false;
};

/// What the scene says about one terrain.
struct TerrainRequest {
  int32_t key = 0;
  int32_t flags = 0;
  int32_t regionSize = 0;
  int32_t meshSize = 0;
  int32_t levels = 0;
  int32_t autoSteep = 0;
  int32_t autoFlat = 0;
  int32_t setCount = 0;
  int32_t textureSize = 0;
  uint32_t triplanar = 0;
  float spacing = 1.0f;
  float blendSharpness = 0.0f;
  float autoSlope = 0.0f;
  float autoHeightFalloff = 0.0f;
  const float *tileSizes = nullptr;  // setCount floats
  /// The sets' pictures, when they came with the message: setCount layers of
  /// sRGB albedo with a height in alpha, then setCount of a normal with a
  /// roughness in alpha, texture size squared RGBA bytes each.
  const uint8_t *pictures = nullptr;
  bool picturesArrived = false;
  std::vector<TerrainRegionRequest> regions;
};

/// Why a terrain message could not be read.
enum class TerrainParse { ok, length, range };

/// Reads the message's three arrays into requests. Every part is measured
/// against what is there before it is read, and every number against the
/// limits above; a message that fails either is refused whole, and
/// `requests` is left empty. The three arrays must be used up exactly, so a
/// packer that has drifted from this one is caught rather than half-read.
TerrainParse parseTerrain(const int32_t *ints, size_t intCount,
                          const float *floats, size_t floatCount,
                          const uint8_t *data, size_t dataLength,
                          std::vector<TerrainRequest> &requests);

class TerrainField;

/// Every terrain in a scene, kept by key between messages.
class TerrainScene {
 public:
  TerrainScene(filament::Engine &engine, filament::Scene &scene);
  ~TerrainScene();

  TerrainScene(const TerrainScene &) = delete;
  TerrainScene &operator=(const TerrainScene &) = delete;

  /// The complete list, as every scene message states it. Terrains not named
  /// are removed. What could not be drawn as asked is reported in `notes`, as
  /// pairs of what it was and why.
  void apply(const std::vector<TerrainRequest> &requests,
             std::vector<std::pair<std::string, std::string>> &notes);

  /// Places every terrain's grid round the camera. Once a frame, before it
  /// is drawn, with the camera where that frame sees from.
  void update(const filament::math::double3 &eye);

  bool empty() const { return _fields.empty(); }
  void clear();

 private:
  struct Kept {
    std::unique_ptr<TerrainField> field;
    uint64_t seen = 0;
  };

  filament::Engine &_engine;
  filament::Scene &_scene;
  filament::Material *_material = nullptr;
  std::unordered_map<int32_t, Kept> _fields;
  uint64_t _generation = 0;
};

}  // namespace orblit
