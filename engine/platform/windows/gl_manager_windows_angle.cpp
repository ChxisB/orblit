/**************************************************************************/
/*  gl_manager_windows_angle.cpp                                          */

#include "gl_manager_windows_angle.h"

#if defined(WINDOWS_ENABLED) && defined(GLES3_ENABLED)

#include <EGL/eglext_angle.h>

const char *GLManagerANGLE_Windows::_get_platform_extension_name() const {
	return "EGL_ANGLE_platform_angle";
}

EGLenum GLManagerANGLE_Windows::_get_platform_extension_enum() const {
	return EGL_PLATFORM_ANGLE_ANGLE;
}

Vector<EGLAttrib> GLManagerANGLE_Windows::_get_platform_display_attributes() const {
	Vector<EGLAttrib> ret;
	ret.push_back(EGL_PLATFORM_ANGLE_TYPE_ANGLE);
	ret.push_back(EGL_PLATFORM_ANGLE_TYPE_D3D11_ANGLE);
	ret.push_back(EGL_NONE);

	return ret;
}

EGLenum GLManagerANGLE_Windows::_get_platform_api_enum() const {
	return EGL_OPENGL_ES_API;
}

Vector<EGLint> GLManagerANGLE_Windows::_get_platform_context_attribs() const {
	Vector<EGLint> ret;
	ret.push_back(EGL_CONTEXT_CLIENT_VERSION);
	ret.push_back(3);
	ret.push_back(EGL_NONE);

	return ret;
}

void GLManagerANGLE_Windows::window_resize(DisplaySystemEnums::WindowID p_window_id, int p_width, int p_height) {
	window_make_current(p_window_id);
	eglWaitNative(EGL_CORE_NATIVE_ENGINE);
}

#endif // WINDOWS_ENABLED && GLES3_ENABLED
