/**************************************************************************/
/*  openxr_local_floor_extension.h                                        */

#pragma once

#include "openxr_extension_wrapper.h"

class OpenXRLocalFloorExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRLocalFloorExtension, OpenXRExtensionWrapper);

protected:
	static void register_members() {}

public:
	static OpenXRLocalFloorExtension *get_singleton();

	OpenXRLocalFloorExtension();
	virtual ~OpenXRLocalFloorExtension() override;

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;

	bool is_available();

private:
	static OpenXRLocalFloorExtension *singleton;

	bool available = false;
};
