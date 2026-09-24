#pragma once

// A parsed scene, and how it is handed to the renderer -- the part that is
// the same on every desktop embedder.
//
// The Linux and Windows plugins each decode the `setScene` message their own
// way, because their embedders hand it over differently: GTK's is an FlValue
// asked its type and unwrapped, Windows' an `EncodableValue` variant reached
// with std::get_if. What they do *after* decoding is not different at all --
// the same fields, in the same order, applied through the same ABI calls --
// so that half lives here and each plugin supplies only its own `From`.
//
// The field list mirrors ../android/.../OrblitScene.kt, which mirrors
// OrblitFilamentPlugin.swift's private `Scene` struct: same fields, same
// order, same optionality, so a change made to one is easy to find in the
// others. Like theirs, `ApplyTo` trusts the indices inside the arrays and
// relies on `From` having already checked their *shape*, because the one
// place these numbers come from is Dart's own OrblitScene.toMessage().

#include <cstdint>
#include <string>
#include <vector>

#include "orblit_renderer.h"

namespace orblit_scene {

// A borrowed typed list: the codec's own storage, which outlives this object
// because the decoded message passed to a method-call handler is alive for
// the whole of that call and the scene is applied inside it. Empty is
// (nullptr, 0), which is what every optional ABI array takes for "nothing to
// say".
template <typename T>
struct List {
  const T* data = nullptr;
  size_t length = 0;

  const T* ptr() const { return data; }
  size_t count() const { return length; }
  uint32_t count32() const { return static_cast<uint32_t>(length); }
};

using Floats = List<float>;
using Ints = List<int32_t>;
using Longs = List<int64_t>;
using Bytes = List<uint8_t>;

// A list of strings, kept two ways: the copies that own the characters, and
// the `const char* const*` the ABI reads. Both are needed -- the ABI wants
// the pointer array, and something has to keep the strings alive under it.
struct Strings {
  std::vector<std::string> owned;
  std::vector<const char*> pointers;

  const char* const* ptr() const {
    return pointers.empty() ? nullptr : pointers.data();
  }
  uint32_t count() const { return static_cast<uint32_t>(owned.size()); }
};

// Everything a scene holds, and the one thing done with it. Not constructed
// directly: each plugin derives from this and adds the `From` that fills it
// in, which is why the fields are protected rather than private.
class SceneData {
 public:
  // Applies every part of the scene, in the order Viewport.write(scene:)
  // does on the Swift side and OrblitScene.applyTo does on the Kotlin one.
  void ApplyTo(orblit_renderer* renderer) const;

 protected:
  SceneData() = default;

  uint32_t count_ = 0;

  Longs keys_;
  Floats transforms_;
  Floats colours_;
  Ints meshes_;
  Ints flags_;
  Strings paths_;
  // Owned rather than borrowed, because Dart may leave them out and the
  // default is not "empty" but "one -1 per object" -- the same defaults
  // OrblitScene.kt fills in.
  std::vector<int32_t> object_materials_;
  std::vector<int32_t> object_morph_counts_;
  Floats object_morph_weights_;

  Longs material_keys_;
  Ints material_flags_;
  Floats material_params_;
  Ints material_maps_;
  Strings texture_paths_;
  Ints texture_srgb_;
  std::vector<int32_t> material_videos_;

  Longs video_keys_;
  Ints video_flags_;
  Floats video_params_;
  Strings video_paths_;

  Longs light_keys_;
  Ints light_kinds_;
  Ints light_flags_;
  Floats light_params_;

  Longs probe_keys_;
  Floats probe_params_;

  Floats field_params_;
  std::string field_from_;

  Floats camera_position_;
  Floats camera_target_;
  float field_of_view_ = 45.0f;
  float aperture_ = 16.0f;
  float shutter_speed_ = 1.0f / 125.0f;
  float sensitivity_ = 100.0f;
  bool orthographic_ = false;
  float view_height_ = 1.0f;
  double at_ = 0.0;

  Floats sky_colour_;
  float ambient_ = 0.0f;
  bool show_body_ = false;

  bool fog_enabled_ = false;
  Floats fog_params_;
  bool precipitation_enabled_ = false;
  Floats precipitation_params_;
  bool sky_enabled_ = false;
  Floats sky_params_;
  bool batching_ = false;

  Floats post_params_;
  Floats pipeline_params_;

  std::string environment_radiance_;
  std::string environment_skybox_;
  Floats environment_params_;

  Floats graph_passes_;
  Floats graph_targets_;
  Strings graph_target_names_;

  Longs outline_keys_;
  Floats outline_params_;
  Floats god_ray_params_;
  Floats distortion_params_;

  Ints population_keys_;
  Ints population_counts_;
  Ints population_meshes_;
  Ints population_flags_;
  Ints population_revisions_;
  Floats population_ranges_;
  Floats population_bounds_;
  Strings population_paths_;
  Ints population_changed_;
  Floats population_transforms_;
  Floats population_colours_;

  Floats decal_params_;
  Ints decal_images_;
  Strings decal_paths_;

  Ints splat_keys_;
  Ints splat_flags_;
  Ints splat_revisions_;
  Floats splat_params_;
  Strings splat_paths_;
  Ints splat_changed_;
  Ints splat_changed_counts_;
  Bytes splat_data_;

  Ints sprite_keys_;
  Ints sprite_flags_;
  Ints sprite_orders_;
  Ints sprite_revisions_;
  Floats sprite_params_;
  Strings sprite_paths_;
  Ints sprite_changed_;
  Ints sprite_changed_counts_;
  Floats sprite_data_;

  // Terrains: three arrays read in step, which the ABI measures whole. Absent
  // altogether when there is none, which reads as none.
  Ints terrain_ints_;
  Floats terrain_floats_;
  Bytes terrain_data_;

  // Poses: after the objects, which they address by key. Absent altogether
  // when nothing is posed, which reads as none.
  Longs pose_keys_;
  Ints pose_ints_;
  Floats pose_floats_;
  Ints pose_joint_counts_;
  Ints pose_joints_;
  Floats pose_joint_transforms_;
};

}  // namespace orblit_scene
