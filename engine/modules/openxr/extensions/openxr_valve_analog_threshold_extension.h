/**************************************************************************/
/*  openxr_valve_analog_threshold_extension.h                             */

#pragma once

#include "../action_map/openxr_binding_modifier.h"
#include "../action_map/openxr_haptic_feedback.h"
#include "openxr_extension_wrapper.h"

class OpenXRValveAnalogThresholdExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRValveAnalogThresholdExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	static OpenXRValveAnalogThresholdExtension *get_singleton();

	OpenXRValveAnalogThresholdExtension();
	virtual ~OpenXRValveAnalogThresholdExtension() override;

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	bool is_available();

private:
	static OpenXRValveAnalogThresholdExtension *singleton;

	bool binding_modifier_ext = false;
	bool threshold_ext = false;
};

class OpenXRAnalogThresholdModifier : public OpenXRActionBindingModifier {
	ORBLIT_CLASS(OpenXRAnalogThresholdModifier, OpenXRActionBindingModifier);

private:
	XrInteractionProfileAnalogThresholdVALVE analog_threshold;
	Ref<OpenXRHapticBase> on_haptic;
	Ref<OpenXRHapticBase> off_haptic;

protected:
	static void register_members();

public:
	OpenXRAnalogThresholdModifier();

	void set_on_threshold(float p_threshold);
	float get_on_threshold() const;

	void set_off_threshold(float p_threshold);
	float get_off_threshold() const;

	void set_on_haptic(const Ref<OpenXRHapticBase> &p_haptic);
	Ref<OpenXRHapticBase> get_on_haptic() const;

	void set_off_haptic(const Ref<OpenXRHapticBase> &p_haptic);
	Ref<OpenXRHapticBase> get_off_haptic() const;

	virtual String get_description() const override { return "Analog threshold modifier"; }
	virtual PackedByteArray get_ip_modification() override;
};
