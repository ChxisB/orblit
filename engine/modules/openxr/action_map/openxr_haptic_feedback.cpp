/**************************************************************************/
/*  openxr_haptic_feedback.cpp                                            */

#include "openxr_haptic_feedback.h"

#include "core/object/class_registry.h"

////////////////////////////////////////////////////////////////////////////
// OpenXRHapticBase

void OpenXRHapticBase::register_members() {
}

////////////////////////////////////////////////////////////////////////////
// OpenXRHapticVibration

void OpenXRHapticVibration::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_duration", "duration"), &OpenXRHapticVibration::set_duration);
	ClassRegistry::bind_method(D_METHOD("get_duration"), &OpenXRHapticVibration::get_duration);
	ADD_PROPERTY(PropertyInfo(Variant::INT, "duration"), "set_duration", "get_duration");

	ClassRegistry::bind_method(D_METHOD("set_frequency", "frequency"), &OpenXRHapticVibration::set_frequency);
	ClassRegistry::bind_method(D_METHOD("get_frequency"), &OpenXRHapticVibration::get_frequency);
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "frequency"), "set_frequency", "get_frequency");

	ClassRegistry::bind_method(D_METHOD("set_amplitude", "amplitude"), &OpenXRHapticVibration::set_amplitude);
	ClassRegistry::bind_method(D_METHOD("get_amplitude"), &OpenXRHapticVibration::get_amplitude);
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "amplitude", PROPERTY_HINT_RANGE, "0.0,1.0,0.01"), "set_amplitude", "get_amplitude");
}

void OpenXRHapticVibration::set_duration(int64_t p_duration) {
	haptic_vibration.duration = p_duration;
	emit_changed();
}

int64_t OpenXRHapticVibration::get_duration() const {
	return haptic_vibration.duration;
}

void OpenXRHapticVibration::set_frequency(float p_frequency) {
	haptic_vibration.frequency = p_frequency;
	emit_changed();
}

float OpenXRHapticVibration::get_frequency() const {
	return haptic_vibration.frequency;
}

void OpenXRHapticVibration::set_amplitude(float p_amplitude) {
	haptic_vibration.amplitude = p_amplitude;
	emit_changed();
}

float OpenXRHapticVibration::get_amplitude() const {
	return haptic_vibration.amplitude;
}

const XrHapticBaseHeader *OpenXRHapticVibration::get_xr_structure() {
	return (XrHapticBaseHeader *)&haptic_vibration;
}

OpenXRHapticVibration::OpenXRHapticVibration() {
	haptic_vibration.type = XR_TYPE_HAPTIC_VIBRATION;
	haptic_vibration.next = nullptr;
	haptic_vibration.duration = -1;
	haptic_vibration.frequency = 0.0;
	haptic_vibration.amplitude = 1.0;
}
