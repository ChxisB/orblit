/**************************************************************************/
/*  openxr_android_thread_settings_extension.cpp                          */

#include "openxr_android_thread_settings_extension.h"

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "core/string/print_string.h"
#include "systems/rendering/rendering_system.h"

#ifdef XR_USE_PLATFORM_ANDROID
#include "../openxr_api.h"

#include <unistd.h>
#endif

OpenXRAndroidThreadSettingsExtension *OpenXRAndroidThreadSettingsExtension::singleton = nullptr;

OpenXRAndroidThreadSettingsExtension *OpenXRAndroidThreadSettingsExtension::get_singleton() {
	return singleton;
}

OpenXRAndroidThreadSettingsExtension::OpenXRAndroidThreadSettingsExtension() {
	singleton = this;
}

OpenXRAndroidThreadSettingsExtension::~OpenXRAndroidThreadSettingsExtension() {
	singleton = nullptr;
}

void OpenXRAndroidThreadSettingsExtension::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_application_thread_type", "thread_type", "thread_id"), &OpenXRAndroidThreadSettingsExtension::set_application_thread_type, DEFVAL(0));

	BIND_ENUM_CONSTANT(THREAD_TYPE_APPLICATION_MAIN);
	BIND_ENUM_CONSTANT(THREAD_TYPE_APPLICATION_WORKER);
	BIND_ENUM_CONSTANT(THREAD_TYPE_RENDERER_MAIN);
	BIND_ENUM_CONSTANT(THREAD_TYPE_RENDERER_WORKER);
}

HashMap<String, bool *> OpenXRAndroidThreadSettingsExtension::get_requested_extensions(XrVersion p_version) {
	HashMap<String, bool *> request_extensions;

#ifdef XR_USE_PLATFORM_ANDROID
	request_extensions[XR_KHR_ANDROID_THREAD_SETTINGS_EXTENSION_NAME] = &available;
#endif

	return request_extensions;
}

void OpenXRAndroidThreadSettingsExtension::on_instance_created(XrInstance p_instance) {
	if (!available) {
		return;
	}

	if (!_initialize_openxr_android_thread_settings_extension()) {
		print_error("OpenXR: Failed to initialize android thread settings extension");
		available = false;
	}
}

void OpenXRAndroidThreadSettingsExtension::on_session_created(XrSession p_session) {
	if (!available) {
		return;
	}

	// Attempt to mark this thread as the "main thread".
	set_application_thread_type(THREAD_TYPE_APPLICATION_MAIN);

	// Attempt to mark the render thread too.
	RenderingSystem *rendering_system = RenderingSystem::get_singleton();
	ERR_FAIL_NULL(rendering_system);
	rendering_system->call_on_render_thread(callable_mp(this, &OpenXRAndroidThreadSettingsExtension::_set_render_thread_type));
}

bool OpenXRAndroidThreadSettingsExtension::set_application_thread_type(ThreadType p_thread_type, uint32_t p_thread_id) {
	if (!available) {
		return false;
	}

#ifdef XR_USE_PLATFORM_ANDROID
	XrAndroidThreadTypeKHR thread_type{};
	switch (p_thread_type) {
		case THREAD_TYPE_APPLICATION_MAIN:
			thread_type = XR_ANDROID_THREAD_TYPE_APPLICATION_MAIN_KHR;
			break;
		case THREAD_TYPE_APPLICATION_WORKER:
			thread_type = XR_ANDROID_THREAD_TYPE_APPLICATION_WORKER_KHR;
			break;
		case THREAD_TYPE_RENDERER_MAIN:
			thread_type = XR_ANDROID_THREAD_TYPE_RENDERER_MAIN_KHR;
			break;
		case THREAD_TYPE_RENDERER_WORKER:
			thread_type = XR_ANDROID_THREAD_TYPE_RENDERER_WORKER_KHR;
			break;
		default:
			print_error(vformat("OpenXR: Failed to set android application thread; invalid thread type %d", p_thread_type));
			return false;
	}

	XrResult result = xrSetAndroidApplicationThreadKHR(OpenXRAPI::get_singleton()->get_session(), thread_type, p_thread_id == 0 ? gettid() : p_thread_id);
	if (result != XR_SUCCESS) {
		print_error(vformat("OpenXR: Failed to set android application thread; %s", OpenXRAPI::get_singleton()->get_error_string(result)));
		return false;
	}
#endif

	return true;
}

bool OpenXRAndroidThreadSettingsExtension::_initialize_openxr_android_thread_settings_extension() {
#ifdef XR_USE_PLATFORM_ANDROID
	EXT_INIT_XR_FUNC_V(xrSetAndroidApplicationThreadKHR);
#endif
	return true;
}

void OpenXRAndroidThreadSettingsExtension::_set_render_thread_type() {
	// Skip when the render thread == the main thread
	if (Thread::is_main_thread()) {
		return;
	}

	set_application_thread_type(THREAD_TYPE_RENDERER_MAIN);
}
