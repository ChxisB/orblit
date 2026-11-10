/**************************************************************************/
/*  xr_tracker.cpp                                                        */

#include "xr_tracker.h"

#include "core/object/class_registry.h"

void XRTracker::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_tracker_type"), &XRTracker::get_tracker_type);
	ClassRegistry::bind_method(D_METHOD("set_tracker_type", "type"), &XRTracker::set_tracker_type);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "type"), "set_tracker_type", "get_tracker_type");

	ClassRegistry::bind_method(D_METHOD("get_tracker_name"), &XRTracker::get_tracker_name);
	ClassRegistry::bind_method(D_METHOD("set_tracker_name", "name"), &XRTracker::set_tracker_name);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "name"), "set_tracker_name", "get_tracker_name");

	ClassRegistry::bind_method(D_METHOD("get_tracker_desc"), &XRTracker::get_tracker_desc);
	ClassRegistry::bind_method(D_METHOD("set_tracker_desc", "description"), &XRTracker::set_tracker_desc);
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "description"), "set_tracker_desc", "get_tracker_desc");
}

void XRTracker::set_tracker_type(XRSystem::TrackerType p_type) {
	type = p_type;
}

XRSystem::TrackerType XRTracker::get_tracker_type() const {
	return type;
}

void XRTracker::set_tracker_name(const StringName &p_name) {
	// Note: this should not be changed after the tracker is registered with the XRSystem!
	name = p_name;
}

StringName XRTracker::get_tracker_name() const {
	return name;
}

void XRTracker::set_tracker_desc(const String &p_desc) {
	description = p_desc;
}

String XRTracker::get_tracker_desc() const {
	return description;
}
