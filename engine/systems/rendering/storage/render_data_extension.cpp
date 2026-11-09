/**************************************************************************/
/*  render_data_extension.cpp                                             */

#include "render_data_extension.h"

#include "core/object/class_registry.h" // IWYU pragma: keep. `ORBLIT_VIRTUAL_BIND` macro.
#include "systems/rendering/rendering_system.h" // IWYU pragma: keep // Needed to bind RSE enums.

// RenderSceneBuffersExtension

void RenderSceneBuffersExtension::register_members() {
	ORBLIT_VIRTUAL_BIND(_configure, "config");
	ORBLIT_VIRTUAL_BIND(_set_fsr_sharpness, "fsr_sharpness");
	ORBLIT_VIRTUAL_BIND(_set_texture_mipmap_bias, "texture_mipmap_bias");
	ORBLIT_VIRTUAL_BIND(_set_anisotropic_filtering_level, "anisotropic_filtering_level");
	ORBLIT_VIRTUAL_BIND(_set_use_debanding, "use_debanding");
}

void RenderSceneBuffersExtension::configure(const RenderSceneBuffersConfiguration *p_config) {
	ORBLIT_VIRTUAL_CALL(_configure, p_config);
}

void RenderSceneBuffersExtension::set_fsr_sharpness(float p_fsr_sharpness) {
	ORBLIT_VIRTUAL_CALL(_set_fsr_sharpness, p_fsr_sharpness);
}

void RenderSceneBuffersExtension::set_texture_mipmap_bias(float p_texture_mipmap_bias) {
	ORBLIT_VIRTUAL_CALL(_set_texture_mipmap_bias, p_texture_mipmap_bias);
}

void RenderSceneBuffersExtension::set_anisotropic_filtering_level(RSE::ViewportAnisotropicFiltering p_anisotropic_filtering_level) {
	ORBLIT_VIRTUAL_CALL(_set_anisotropic_filtering_level, p_anisotropic_filtering_level);
}

void RenderSceneBuffersExtension::set_use_debanding(bool p_use_debanding) {
	ORBLIT_VIRTUAL_CALL(_set_use_debanding, p_use_debanding);
}

// RenderSceneDataExtension

void RenderSceneDataExtension::register_members() {
	ORBLIT_VIRTUAL_BIND(_get_cam_transform);
	ORBLIT_VIRTUAL_BIND(_get_cam_projection);
	ORBLIT_VIRTUAL_BIND(_get_view_count);
	ORBLIT_VIRTUAL_BIND(_get_view_eye_offset, "view");
	ORBLIT_VIRTUAL_BIND(_get_view_projection, "view");

	ORBLIT_VIRTUAL_BIND(_get_uniform_buffer);
}

Transform3D RenderSceneDataExtension::get_cam_transform() const {
	Transform3D ret;
	ORBLIT_VIRTUAL_CALL(_get_cam_transform, ret);
	return ret;
}

Projection RenderSceneDataExtension::get_cam_projection() const {
	Projection ret;
	ORBLIT_VIRTUAL_CALL(_get_cam_projection, ret);
	return ret;
}

uint32_t RenderSceneDataExtension::get_view_count() const {
	uint32_t ret = 0;
	ORBLIT_VIRTUAL_CALL(_get_view_count, ret);
	return ret;
}

Vector3 RenderSceneDataExtension::get_view_eye_offset(uint32_t p_view) const {
	Vector3 ret;
	ORBLIT_VIRTUAL_CALL(_get_view_eye_offset, p_view, ret);
	return ret;
}

Projection RenderSceneDataExtension::get_view_projection(uint32_t p_view) const {
	Projection ret;
	ORBLIT_VIRTUAL_CALL(_get_view_projection, p_view, ret);
	return ret;
}

RID RenderSceneDataExtension::get_uniform_buffer() const {
	RID ret;
	ORBLIT_VIRTUAL_CALL(_get_uniform_buffer, ret);
	return ret;
}

// RenderDataExtension

void RenderDataExtension::register_members() {
	ORBLIT_VIRTUAL_BIND(_get_render_scene_buffers);
	ORBLIT_VIRTUAL_BIND(_get_render_scene_data)
	ORBLIT_VIRTUAL_BIND(_get_environment)
	ORBLIT_VIRTUAL_BIND(_get_camera_attributes)
}

Ref<RenderSceneBuffers> RenderDataExtension::get_render_scene_buffers() const {
	Ref<RenderSceneBuffers> ret;
	ORBLIT_VIRTUAL_CALL(_get_render_scene_buffers, ret);
	return ret;
}

RenderSceneData *RenderDataExtension::get_render_scene_data() const {
	RenderSceneData *ret = nullptr;
	ORBLIT_VIRTUAL_CALL(_get_render_scene_data, ret);
	return ret;
}

RID RenderDataExtension::get_environment() const {
	RID ret;
	ORBLIT_VIRTUAL_CALL(_get_environment, ret);
	return ret;
}

RID RenderDataExtension::get_camera_attributes() const {
	RID ret;
	ORBLIT_VIRTUAL_CALL(_get_camera_attributes, ret);
	return ret;
}
