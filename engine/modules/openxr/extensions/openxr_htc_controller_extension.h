/**************************************************************************/
/*  openxr_htc_controller_extension.h                                     */

#pragma once

#include "openxr_extension_wrapper.h"

class OpenXRHTCControllerExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRHTCControllerExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	enum HTCControllers {
		// Note, HTC Vive Wand controllers are part of the core spec and not part of our extension.
		HTC_VIVE_COSMOS,
		HTC_VIVE_FOCUS3,
		HTC_HAND_INTERACTION,
		HTC_MAX_CONTROLLERS
	};

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	PackedStringArray get_suggested_tracker_names() override;

	bool is_available(HTCControllers p_type);

	virtual void on_register_metadata(OpenXRInteractionProfileMetadata *p_interaction_profile_metadata) override;

private:
	bool available[HTC_MAX_CONTROLLERS] = { false, false };
};
