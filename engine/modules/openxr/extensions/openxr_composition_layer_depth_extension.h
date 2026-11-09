/**************************************************************************/
/*  openxr_composition_layer_depth_extension.h                            */

#pragma once

#include "openxr_extension_wrapper.h"

class OpenXRCompositionLayerDepthExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRCompositionLayerDepthExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	static OpenXRCompositionLayerDepthExtension *get_singleton();

	OpenXRCompositionLayerDepthExtension();
	virtual ~OpenXRCompositionLayerDepthExtension() override;

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;
	bool is_available();
	virtual int get_composition_layer_count() override;
	virtual XrCompositionLayerBaseHeader *get_composition_layer(int p_index) override;
	virtual int get_composition_layer_order(int p_index) override;

private:
	static OpenXRCompositionLayerDepthExtension *singleton;

	bool available = false;
};
