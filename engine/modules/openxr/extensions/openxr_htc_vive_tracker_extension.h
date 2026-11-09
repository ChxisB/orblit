/**************************************************************************/
/*  openxr_htc_vive_tracker_extension.h                                   */

#pragma once

#include "openxr_extension_wrapper.h"

class OpenXRHTCViveTrackerExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRHTCViveTrackerExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	PackedStringArray get_suggested_tracker_names() override;

	bool is_available();

	virtual void on_register_metadata(OpenXRInteractionProfileMetadata *p_interaction_profile_metadata) override;
	virtual bool on_event_polled(const XrEventDataBuffer &event) override;

private:
	bool available = false;
};
