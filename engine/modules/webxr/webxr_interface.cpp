/**************************************************************************/
/*  webxr_interface.cpp                                                   */

#include "webxr_interface.h"

#include "core/object/class_registry.h"

void WebXRInterface::register_members() {
	ClassRegistry::bind_method(D_METHOD("is_session_supported", "session_mode"), &WebXRInterface::is_session_supported);
	ClassRegistry::bind_method(D_METHOD("set_session_mode", "session_mode"), &WebXRInterface::set_session_mode);
	ClassRegistry::bind_method(D_METHOD("get_session_mode"), &WebXRInterface::get_session_mode);
	ClassRegistry::bind_method(D_METHOD("set_required_features", "required_features"), &WebXRInterface::set_required_features);
	ClassRegistry::bind_method(D_METHOD("get_required_features"), &WebXRInterface::get_required_features);
	ClassRegistry::bind_method(D_METHOD("set_optional_features", "optional_features"), &WebXRInterface::set_optional_features);
	ClassRegistry::bind_method(D_METHOD("get_optional_features"), &WebXRInterface::get_optional_features);
	ClassRegistry::bind_method(D_METHOD("get_reference_space_type"), &WebXRInterface::get_reference_space_type);
	ClassRegistry::bind_method(D_METHOD("get_enabled_features"), &WebXRInterface::get_enabled_features);
	ClassRegistry::bind_method(D_METHOD("set_requested_reference_space_types", "requested_reference_space_types"), &WebXRInterface::set_requested_reference_space_types);
	ClassRegistry::bind_method(D_METHOD("get_requested_reference_space_types"), &WebXRInterface::get_requested_reference_space_types);
	ClassRegistry::bind_method(D_METHOD("set_disable_webxr_layers", "disable"), &WebXRInterface::set_disable_webxr_layers);
	ClassRegistry::bind_method(D_METHOD("get_disable_webxr_layers"), &WebXRInterface::get_disable_webxr_layers);
	ClassRegistry::bind_method(D_METHOD("is_input_source_active", "input_source_id"), &WebXRInterface::is_input_source_active);
	ClassRegistry::bind_method(D_METHOD("get_input_source_tracker", "input_source_id"), &WebXRInterface::get_input_source_tracker);
	ClassRegistry::bind_method(D_METHOD("get_input_source_target_ray_mode", "input_source_id"), &WebXRInterface::get_input_source_target_ray_mode);
	ClassRegistry::bind_method(D_METHOD("get_visibility_state"), &WebXRInterface::get_visibility_state);
	ClassRegistry::bind_method(D_METHOD("get_display_refresh_rate"), &WebXRInterface::get_display_refresh_rate);
	ClassRegistry::bind_method(D_METHOD("set_display_refresh_rate", "refresh_rate"), &WebXRInterface::set_display_refresh_rate);
	ClassRegistry::bind_method(D_METHOD("get_available_display_refresh_rates"), &WebXRInterface::get_available_display_refresh_rates);

	ADD_PROPERTY(PropertyInfo(Variant::STRING, "session_mode", PROPERTY_HINT_NONE), "set_session_mode", "get_session_mode");
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "required_features", PROPERTY_HINT_NONE), "set_required_features", "get_required_features");
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "optional_features", PROPERTY_HINT_NONE), "set_optional_features", "get_optional_features");
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "requested_reference_space_types", PROPERTY_HINT_NONE), "set_requested_reference_space_types", "get_requested_reference_space_types");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "disable_webxr_layers", PROPERTY_HINT_NONE), "set_disable_webxr_layers", "get_disable_webxr_layers");
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "reference_space_type", PROPERTY_HINT_NONE), "", "get_reference_space_type");
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "enabled_features", PROPERTY_HINT_NONE), "", "get_enabled_features");
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "visibility_state", PROPERTY_HINT_NONE), "", "get_visibility_state");

	ADD_SIGNAL(MethodInfo("session_supported", PropertyInfo(Variant::STRING, "session_mode"), PropertyInfo(Variant::BOOL, "supported")));
	ADD_SIGNAL(MethodInfo("session_started"));
	ADD_SIGNAL(MethodInfo("session_ended"));
	ADD_SIGNAL(MethodInfo("session_failed", PropertyInfo(Variant::STRING, "message")));

	ADD_SIGNAL(MethodInfo("selectstart", PropertyInfo(Variant::INT, "input_source_id")));
	ADD_SIGNAL(MethodInfo("select", PropertyInfo(Variant::INT, "input_source_id")));
	ADD_SIGNAL(MethodInfo("selectend", PropertyInfo(Variant::INT, "input_source_id")));
	ADD_SIGNAL(MethodInfo("squeezestart", PropertyInfo(Variant::INT, "input_source_id")));
	ADD_SIGNAL(MethodInfo("squeeze", PropertyInfo(Variant::INT, "input_source_id")));
	ADD_SIGNAL(MethodInfo("squeezeend", PropertyInfo(Variant::INT, "input_source_id")));

	ADD_SIGNAL(MethodInfo("visibility_state_changed"));
	ADD_SIGNAL(MethodInfo("reference_space_reset"));
	ADD_SIGNAL(MethodInfo("display_refresh_rate_changed"));

	BIND_ENUM_CONSTANT(TARGET_RAY_MODE_UNKNOWN);
	BIND_ENUM_CONSTANT(TARGET_RAY_MODE_GAZE);
	BIND_ENUM_CONSTANT(TARGET_RAY_MODE_TRACKED_POINTER);
	BIND_ENUM_CONSTANT(TARGET_RAY_MODE_SCREEN);
}
