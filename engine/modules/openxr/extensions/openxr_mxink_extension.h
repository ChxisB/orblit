/**************************************************************************/
/*  openxr_mxink_extension.h                                              */

#pragma once

#include "openxr_extension_wrapper.h"

class OpenXRMxInkExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRMxInkExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	bool is_available();

	virtual void on_register_metadata(OpenXRInteractionProfileMetadata *p_interaction_profile_metadata) override;

private:
	bool available = false;
};
