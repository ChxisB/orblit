// Gaussian splats as Filament draws them.
//
// C++ against Filament's public API and nothing else, so it moves to every
// backend Filament has without change. The .mm owns one SplatScene and calls
// it from the scene message and once a frame; everything about textures,
// buffers and sorting is here.
#pragma once

#include "OrblitSplats.h"

#include <filament/Engine.h>
#include <filament/IndexBuffer.h>
#include <filament/Material.h>
#include <filament/MaterialInstance.h>
#include <filament/Scene.h>
#include <filament/Texture.h>
#include <filament/VertexBuffer.h>
#include <math/mat4.h>
#include <math/vec3.h>
#include <utils/Entity.h>

#include <memory>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

namespace orblit {

/// The camera a frame is drawn from, as the splats need it: which way it
/// faces, to put them in order, and where it is and what it sees, to leave
/// out the ones it cannot.
struct SplatCamera {
  /// Its forward vector, in world space.
  filament::math::float3 forward{0, 0, -1};
  filament::math::mat4f viewFromWorld;
  filament::math::mat4f clipFromView;
};

/// One cloud: its data on the GPU, its renderable, and its sorter.
class SplatSet {
 public:
  SplatSet(filament::Engine &engine, filament::Scene &scene,
           filament::Material &material, SplatCloud &&cloud);
  ~SplatSet();

  SplatSet(const SplatSet &) = delete;
  SplatSet &operator=(const SplatSet &) = delete;

  /// Column-major, model to world.
  void setTransform(const float matrix[16]);
  void setOpacity(float opacity);
  void setBrightness(float brightness);

  /// Whether to sort, and how finely.
  ///
  /// Unsorted draws every splat in the order it was given, for measuring what
  /// the sort is worth; sorted is the only right answer for a picture. A
  /// coarse sort orders on sixteen bits of depth rather than thirty-two, which
  /// halves its passes for splats very nearly as far away as one another
  /// coming out in either order.
  void setOrdering(bool sorted, bool coarse);

  /// Once a frame, with the camera the frame is drawn from: asks for a new
  /// order when the camera has moved or turned since the last one, and
  /// uploads any order that has finished.
  void update(const SplatCamera &camera);

  uint32_t count() const { return _count; }
  /// How many splats the order on the GPU draws: all of them until the first
  /// sort lands, and the ones in view after it.
  uint32_t drawn() const { return _drawn; }
  double lastSortMilliseconds() const { return _lastSortMs; }

 private:
  void landSort();
  void uploadGiven();
  void uploadOrder(const std::vector<uint32_t> &order);
  void setDrawn(uint32_t splats);

  filament::Engine &_engine;
  filament::Scene &_scene;
  uint32_t _count = 0;

  filament::Texture *_splats = nullptr;
  filament::Texture *_order = nullptr;
  /// The higher spherical-harmonic bands, or a single texel standing in for
  /// them when the cloud has none: a material's sampler has to be bound
  /// whether or not the shader ever reads it.
  filament::Texture *_harmonics = nullptr;
  filament::VertexBuffer *_corners = nullptr;
  filament::IndexBuffer *_indices = nullptr;
  filament::MaterialInstance *_instance = nullptr;
  utils::Entity _entity;
  /// The layers the renderable was built on, given back when a cloud that
  /// had nothing in view has something again.
  uint8_t _layers = 1;
  /// Splats the geometry draws: the first this many slots of the order.
  uint32_t _drawn = 0;

  std::unique_ptr<SplatSorter> _sorter;
  float _matrix[16] = {1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1};
  bool _sorted = true;
  bool _coarse = false;
  /// Whether the order on the GPU is the given one rather than a sort.
  bool _showingGiven = true;
  bool _everSorted = false;
  /// What the order on the GPU, or on its way, was sorted for: the camera,
  /// as the cloud's own space to clip space, and how finely.
  filament::math::mat4f _sortedClip;
  bool _sortedCoarse = false;
  double _lastSortMs = 0;
  uint32_t _sortsLanded = 0;
  bool _warnedDegenerate = false;
};

/// What the scene says about one cloud this frame.
struct SplatRequest {
  int32_t key = 0;
  int32_t flags = 0;
  int32_t revision = 0;
  const float *params = nullptr;  // kSplatParams floats
  std::string path;               // empty for a cloud sent in memory
  const uint8_t *data = nullptr;  // records, when they came with the message
  size_t bytes = 0;
};

/// Every cloud in a scene, kept by key between messages.
class SplatScene {
 public:
  SplatScene(filament::Engine &engine, filament::Scene &scene);
  ~SplatScene();

  /// The complete list, as every scene message states it. Clouds not named
  /// are removed. What could not be loaded is reported in `notes` as pairs
  /// of what it was and why.
  void apply(const std::vector<SplatRequest> &requests,
             std::vector<std::pair<std::string, std::string>> &notes);

  void update(const SplatCamera &camera);

  bool empty() const { return _sets.empty(); }
  void clear();

 private:
  struct Kept {
    std::unique_ptr<SplatSet> set;
    std::string path;
    int32_t revision = 0;
    /// The spherical-harmonic degree this one was read at, and the most
    /// splats it was asked to keep — nought for all. Kept because asking for
    /// a different one means reading the cloud again: what was dropped on the
    /// way in is not on the GPU to be brought back.
    uint32_t degree = 0;
    uint32_t limit = 0;
    uint64_t seen = 0;
  };

  filament::Engine &_engine;
  filament::Scene &_scene;
  filament::Material *_material = nullptr;
  std::unordered_map<int32_t, Kept> _sets;
  uint64_t _generation = 0;
};

}  // namespace orblit
