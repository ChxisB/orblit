/**************************************************************************/
/*  openxr_local_floor_extension.cpp                                      */

#include "openxr_local_floor_extension.h"

#include "../openxr_api.h"

OpenXRLocalFloorExtension *OpenXRLocalFloorExtension::singleton = nullptr;

OpenXRLocalFloorExtension *OpenXRLocalFloorExtension::get_singleton() {
	return singleton;
}

OpenXRLocalFloorExtension::OpenXRLocalFloorExtension() {
	singleton = this;
}

OpenXRLocalFloorExtension::~OpenXRLocalFloorExtension() {
	singleton = nullptr;
}

HashMap<String, bool *> OpenXRLocalFloorExtension::get_requested_extensions(XrVersion p_version) {
	HashMap<String, bool *> request_extensions;

	if (p_version < XR_API_VERSION_1_1_0) {
		// Extension was promoted in OpenXR 1.1, only include it in OpenXR 1.0.
		request_extensions[XR_EXT_LOCAL_FLOOR_EXTENSION_NAME] = &available;
	}

	return request_extensions;
}

bool OpenXRLocalFloorExtension::is_available() {
	return available;
}
