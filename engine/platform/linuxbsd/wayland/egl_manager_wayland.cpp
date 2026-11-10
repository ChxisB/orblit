/**************************************************************************/
/*  egl_manager_wayland.cpp                                               */

#include "egl_manager_wayland.h"

#ifdef WAYLAND_ENABLED
#ifdef EGL_ENABLED
#ifdef GLES3_ENABLED

const char *EGLManagerWayland::_get_platform_extension_name() const {
	return "EGL_KHR_platform_wayland";
}

EGLenum EGLManagerWayland::_get_platform_extension_enum() const {
	return EGL_PLATFORM_WAYLAND_KHR;
}

EGLenum EGLManagerWayland::_get_platform_api_enum() const {
	return EGL_OPENGL_API;
}

Vector<EGLAttrib> EGLManagerWayland::_get_platform_display_attributes() const {
	return Vector<EGLAttrib>();
}

Vector<EGLint> EGLManagerWayland::_get_platform_context_attribs() const {
	Vector<EGLint> ret;
	ret.push_back(EGL_CONTEXT_MAJOR_VERSION);
	ret.push_back(3);
	ret.push_back(EGL_CONTEXT_MINOR_VERSION);
	ret.push_back(3);
	ret.push_back(EGL_NONE);

	return ret;
}

#endif // GLES3_ENABLED
#endif // EGL_ENABLED
#endif // WAYLAND_ENABLED
