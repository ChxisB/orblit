/**************************************************************************/
/*  openxr_meta_controller_extension.h                                    */

#pragma once

#include "openxr_extension_wrapper.h"

class OpenXRMetaControllerExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRMetaControllerExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	enum MetaControllers {
		META_TOUCH_PROXIMITY, // Proximity extensions for normal touch controllers
		META_TOUCH_PRO, // Touch controller for the Quest Pro
		META_TOUCH_PLUS, // Touch controller for the Quest Plus
		META_MAX_CONTROLLERS
	};

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	bool is_available(MetaControllers p_type);

	virtual void on_register_metadata(OpenXRInteractionProfileMetadata *p_interaction_profile_metadata) override;

private:
	bool available[META_MAX_CONTROLLERS] = { false, false, false };
};
