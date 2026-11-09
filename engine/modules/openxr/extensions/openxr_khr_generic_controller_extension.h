/**************************************************************************/
/*  openxr_khr_generic_controller_extension.h                             */

#pragma once

#include "openxr_extension_wrapper.h"

class OpenXRKHRGenericController : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRKHRGenericController, OpenXRExtensionWrapper);

public:
	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	bool is_available() const;

	virtual void on_register_metadata(OpenXRInteractionProfileMetadata *p_interaction_profile_metadata) override;

private:
	bool generic_controller_ext = false;
};
