/**************************************************************************/
/*  openxr_haptic_feedback.h                                              */

#pragma once

#include "core/io/resource.h"

#include <openxr/openxr.h>

class OpenXRHapticBase : public Resource {
	ORBLIT_CLASS(OpenXRHapticBase, Resource);

private:
protected:
	static void register_members();

public:
	virtual const XrHapticBaseHeader *get_xr_structure() = 0;
};

class OpenXRHapticVibration : public OpenXRHapticBase {
	ORBLIT_CLASS(OpenXRHapticVibration, OpenXRHapticBase);

private:
	XrHapticVibration haptic_vibration;

protected:
	static void register_members();

public:
	void set_duration(int64_t p_duration);
	int64_t get_duration() const;

	void set_frequency(float p_frequency);
	float get_frequency() const;

	void set_amplitude(float p_amplitude);
	float get_amplitude() const;

	virtual const XrHapticBaseHeader *get_xr_structure() override;

	OpenXRHapticVibration();
};
