#include "orblit_scene_common.h"

// Handing a parsed scene to the renderer, one part at a time.
//
// The order matters and is the order the other three platforms use: objects
// and their materials before the things that address them by key, the camera
// and the environment after, poses last. Shared by the Linux and Windows
// plugins; see orblit_scene_common.h.

namespace orblit_scene {

void SceneData::ApplyTo(orblit_renderer* renderer) const {
  orblit_renderer_set_environment(renderer, environment_radiance_.c_str(),
                                 environment_skybox_.c_str(),
                                 environment_params_.ptr(),
                                 environment_params_.count());

  // Strides are the ABI's own, so the counts are derived here rather than
  // sent -- the same arithmetic orblit_jni.cpp does, for the same reason.
  const uint32_t pass_stride = orblit_renderer_stride(ORBLIT_STRIDE_PASS);
  const uint32_t target_stride = orblit_renderer_stride(ORBLIT_STRIDE_TARGET);
  const uint32_t pass_count =
      pass_stride > 0 ? graph_passes_.count32() / pass_stride : 0;
  const uint32_t target_count =
      target_stride > 0 ? graph_targets_.count32() / target_stride : 0;
  orblit_renderer_set_render_graph(
      renderer, pass_count, graph_passes_.ptr(), graph_passes_.count(),
      target_count, graph_targets_.ptr(), graph_targets_.count(),
      graph_target_names_.ptr(), graph_target_names_.count());

  orblit_renderer_set_batching(renderer, batching_ ? 1 : 0);
  orblit_renderer_set_god_rays(renderer, god_ray_params_.ptr(),
                              god_ray_params_.count(), distortion_params_.ptr(),
                              distortion_params_.count());

  orblit_renderer_apply_videos(renderer, video_keys_.count32(),
                              video_keys_.ptr(), video_flags_.ptr(),
                              video_params_.ptr(), video_params_.count(),
                              video_paths_.ptr(), video_paths_.count());

  orblit_renderer_apply_materials(
      renderer, material_keys_.count32(), material_keys_.ptr(),
      material_flags_.ptr(), material_params_.ptr(), material_params_.count(),
      material_maps_.ptr(), material_maps_.count(), texture_paths_.ptr(),
      texture_srgb_.ptr(), texture_paths_.count(), material_videos_.data());

  orblit_renderer_apply_objects(
      renderer, count_, keys_.ptr(), transforms_.ptr(), transforms_.count(),
      colours_.ptr(), colours_.count(), meshes_.ptr(), flags_.ptr(),
      object_materials_.data(), object_morph_counts_.data(),
      object_morph_weights_.ptr(), object_morph_weights_.count(), paths_.ptr(),
      paths_.count());

  // Poses address the objects just applied by key, so they come straight
  // after them -- and every time, even with none, so an object that stops
  // being posed goes back to rest. The ABI checks every length against the
  // count and the joint counts.
  orblit_renderer_apply_poses(
      renderer, pose_keys_.count32(), pose_keys_.ptr(), pose_ints_.ptr(),
      pose_ints_.count(), pose_floats_.ptr(), pose_floats_.count(),
      pose_joint_counts_.ptr(), pose_joints_.ptr(), pose_joints_.count(),
      pose_joint_transforms_.ptr(), pose_joint_transforms_.count(), at_);

  if (population_keys_.count() > 0) {
    orblit_renderer_apply_populations(
        renderer, population_keys_.count32(), population_keys_.ptr(),
        population_counts_.ptr(), population_meshes_.ptr(),
        population_flags_.ptr(), population_revisions_.ptr(),
        population_ranges_.ptr(), population_bounds_.ptr(),
        population_bounds_.count(), population_paths_.ptr(),
        population_paths_.count(), population_changed_.ptr(),
        population_changed_.count32(), population_transforms_.ptr(),
        population_transforms_.count(), population_colours_.ptr(),
        population_colours_.count());
  }

  orblit_renderer_apply_splats(
      renderer, splat_keys_.count32(), splat_keys_.ptr(), splat_flags_.ptr(),
      splat_revisions_.ptr(), splat_params_.ptr(), splat_params_.count(),
      splat_paths_.ptr(), splat_paths_.count(), splat_changed_.ptr(),
      splat_changed_counts_.ptr(), splat_changed_.count32(),
      splat_data_.ptr(), splat_data_.count());

  orblit_renderer_apply_sprites(
      renderer, sprite_keys_.count32(), sprite_keys_.ptr(), sprite_flags_.ptr(),
      sprite_orders_.ptr(), sprite_revisions_.ptr(), sprite_params_.ptr(),
      sprite_params_.count(), sprite_paths_.ptr(), sprite_paths_.count(),
      sprite_changed_.ptr(), sprite_changed_counts_.ptr(),
      sprite_changed_.count32(), sprite_data_.ptr(), sprite_data_.count());

  orblit_renderer_apply_lights(renderer, light_keys_.count32(),
                              light_keys_.ptr(), light_kinds_.ptr(),
                              light_flags_.ptr(), light_params_.ptr(),
                              light_params_.count());

  // Decals are counted by their images, as the JNI bridge counts them: one
  // image index per decal, whether or not it names a picture.
  orblit_renderer_apply_decals(renderer, decal_images_.count32(),
                              decal_params_.ptr(), decal_params_.count(),
                              decal_images_.ptr(), decal_paths_.ptr(),
                              decal_paths_.count());

  orblit_renderer_apply_probes(renderer, probe_keys_.count32(),
                              probe_keys_.ptr(), probe_params_.ptr(),
                              probe_params_.count());

  if (field_params_.count() > 0) {
    orblit_renderer_apply_field(renderer, field_params_.ptr(),
                               field_params_.count(), field_from_.c_str());
  }

  if (sky_colour_.count() >= 3) {
    orblit_renderer_set_sky_colour(renderer, sky_colour_.ptr(), ambient_,
                                  show_body_ ? 1 : 0);
  }
  orblit_renderer_set_fog(renderer, fog_enabled_ ? 1 : 0, fog_params_.ptr(),
                         fog_params_.count());
  if (post_params_.count() > 0) {
    orblit_renderer_set_post_process(renderer, post_params_.ptr(),
                                    post_params_.count());
  }
  if (pipeline_params_.count() > 0) {
    orblit_renderer_set_pipeline(renderer, pipeline_params_.ptr(),
                                pipeline_params_.count());
  }
  orblit_renderer_set_precipitation(renderer, precipitation_enabled_ ? 1 : 0,
                                   precipitation_params_.ptr(),
                                   precipitation_params_.count());
  orblit_renderer_set_sky(renderer, sky_enabled_ ? 1 : 0, sky_params_.ptr(),
                         sky_params_.count());

  orblit_renderer_set_camera(renderer, camera_position_.ptr(),
                            camera_target_.ptr(), field_of_view_,
                            orthographic_ ? 1 : 0, view_height_, at_);
  orblit_renderer_set_exposure(renderer, aperture_, shutter_speed_,
                              sensitivity_);

  orblit_renderer_set_outline(renderer, outline_keys_.ptr(),
                             outline_keys_.count32(), outline_params_.ptr(),
                             outline_params_.count());
}

}  // namespace orblit_scene
