/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#if defined(LINUXBSD_ENABLED)
#include "camera_linux.h"
#endif
#if defined(WINDOWS_ENABLED)
#include "camera_win.h"
#endif
#if defined(MACOS_ENABLED)
#include "camera_apple.h"
#endif
#if defined(ANDROID_ENABLED)
#include "camera_android.h"
#endif

void initialize_camera_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

#if defined(LINUXBSD_ENABLED)
	CameraSystem::make_default<CameraLinux>();
#endif
#if defined(WINDOWS_ENABLED)
	CameraSystem::make_default<CameraWindows>();
#endif
#if defined(MACOS_ENABLED)
	CameraSystem::make_default<CameraApple>();
#endif
#if defined(ANDROID_ENABLED)
	CameraSystem::make_default<CameraAndroid>();
#endif
}

void uninitialize_camera_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
}
