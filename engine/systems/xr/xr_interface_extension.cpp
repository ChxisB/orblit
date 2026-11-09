/**************************************************************************/
/*  xr_interface_extension.cpp                                            */

#include "xr_interface_extension.h"

#include "core/object/class_registry.h"
#ifdef RD_ENABLED
#include "systems/rendering/renderer_rd/storage_rd/texture_storage.h"
#endif

void XRInterfaceExtension::register_members() {
	ORBLIT_VIRTUAL_BIND(_get_name);
	ORBLIT_VIRTUAL_BIND(_get_capabilities);

	ORBLIT_VIRTUAL_BIND(_is_initialized);
	ORBLIT_VIRTUAL_BIND(_initialize);
	ORBLIT_VIRTUAL_BIND(_uninitialize);
	ORBLIT_VIRTUAL_BIND(_get_system_info);

	ORBLIT_VIRTUAL_BIND(_supports_play_area_mode, "mode");
	ORBLIT_VIRTUAL_BIND(_get_play_area_mode);
	ORBLIT_VIRTUAL_BIND(_set_play_area_mode, "mode");
	ORBLIT_VIRTUAL_BIND(_get_play_area);

	ORBLIT_VIRTUAL_BIND(_get_render_target_size);
	ORBLIT_VIRTUAL_BIND(_get_view_count);
	ORBLIT_VIRTUAL_BIND(_get_camera_transform);
	ORBLIT_VIRTUAL_BIND(_get_camera_projections, "tracker_name", "aspect", "z_near", "z_far");
	ORBLIT_VIRTUAL_BIND(_get_camera_offsets, "tracker_name");
	ORBLIT_VIRTUAL_BIND(_get_vrs_texture);
	ORBLIT_VIRTUAL_BIND(_get_vrs_texture_format);

	ORBLIT_VIRTUAL_BIND(_process);
	ORBLIT_VIRTUAL_BIND(_pre_render);
	ORBLIT_VIRTUAL_BIND(_pre_draw_viewport, "render_target");
	ORBLIT_VIRTUAL_BIND(_post_draw_viewport, "render_target", "screen_rect");
	ORBLIT_VIRTUAL_BIND(_end_frame);

	/** input and output **/

	ORBLIT_VIRTUAL_BIND(_get_suggested_tracker_names);
	ORBLIT_VIRTUAL_BIND(_get_suggested_pose_names, "tracker_name");
	ORBLIT_VIRTUAL_BIND(_get_tracking_status);
	ORBLIT_VIRTUAL_BIND(_trigger_haptic_pulse, "action_name", "tracker_name", "frequency", "amplitude", "duration_sec", "delay_sec");

	// we don't have any properties specific to VR yet....

	// but we do have properties specific to AR....
	ORBLIT_VIRTUAL_BIND(_get_anchor_detection_is_enabled);
	ORBLIT_VIRTUAL_BIND(_set_anchor_detection_is_enabled, "enabled");
	ORBLIT_VIRTUAL_BIND(_get_camera_feed_id);

	// override output methods
	ORBLIT_VIRTUAL_BIND(_get_color_texture);
	ORBLIT_VIRTUAL_BIND(_get_depth_texture);
	ORBLIT_VIRTUAL_BIND(_get_velocity_texture);

	ClassRegistry::bind_method(D_METHOD("get_color_texture"), &XRInterfaceExtension::get_color_texture);
	ClassRegistry::bind_method(D_METHOD("get_depth_texture"), &XRInterfaceExtension::get_depth_texture);
	ClassRegistry::bind_method(D_METHOD("get_velocity_texture"), &XRInterfaceExtension::get_velocity_texture);

	// helper methods
	ClassRegistry::bind_method(D_METHOD("add_blit", "render_target", "src_rect", "dst_rect", "use_layer", "layer", "apply_lens_distortion", "eye_center", "k1", "k2", "upscale", "aspect_ratio"), &XRInterfaceExtension::add_blit);
	ClassRegistry::bind_method(D_METHOD("get_render_target_texture", "render_target"), &XRInterfaceExtension::get_render_target_texture);
	// ClassRegistry::bind_method(D_METHOD("get_render_target_depth", "render_target"), &XRInterfaceExtension::get_render_target_depth);
}

StringName XRInterfaceExtension::get_name() const {
	StringName name;

	if (ORBLIT_VIRTUAL_CALL(_get_name, name)) {
		return name;
	}

	return "Unknown";
}

uint32_t XRInterfaceExtension::get_capabilities() const {
	uint32_t capabilities = 0;
	ORBLIT_VIRTUAL_CALL(_get_capabilities, capabilities);
	return capabilities;
}

bool XRInterfaceExtension::is_initialized() const {
	bool initialized = false;
	ORBLIT_VIRTUAL_CALL(_is_initialized, initialized);
	return initialized;
}

bool XRInterfaceExtension::initialize() {
	bool initialized = false;
	ORBLIT_VIRTUAL_CALL(_initialize, initialized);
	return initialized;
}

void XRInterfaceExtension::uninitialize() {
	ORBLIT_VIRTUAL_CALL(_uninitialize);
}

Dictionary XRInterfaceExtension::get_system_info() {
	Dictionary dict;
	ORBLIT_VIRTUAL_CALL(_get_system_info, dict);
	return dict;
}

PackedStringArray XRInterfaceExtension::get_suggested_tracker_names() const {
	PackedStringArray arr;

	ORBLIT_VIRTUAL_CALL(_get_suggested_tracker_names, arr);

	return arr;
}

PackedStringArray XRInterfaceExtension::get_suggested_pose_names(const StringName &p_tracker_name) const {
	PackedStringArray arr;

	ORBLIT_VIRTUAL_CALL(_get_suggested_pose_names, p_tracker_name, arr);

	return arr;
}

XRInterface::TrackingStatus XRInterfaceExtension::get_tracking_status() const {
	XRInterface::TrackingStatus status = XR_UNKNOWN_TRACKING;
	ORBLIT_VIRTUAL_CALL(_get_tracking_status, status);
	return status;
}

void XRInterfaceExtension::trigger_haptic_pulse(const String &p_action_name, const StringName &p_tracker_name, double p_frequency, double p_amplitude, double p_duration_sec, double p_delay_sec) {
	ORBLIT_VIRTUAL_CALL(_trigger_haptic_pulse, p_action_name, p_tracker_name, p_frequency, p_amplitude, p_duration_sec, p_delay_sec);
}

bool XRInterfaceExtension::supports_play_area_mode(XRInterface::PlayAreaMode p_mode) {
	bool is_supported = false;
	ORBLIT_VIRTUAL_CALL(_supports_play_area_mode, p_mode, is_supported);
	return is_supported;
}

XRInterface::PlayAreaMode XRInterfaceExtension::get_play_area_mode() const {
	XRInterface::PlayAreaMode mode = XR_PLAY_AREA_UNKNOWN;
	ORBLIT_VIRTUAL_CALL(_get_play_area_mode, mode);
	return mode;
}

bool XRInterfaceExtension::set_play_area_mode(XRInterface::PlayAreaMode p_mode) {
	bool success = false;
	ORBLIT_VIRTUAL_CALL(_set_play_area_mode, p_mode, success);
	return success;
}

PackedVector3Array XRInterfaceExtension::get_play_area() const {
	PackedVector3Array arr;
	ORBLIT_VIRTUAL_CALL(_get_play_area, arr);
	return arr;
}

/** these will only be implemented on AR interfaces, so we want dummies for VR **/
bool XRInterfaceExtension::get_anchor_detection_is_enabled() const {
	bool enabled = false;
	ORBLIT_VIRTUAL_CALL(_get_anchor_detection_is_enabled, enabled);
	return enabled;
}

void XRInterfaceExtension::set_anchor_detection_is_enabled(bool p_enable) {
	// don't do anything here, this needs to be implemented on AR interface to enable/disable things like plane detection etc.
	ORBLIT_VIRTUAL_CALL(_set_anchor_detection_is_enabled, p_enable);
}

int XRInterfaceExtension::get_camera_feed_id() {
	int feed_id = 0;
	ORBLIT_VIRTUAL_CALL(_get_camera_feed_id, feed_id);
	return feed_id;
}

Size2 XRInterfaceExtension::get_render_target_size() {
	Size2 size;
	ORBLIT_VIRTUAL_CALL(_get_render_target_size, size);
	return size;
}

uint32_t XRInterfaceExtension::get_view_count() {
	uint32_t view_count = 1;
	ORBLIT_VIRTUAL_CALL(_get_view_count, view_count);
	return view_count;
}

Transform3D XRInterfaceExtension::get_camera_transform() {
	Transform3D transform;
	ORBLIT_VIRTUAL_CALL(_get_camera_transform, transform);
	return transform;
}

TypedArray<Projection> XRInterfaceExtension::get_camera_projections(const StringName &p_tracker_name, double p_aspect, double p_z_near, double p_z_far) {
	TypedArray<Projection> camera_projections;

	if (ORBLIT_VIRTUAL_CALL(_get_camera_projections, p_tracker_name, p_aspect, p_z_near, p_z_far, camera_projections)) {
		return camera_projections;
	}

	return camera_projections;
}

TypedArray<Transform3D> XRInterfaceExtension::get_camera_offsets(const StringName &p_tracker_name) {
	TypedArray<Transform3D> camera_offsets;

	if (ORBLIT_VIRTUAL_CALL(_get_camera_offsets, p_tracker_name, camera_offsets)) {
		return camera_offsets;
	}

	return camera_offsets;
}

RID XRInterfaceExtension::get_vrs_texture() {
	RID vrs_texture;
	if (ORBLIT_VIRTUAL_CALL(_get_vrs_texture, vrs_texture)) {
		return vrs_texture;
	} else {
		return XRInterface::get_vrs_texture();
	}
}

XRInterface::VRSTextureFormat XRInterfaceExtension::get_vrs_texture_format() {
	VRSTextureFormat vrs_texture_format = XR_VRS_TEXTURE_FORMAT_UNIFIED;
	if (ORBLIT_VIRTUAL_CALL(_get_vrs_texture_format, vrs_texture_format)) {
		return vrs_texture_format;
	}
	return vrs_texture_format;
}

RID XRInterfaceExtension::get_color_texture() {
	RID texture;
	ORBLIT_VIRTUAL_CALL(_get_color_texture, texture);
	return texture;
}

RID XRInterfaceExtension::get_depth_texture() {
	RID texture;
	ORBLIT_VIRTUAL_CALL(_get_depth_texture, texture);
	return texture;
}

RID XRInterfaceExtension::get_velocity_texture() {
	RID texture;
	ORBLIT_VIRTUAL_CALL(_get_velocity_texture, texture);
	return texture;
}

void XRInterfaceExtension::add_blit(RID p_render_target, Rect2 p_src_rect, Rect2i p_dst_rect, bool p_use_layer, uint32_t p_layer, bool p_apply_lens_distortion, Vector2 p_eye_center, double p_k1, double p_k2, double p_upscale, double p_aspect_ratio) {
	RenderingSystemTypes::BlitToScreen blit;

	ERR_FAIL_COND_MSG(!can_add_blits, "add_blit can only be called from an XR plugin from within _post_draw_viewport!");

	blit.render_target = p_render_target;
	blit.src_rect = p_src_rect;
	blit.dst_rect = p_dst_rect;

	blit.multi_view.use_layer = p_use_layer;
	blit.multi_view.layer = p_layer;

	blit.lens_distortion.apply = p_apply_lens_distortion;
	blit.lens_distortion.eye_center = p_eye_center;
	blit.lens_distortion.k1 = p_k1;
	blit.lens_distortion.k2 = p_k2;
	blit.lens_distortion.upscale = p_upscale;
	blit.lens_distortion.aspect_ratio = p_aspect_ratio;

	blits.push_back(blit);
}

void XRInterfaceExtension::process() {
	ORBLIT_VIRTUAL_CALL(_process);
}

void XRInterfaceExtension::pre_render() {
	ORBLIT_VIRTUAL_CALL(_pre_render);
}

bool XRInterfaceExtension::pre_draw_viewport(RID p_render_target) {
	bool do_render = true;
	ORBLIT_VIRTUAL_CALL(_pre_draw_viewport, p_render_target, do_render);
	return do_render; // If not implemented we're returning true.
}

Vector<RenderingSystemTypes::BlitToScreen> XRInterfaceExtension::post_draw_viewport(RID p_render_target, const Rect2 &p_screen_rect) {
	// This is just so our XR plugin can add blits...
	blits.clear();
	can_add_blits = true;

	if (ORBLIT_VIRTUAL_CALL(_post_draw_viewport, p_render_target, p_screen_rect)) {
		return blits;
	}

	can_add_blits = false;
	return blits;
}

void XRInterfaceExtension::end_frame() {
	ORBLIT_VIRTUAL_CALL(_end_frame);
}

RID XRInterfaceExtension::get_render_target_texture(RID p_render_target) {
	// In due time this will need to be enhance to return the correct INTERNAL RID for the chosen rendering engine.
	// So once a GLES driver is implemented we'll return that and the implemented plugin needs to handle this correctly too.
#ifdef RD_ENABLED
	RendererRD::TextureStorage *texture_storage = RendererRD::TextureStorage::get_singleton();
	ERR_FAIL_NULL_V_MSG(texture_storage, RID(), "Texture storage not setup");

	return texture_storage->render_target_get_rd_texture(p_render_target);
#else
	return RID();
#endif // RD_ENABLED
}

/*
RID XRInterfaceExtension::get_render_target_depth(RID p_render_target) {
	// TODO implement this, the problem is that our depth texture isn't part of our render target as it is used for 3D rendering only
	// but we don't have access to our render buffers from here....
}
*/
