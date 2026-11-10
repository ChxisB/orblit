/**************************************************************************/
/*  openxr_android_thread_settings_extension.h                            */

#pragma once

#include "openxr_extension_wrapper.h"

#include "core/containers/hash_map.h"

#ifdef XR_USE_PLATFORM_ANDROID
#include "../util.h"

#include <jni.h>
#include <openxr/openxr_platform.h>
#endif

class OpenXRAndroidThreadSettingsExtension : public OpenXRExtensionWrapper {
	ORBLIT_CLASS(OpenXRAndroidThreadSettingsExtension, OpenXRExtensionWrapper);

public:
	static OpenXRAndroidThreadSettingsExtension *get_singleton();

	OpenXRAndroidThreadSettingsExtension();
	virtual ~OpenXRAndroidThreadSettingsExtension() override;

	virtual HashMap<String, bool *> get_requested_extensions(XrVersion p_version) override;
	virtual void on_instance_created(XrInstance p_instance) override;
	virtual void on_session_created(XrSession p_session) override;

	enum ThreadType {
		THREAD_TYPE_APPLICATION_MAIN,
		THREAD_TYPE_APPLICATION_WORKER,
		THREAD_TYPE_RENDERER_MAIN,
		THREAD_TYPE_RENDERER_WORKER,
	};
	bool set_application_thread_type(ThreadType p_thread_type, uint32_t p_thread_id = 0);

protected:
	static void register_members();

private:
	static OpenXRAndroidThreadSettingsExtension *singleton;

	bool _initialize_openxr_android_thread_settings_extension();
	void _set_render_thread_type();

	bool available = false;

#ifdef XR_USE_PLATFORM_ANDROID
	EXT_PROTO_XRRESULT_FUNC3(xrSetAndroidApplicationThreadKHR, (XrSession), session, (XrAndroidThreadTypeKHR), threadType, (uint32_t), threadId);
#endif
};

VARIANT_ENUM_CAST(OpenXRAndroidThreadSettingsExtension::ThreadType)
