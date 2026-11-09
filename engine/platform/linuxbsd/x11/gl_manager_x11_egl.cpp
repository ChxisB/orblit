/**************************************************************************/
/*  gl_manager_x11_egl.cpp                                                */

#include "gl_manager_x11_egl.h"

#if defined(X11_ENABLED) && defined(GLES3_ENABLED)

const char *GLManagerEGL_X11::_get_platform_extension_name() const {
	return "EGL_KHR_platform_x11";
}

EGLenum GLManagerEGL_X11::_get_platform_extension_enum() const {
	return EGL_PLATFORM_X11_KHR;
}

Vector<EGLAttrib> GLManagerEGL_X11::_get_platform_display_attributes() const {
	return Vector<EGLAttrib>();
}

EGLenum GLManagerEGL_X11::_get_platform_api_enum() const {
	return EGL_OPENGL_ES_API;
}

Vector<EGLint> GLManagerEGL_X11::_get_platform_context_attribs() const {
	Vector<EGLint> ret;
	ret.push_back(EGL_CONTEXT_CLIENT_VERSION);
	ret.push_back(3);
	ret.push_back(EGL_NONE);

	return ret;
}

#endif // X11_ENABLED && GLES3_ENABLED
