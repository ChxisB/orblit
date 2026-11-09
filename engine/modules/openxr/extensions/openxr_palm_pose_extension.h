/**************************************************************************/
/*  openxr_palm_pose_extension.h                                          */

#pragma once

#include "openxr_extension_wrapper.h"

class OpenXRPalmPoseExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRPalmPoseExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	static OpenXRPalmPoseExtension *get_singleton();

	OpenXRPalmPoseExtension();
	virtual ~OpenXRPalmPoseExtension() override;

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	bool is_available();

private:
	static OpenXRPalmPoseExtension *singleton;

	bool available = false;
};
