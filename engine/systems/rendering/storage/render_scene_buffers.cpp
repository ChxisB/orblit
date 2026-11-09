/**************************************************************************/
/*  render_scene_buffers.cpp                                              */

#include "render_scene_buffers.h"

#include "core/object/class_registry.h"
#include "systems/rendering/rendering_system.h" // IWYU pragma: keep // Needed to bind RSE enums.

void RenderSceneBuffersConfiguration::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_render_target"), &RenderSceneBuffersConfiguration::get_render_target);
	ClassRegistry::bind_method(D_METHOD("set_render_target", "render_target"), &RenderSceneBuffersConfiguration::set_render_target);
	ADD_PROPERTY(PropertyInfo(Variant::RID, "render_target"), "set_render_target", "get_render_target");

	ClassRegistry::bind_method(D_METHOD("get_internal_size"), &RenderSceneBuffersConfiguration::get_internal_size);
	ClassRegistry::bind_method(D_METHOD("set_internal_size", "internal_size"), &RenderSceneBuffersConfiguration::set_internal_size);
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2I, "internal_size"), "set_internal_size", "get_internal_size");

	ClassRegistry::bind_method(D_METHOD("get_target_size"), &RenderSceneBuffersConfiguration::get_target_size);
	ClassRegistry::bind_method(D_METHOD("set_target_size", "target_size"), &RenderSceneBuffersConfiguration::set_target_size);
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2I, "target_size"), "set_target_size", "get_target_size");

	ClassRegistry::bind_method(D_METHOD("get_view_count"), &RenderSceneBuffersConfiguration::get_view_count);
	ClassRegistry::bind_method(D_METHOD("set_view_count", "view_count"), &RenderSceneBuffersConfiguration::set_view_count);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "view_count"), "set_view_count", "get_view_count");

	ClassRegistry::bind_method(D_METHOD("get_scaling_3d_mode"), &RenderSceneBuffersConfiguration::get_scaling_3d_mode);
	ClassRegistry::bind_method(D_METHOD("set_scaling_3d_mode", "scaling_3d_mode"), &RenderSceneBuffersConfiguration::set_scaling_3d_mode);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "scaling_3d_mode", PROPERTY_HINT_ENUM, "Nearest (Fastest):5,Bilinear (Fastest):0,FSR 1.0 (Fast):1,FSR 2.2 (Slow):2,MetalFX (Spatial - Fast):3,MetalFX (Temporal - Slow):4"), "set_scaling_3d_mode", "get_scaling_3d_mode"); // TODO VIEWPORT_SCALING_3D_MODE_OFF is possible here too, but we can't specify an enum string for it.

	ClassRegistry::bind_method(D_METHOD("get_msaa_3d"), &RenderSceneBuffersConfiguration::get_msaa_3d);
	ClassRegistry::bind_method(D_METHOD("set_msaa_3d", "msaa_3d"), &RenderSceneBuffersConfiguration::set_msaa_3d);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "msaa_3d", PROPERTY_HINT_ENUM, "Disabled,2x,4x,8x"), "set_msaa_3d", "get_msaa_3d");

	ClassRegistry::bind_method(D_METHOD("get_screen_space_aa"), &RenderSceneBuffersConfiguration::get_screen_space_aa);
	ClassRegistry::bind_method(D_METHOD("set_screen_space_aa", "screen_space_aa"), &RenderSceneBuffersConfiguration::set_screen_space_aa);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "screen_space_aa", PROPERTY_HINT_ENUM, "Disabled,FXAA,SMAA"), "set_screen_space_aa", "get_screen_space_aa");

	ClassRegistry::bind_method(D_METHOD("get_fsr_sharpness"), &RenderSceneBuffersConfiguration::get_fsr_sharpness);
	ClassRegistry::bind_method(D_METHOD("set_fsr_sharpness", "fsr_sharpness"), &RenderSceneBuffersConfiguration::set_fsr_sharpness);
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "fsr_sharpness"), "set_fsr_sharpness", "get_fsr_sharpness");

	ClassRegistry::bind_method(D_METHOD("get_texture_mipmap_bias"), &RenderSceneBuffersConfiguration::get_texture_mipmap_bias);
	ClassRegistry::bind_method(D_METHOD("set_texture_mipmap_bias", "texture_mipmap_bias"), &RenderSceneBuffersConfiguration::set_texture_mipmap_bias);
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "texture_mipmap_bias"), "set_texture_mipmap_bias", "get_texture_mipmap_bias");

	ClassRegistry::bind_method(D_METHOD("get_anisotropic_filtering_level"), &RenderSceneBuffersConfiguration::get_anisotropic_filtering_level);
	ClassRegistry::bind_method(D_METHOD("set_anisotropic_filtering_level", "anisotropic_filtering_level"), &RenderSceneBuffersConfiguration::set_anisotropic_filtering_level);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "anisotropic_filtering_level"), "set_anisotropic_filtering_level", "get_anisotropic_filtering_level");
}

void RenderSceneBuffers::register_members() {
	ClassRegistry::bind_method(D_METHOD("configure", "config"), &RenderSceneBuffers::configure);
}
