/**************************************************************************/
/*  gl_manager_macos_angle.mm                                             */

#import "gl_manager_macos_angle.h"

#if defined(MACOS_ENABLED) && defined(GLES3_ENABLED)

#import <EGL/eglext_angle.h>

#include <cstdio>
#include <cstdlib>

const char *GLManagerANGLE_MacOS::_get_platform_extension_name() const {
	return "EGL_ANGLE_platform_angle";
}

EGLenum GLManagerANGLE_MacOS::_get_platform_extension_enum() const {
	return EGL_PLATFORM_ANGLE_ANGLE;
}

Vector<EGLAttrib> GLManagerANGLE_MacOS::_get_platform_display_attributes() const {
	Vector<EGLAttrib> ret;
	ret.push_back(EGL_PLATFORM_ANGLE_TYPE_ANGLE);
	ret.push_back(EGL_PLATFORM_ANGLE_TYPE_METAL_ANGLE);
	ret.push_back(EGL_NONE);

	return ret;
}

EGLenum GLManagerANGLE_MacOS::_get_platform_api_enum() const {
	return EGL_OPENGL_ES_API;
}

Vector<EGLint> GLManagerANGLE_MacOS::_get_platform_context_attribs() const {
	Vector<EGLint> ret;
	ret.push_back(EGL_CONTEXT_CLIENT_VERSION);
	ret.push_back(3);
	ret.push_back(EGL_NONE);

	return ret;
}

#endif // MACOS_ENABLED && GLES3_ENABLED
