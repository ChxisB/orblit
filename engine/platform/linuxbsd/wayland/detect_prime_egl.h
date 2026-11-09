/**************************************************************************/
/*  detect_prime_egl.h                                                    */

#pragma once

#ifdef GLES3_ENABLED
#ifdef EGL_ENABLED

#ifdef GLAD_ENABLED
#include <platform_egl.h>
#else
#include <EGL/egl.h>
#include <EGL/eglext.h>

#define GLAD_EGL_VERSION_1_5 1

#ifdef EGL_EXT_platform_base
#define GLAD_EGL_EXT_platform_base 1
#endif

#define KHRONOS_STATIC 1
extern "C" EGLAPI EGLDisplay EGLAPIENTRY eglGetPlatformDisplayEXT(EGLenum platform, void *native_display, const EGLint *attrib_list);
#undef KHRONOS_STATIC

#endif // GLAD_ENABLED

#ifndef EGL_EXT_platform_base
#define GLAD_EGL_EXT_platform_base 0
#endif

class DetectPrimeEGL {
private:
	struct Vendor {
		const char *glxvendor = nullptr;
		int priority = 0;
	};

	static constexpr Vendor vendor_map[] = {
		{ "Advanced Micro Devices, Inc.", 30 },
		{ "AMD", 30 },
		{ "NVIDIA Corporation", 30 },
		{ "X.Org", 30 },
		{ "Intel Open Source Technology Center", 20 },
		{ "Intel", 20 },
		{ "nouveau", 10 },
		{ "Mesa Project", 0 },
		{ nullptr, 0 }
	};

public:
	static void create_context(EGLenum p_platform_enum);

	static int detect_prime(EGLenum p_platform_enum);
};

#endif // GLES3_ENABLED
#endif // EGL_ENABLED
