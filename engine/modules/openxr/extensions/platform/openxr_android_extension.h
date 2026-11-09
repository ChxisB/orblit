/**************************************************************************/
/*  openxr_android_extension.h                                            */

#pragma once

#include "../../util.h"
#include "../openxr_extension_wrapper.h"

#include <jni.h>

class OpenXRAndroidExtension : public OpenXRExtensionWrapper {
public:
	static OpenXRAndroidExtension *get_singleton();

	OpenXRAndroidExtension();

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;
	virtual void on_before_instance_created() override;
	virtual void *set_instance_create_info_and_get_next_pointer(XrVersion p_xr_version, void *p_next_pointer) override;

	virtual ~OpenXRAndroidExtension() override;

private:
	static OpenXRAndroidExtension *singleton;

	JavaVM *vm;
	jobject activity_object;
	bool loader_init_extension_available = false;
	bool loader_init_android_extension_available = false;
	bool create_instance_extension_available = false;

	// Initialize the loader
	EXT_PROTO_XRRESULT_FUNC1(xrInitializeLoaderKHR, (const XrLoaderInitInfoBaseHeaderKHR *), loaderInitInfo)
};
