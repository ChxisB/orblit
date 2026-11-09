/**************************************************************************/
/*  openxr_composition_layer_depth_extension.cpp                          */

#include "openxr_composition_layer_depth_extension.h"

OpenXRCompositionLayerDepthExtension *OpenXRCompositionLayerDepthExtension::singleton = nullptr;

OpenXRCompositionLayerDepthExtension *OpenXRCompositionLayerDepthExtension::get_singleton() {
	return singleton;
}

OpenXRCompositionLayerDepthExtension::OpenXRCompositionLayerDepthExtension() {
	singleton = this;
}

OpenXRCompositionLayerDepthExtension::~OpenXRCompositionLayerDepthExtension() {
	singleton = nullptr;
}

HashMap<String, bool *> OpenXRCompositionLayerDepthExtension::get_requested_extensions(XrVersion p_version) {
	HashMap<String, bool *> request_extensions;

	request_extensions[XR_KHR_COMPOSITION_LAYER_DEPTH_EXTENSION_NAME] = &available;

	return request_extensions;
}

bool OpenXRCompositionLayerDepthExtension::is_available() {
	return available;
}

int OpenXRCompositionLayerDepthExtension::get_composition_layer_count() {
	return 0;
}

XrCompositionLayerBaseHeader *OpenXRCompositionLayerDepthExtension::get_composition_layer(int p_index) {
	// Seems this is all done in our base layer... Just in case this changes...
	return nullptr;
}

int OpenXRCompositionLayerDepthExtension::get_composition_layer_order(int p_index) {
	return 0;
}
