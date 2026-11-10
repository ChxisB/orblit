/**************************************************************************/
/*  xr_pose.cpp                                                           */

#include "xr_pose.h"

#include "core/object/class_registry.h"
#include "systems/xr/xr_system.h"

void XRPose::register_members() {
	BIND_ENUM_CONSTANT(XR_TRACKING_CONFIDENCE_NONE);
	BIND_ENUM_CONSTANT(XR_TRACKING_CONFIDENCE_LOW);
	BIND_ENUM_CONSTANT(XR_TRACKING_CONFIDENCE_HIGH);

	ClassRegistry::bind_method(D_METHOD("set_has_tracking_data", "has_tracking_data"), &XRPose::set_has_tracking_data);
	ClassRegistry::bind_method(D_METHOD("get_has_tracking_data"), &XRPose::get_has_tracking_data);
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "has_tracking_data"), "set_has_tracking_data", "get_has_tracking_data");

	ClassRegistry::bind_method(D_METHOD("set_name", "name"), &XRPose::set_name);
	ClassRegistry::bind_method(D_METHOD("get_name"), &XRPose::get_name);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "name"), "set_name", "get_name");

	ClassRegistry::bind_method(D_METHOD("set_transform", "transform"), &XRPose::set_transform);
	ClassRegistry::bind_method(D_METHOD("get_transform"), &XRPose::get_transform);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "transform"), "set_transform", "get_transform");
	ClassRegistry::bind_method(D_METHOD("get_adjusted_transform"), &XRPose::get_adjusted_transform);

	ClassRegistry::bind_method(D_METHOD("set_linear_velocity", "velocity"), &XRPose::set_linear_velocity);
	ClassRegistry::bind_method(D_METHOD("get_linear_velocity"), &XRPose::get_linear_velocity);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "linear_velocity"), "set_linear_velocity", "get_linear_velocity");

	ClassRegistry::bind_method(D_METHOD("set_angular_velocity", "velocity"), &XRPose::set_angular_velocity);
	ClassRegistry::bind_method(D_METHOD("get_angular_velocity"), &XRPose::get_angular_velocity);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "angular_velocity"), "set_angular_velocity", "get_angular_velocity");

	ClassRegistry::bind_method(D_METHOD("set_tracking_confidence", "tracking_confidence"), &XRPose::set_tracking_confidence);
	ClassRegistry::bind_method(D_METHOD("get_tracking_confidence"), &XRPose::get_tracking_confidence);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "tracking_confidence"), "set_tracking_confidence", "get_tracking_confidence");
}

void XRPose::set_has_tracking_data(const bool p_has_tracking_data) {
	has_tracking_data = p_has_tracking_data;
}
bool XRPose::get_has_tracking_data() const {
	return has_tracking_data;
}

void XRPose::set_name(const StringName &p_name) {
	name = p_name;
}

StringName XRPose::get_name() const {
	return name;
}

void XRPose::set_transform(const Transform3D p_transform) {
	transform = p_transform;
}

Transform3D XRPose::get_transform() const {
	return transform;
}

Transform3D XRPose::get_adjusted_transform() const {
	Transform3D adjusted_transform = transform;

	XRSystem *xr_system = XRSystem::get_singleton();
	ERR_FAIL_NULL_V(xr_system, transform);

	// apply world scale
	adjusted_transform.origin *= xr_system->get_world_scale();

	// apply reference frame
	adjusted_transform = xr_system->get_reference_frame() * adjusted_transform;

	return adjusted_transform;
}

void XRPose::set_linear_velocity(const Vector3 p_velocity) {
	linear_velocity = p_velocity;
}

Vector3 XRPose::get_linear_velocity() const {
	return linear_velocity;
}

void XRPose::set_angular_velocity(const Vector3 p_velocity) {
	angular_velocity = p_velocity;
}

Vector3 XRPose::get_angular_velocity() const {
	return angular_velocity;
}

void XRPose::set_tracking_confidence(const XRPose::TrackingConfidence p_tracking_confidence) {
	tracking_confidence = p_tracking_confidence;
}

XRPose::TrackingConfidence XRPose::get_tracking_confidence() const {
	return tracking_confidence;
}
