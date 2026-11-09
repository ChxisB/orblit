/**************************************************************************/
/*  openxr_wmr_controller_extension.h                                     */

#pragma once

#include "openxr_extension_wrapper.h"

class OpenXRWMRControllerExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRWMRControllerExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	enum WMRControllers {
		WMR_HPMR,
		WMR_SAMSUNG_ODESSY,
		WMR_HAND_INTERACTION,
		WMR_MAX_CONTROLLERS
	};

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	bool is_available(WMRControllers p_type);

	virtual void on_register_metadata(OpenXRInteractionProfileMetadata *p_interaction_profile_metadata) override;

private:
	bool available[WMR_MAX_CONTROLLERS] = { false, false };
};
