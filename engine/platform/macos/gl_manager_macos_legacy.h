/**************************************************************************/
/*  gl_manager_macos_legacy.h                                             */

#pragma once

#if defined(MACOS_ENABLED) && defined(GLES3_ENABLED)

#include "platform_gl.h"

#include "core/containers/rb_map.h"
#include "core/error/error_list.h"
#include "systems/display/display_system_enums.h"

#import <AppKit/AppKit.h>
#import <ApplicationServices/ApplicationServices.h>
#import <CoreVideo/CoreVideo.h>

ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wdeprecated-declarations") // OpenGL is deprecated in macOS 10.14.

typedef CGLError (*CGLEnablePtr)(CGLContextObj ctx, CGLContextEnable pname);
typedef CGLError (*CGLSetParameterPtr)(CGLContextObj ctx, CGLContextParameter pname, const GLint *params);
typedef CGLContextObj (*CGLGetCurrentContextPtr)();

class GLManagerLegacy_MacOS {
	struct GLWindow {
		id window_view = nullptr;
		NSOpenGLContext *context = nullptr;
	};

	RBMap<DisplaySystemEnums::WindowID, GLWindow> windows;

	NSOpenGLContext *shared_context = nullptr;
	DisplaySystemEnums::WindowID current_window = DisplaySystemEnums::INVALID_WINDOW_ID;

	Error create_context(GLWindow &win);

	bool framework_loaded = false;
	bool use_vsync = false;
	CGLEnablePtr CGLEnable = nullptr;
	CGLSetParameterPtr CGLSetParameter = nullptr;
	CGLGetCurrentContextPtr CGLGetCurrentContext = nullptr;

public:
	Error window_create(DisplaySystemEnums::WindowID p_window_id, id p_view, int p_width, int p_height);
	void window_destroy(DisplaySystemEnums::WindowID p_window_id);
	void window_resize(DisplaySystemEnums::WindowID p_window_id, int p_width, int p_height);

	void release_current();
	void swap_buffers();

	void window_make_current(DisplaySystemEnums::WindowID p_window_id);

	void window_set_per_pixel_transparency_enabled(DisplaySystemEnums::WindowID p_window_id, bool p_enabled);

	Error initialize();

	void set_use_vsync(bool p_use);
	bool is_using_vsync() const;

	NSOpenGLContext *get_context(DisplaySystemEnums::WindowID p_window_id);

	GLManagerLegacy_MacOS();
	~GLManagerLegacy_MacOS();
};

ORBLIT_CLANG_WARNING_PUSH

#endif // MACOS_ENABLED && GLES3_ENABLED
