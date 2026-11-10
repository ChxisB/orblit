/**************************************************************************/
/*  xr_interface.cpp                                                      */

#include "xr_interface.h"

#include "core/object/class_registry.h"
#include "systems/xr/xr_system.h"

void XRInterface::register_members() {
	ADD_SIGNAL(MethodInfo("play_area_changed", PropertyInfo(Variant::INT, "mode")));

	ClassRegistry::bind_method(D_METHOD("get_name"), &XRInterface::get_name);
	ClassRegistry::bind_method(D_METHOD("get_capabilities"), &XRInterface::get_capabilities);

	ClassRegistry::bind_method(D_METHOD("is_primary"), &XRInterface::is_primary);
	ClassRegistry::bind_method(D_METHOD("set_primary", "primary"), &XRInterface::set_primary);

	ClassRegistry::bind_method(D_METHOD("is_initialized"), &XRInterface::is_initialized);
	ClassRegistry::bind_method(D_METHOD("initialize"), &XRInterface::initialize);
	ClassRegistry::bind_method(D_METHOD("uninitialize"), &XRInterface::uninitialize);
	ClassRegistry::bind_method(D_METHOD("get_system_info"), &XRInterface::get_system_info);

	ClassRegistry::bind_method(D_METHOD("get_tracking_status"), &XRInterface::get_tracking_status);

	ClassRegistry::bind_method(D_METHOD("get_render_target_size"), &XRInterface::get_render_target_size);
	ClassRegistry::bind_method(D_METHOD("get_view_count"), &XRInterface::get_view_count);

	ClassRegistry::bind_method(D_METHOD("trigger_haptic_pulse", "action_name", "tracker_name", "frequency", "amplitude", "duration_sec", "delay_sec"), &XRInterface::trigger_haptic_pulse);

	ADD_GROUP("Interface", "interface_");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "interface_is_primary"), "set_primary", "is_primary");

	// methods and properties specific to VR...
	ClassRegistry::bind_method(D_METHOD("supports_play_area_mode", "mode"), &XRInterface::supports_play_area_mode);
	ClassRegistry::bind_method(D_METHOD("get_play_area_mode"), &XRInterface::get_play_area_mode);
	ClassRegistry::bind_method(D_METHOD("set_play_area_mode", "mode"), &XRInterface::set_play_area_mode);
	ClassRegistry::bind_method(D_METHOD("get_play_area"), &XRInterface::get_play_area);

	ADD_GROUP("XR", "xr_");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "xr_play_area_mode", PROPERTY_HINT_ENUM, "Unknown,3DOF,Sitting,Roomscale,Stage"), "set_play_area_mode", "get_play_area_mode");

	// methods and properties specific to AR....
	ClassRegistry::bind_method(D_METHOD("get_anchor_detection_is_enabled"), &XRInterface::get_anchor_detection_is_enabled);
	ClassRegistry::bind_method(D_METHOD("set_anchor_detection_is_enabled", "enable"), &XRInterface::set_anchor_detection_is_enabled);
	ClassRegistry::bind_method(D_METHOD("get_camera_feed_id"), &XRInterface::get_camera_feed_id);

	ClassRegistry::bind_method(D_METHOD("get_camera_projections", "tracker_name", "aspect", "near", "far"), &XRInterface::get_camera_projections);
	ClassRegistry::bind_method(D_METHOD("get_camera_offsets", "tracker_name"), &XRInterface::get_camera_offsets);

	/** environment blend mode. */
	ClassRegistry::bind_method(D_METHOD("get_supported_environment_blend_modes"), &XRInterface::get_supported_environment_blend_modes);
	ClassRegistry::bind_method(D_METHOD("set_environment_blend_mode", "mode"), &XRInterface::set_environment_blend_mode);
	ClassRegistry::bind_method(D_METHOD("get_environment_blend_mode"), &XRInterface::get_environment_blend_mode);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "environment_blend_mode"), "set_environment_blend_mode", "get_environment_blend_mode");

	ADD_GROUP("AR", "ar_");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "ar_is_anchor_detection_enabled"), "set_anchor_detection_is_enabled", "get_anchor_detection_is_enabled");

	BIND_ENUM_CONSTANT(XR_NONE);
	BIND_ENUM_CONSTANT(XR_MONO);
	BIND_ENUM_CONSTANT(XR_STEREO);
	BIND_ENUM_CONSTANT(XR_QUAD);
	BIND_ENUM_CONSTANT(XR_VR);
	BIND_ENUM_CONSTANT(XR_AR);
	BIND_ENUM_CONSTANT(XR_EXTERNAL);

	BIND_ENUM_CONSTANT(XR_NORMAL_TRACKING);
	BIND_ENUM_CONSTANT(XR_EXCESSIVE_MOTION);
	BIND_ENUM_CONSTANT(XR_INSUFFICIENT_FEATURES);
	BIND_ENUM_CONSTANT(XR_UNKNOWN_TRACKING);
	BIND_ENUM_CONSTANT(XR_NOT_TRACKING);

	BIND_ENUM_CONSTANT(XR_PLAY_AREA_UNKNOWN);
	BIND_ENUM_CONSTANT(XR_PLAY_AREA_3DOF);
	BIND_ENUM_CONSTANT(XR_PLAY_AREA_SITTING);
	BIND_ENUM_CONSTANT(XR_PLAY_AREA_ROOMSCALE);
	BIND_ENUM_CONSTANT(XR_PLAY_AREA_STAGE);
	BIND_ENUM_CONSTANT(XR_PLAY_AREA_CUSTOM);

	BIND_ENUM_CONSTANT(XR_ENV_BLEND_MODE_OPAQUE);
	BIND_ENUM_CONSTANT(XR_ENV_BLEND_MODE_ADDITIVE);
	BIND_ENUM_CONSTANT(XR_ENV_BLEND_MODE_ALPHA_BLEND);

	BIND_ENUM_CONSTANT(XR_VRS_TEXTURE_FORMAT_UNIFIED);
	BIND_ENUM_CONSTANT(XR_VRS_TEXTURE_FORMAT_FRAGMENT_SHADING_RATE);
	BIND_ENUM_CONSTANT(XR_VRS_TEXTURE_FORMAT_FRAGMENT_DENSITY_MAP);
	BIND_ENUM_CONSTANT(XR_VRS_TEXTURE_FORMAT_RASTERIZATION_RATE_MAP);
}

bool XRInterface::is_primary() {
	XRSystem *xr_system = XRSystem::get_singleton();
	ERR_FAIL_NULL_V(xr_system, false);

	return xr_system->get_primary_interface() == this;
}

void XRInterface::set_primary(bool p_primary) {
	XRSystem *xr_system = XRSystem::get_singleton();
	ERR_FAIL_NULL(xr_system);

	if (p_primary) {
		ERR_FAIL_COND(!is_initialized());

		xr_system->set_primary_interface(this);
	} else if (xr_system->get_primary_interface() == this) {
		xr_system->set_primary_interface(nullptr);
	}
}

XRInterface::XRInterface() {}

XRInterface::~XRInterface() {}

// query if this interface supports this play area mode
bool XRInterface::supports_play_area_mode(XRInterface::PlayAreaMode p_mode) {
	return p_mode == XR_PLAY_AREA_UNKNOWN;
}

// get the current play area mode
XRInterface::PlayAreaMode XRInterface::get_play_area_mode() const {
	return XR_PLAY_AREA_UNKNOWN;
}

// change the play area mode, note that this should return false if the mode is not available
bool XRInterface::set_play_area_mode(XRInterface::PlayAreaMode p_mode) {
	return p_mode == XR_PLAY_AREA_UNKNOWN;
}

// if available, returns an array of vectors denoting the play area the player can move around in
PackedVector3Array XRInterface::get_play_area() const {
	// Return an empty array by default.
	// Note implementation is responsible for applying our reference frame and world scale to the raw data.
	// `play_area_changed` should be emitted if play area data is available and either the reference frame or world scale changes.
	return PackedVector3Array();
}

/** these will only be implemented on AR interfaces, so we want dummies for VR **/
bool XRInterface::get_anchor_detection_is_enabled() const {
	return false;
}

void XRInterface::set_anchor_detection_is_enabled(bool p_enable) {
}

int XRInterface::get_camera_feed_id() {
	return 0;
}

RID XRInterface::get_vrs_texture() {
	return RID();
}

/** these are optional, so we want dummies **/

RID XRInterface::get_color_texture() {
	return RID();
}

RID XRInterface::get_depth_texture() {
	return RID();
}

RID XRInterface::get_velocity_texture() {
	return RID();
}

RID XRInterface::get_velocity_depth_texture() {
	return RID();
}

Size2i XRInterface::get_velocity_target_size() {
	return Size2i();
}

Rect2i XRInterface::get_render_region() {
	return Rect2i();
}

PackedStringArray XRInterface::get_suggested_tracker_names() const {
	PackedStringArray arr;

	return arr;
}

PackedStringArray XRInterface::get_suggested_pose_names(const StringName &p_tracker_name) const {
	PackedStringArray arr;

	return arr;
}

XRInterface::TrackingStatus XRInterface::get_tracking_status() const {
	return XR_UNKNOWN_TRACKING;
}

void XRInterface::trigger_haptic_pulse(const String &p_action_name, const StringName &p_tracker_name, double p_frequency, double p_amplitude, double p_duration_sec, double p_delay_sec) {
}

Array XRInterface::get_supported_environment_blend_modes() {
	return Array{ XR_ENV_BLEND_MODE_OPAQUE };
}
