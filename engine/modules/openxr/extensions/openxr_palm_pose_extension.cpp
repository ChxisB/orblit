/**************************************************************************/
/*  openxr_palm_pose_extension.cpp                                        */

#include "openxr_palm_pose_extension.h"

#include "../openxr_api.h"

OpenXRPalmPoseExtension *OpenXRPalmPoseExtension::singleton = nullptr;

OpenXRPalmPoseExtension *OpenXRPalmPoseExtension::get_singleton() {
	return singleton;
}

OpenXRPalmPoseExtension::OpenXRPalmPoseExtension() {
	singleton = this;
}

OpenXRPalmPoseExtension::~OpenXRPalmPoseExtension() {
	singleton = nullptr;
}

HashMap<String, bool *> OpenXRPalmPoseExtension::get_requested_extensions(XrVersion p_version) {
	HashMap<String, bool *> request_extensions;

	if (p_version < XR_API_VERSION_1_1_0) {
		// Extension was promoted in OpenXR 1.1, only include it in OpenXR 1.0.
		request_extensions[XR_EXT_PALM_POSE_EXTENSION_NAME] = &available;
	}

	return request_extensions;
}

bool OpenXRPalmPoseExtension::is_available() {
	return available;
}
