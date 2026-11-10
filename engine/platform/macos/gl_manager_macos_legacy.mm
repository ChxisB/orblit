/**************************************************************************/
/*  gl_manager_macos_legacy.mm                                            */

#import "gl_manager_macos_legacy.h"

#if defined(MACOS_ENABLED) && defined(GLES3_ENABLED)

#include "core/os/os.h"

#include <dlfcn.h>

#include <cstdio>
#include <cstdlib>

ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wdeprecated-declarations") // OpenGL is deprecated in macOS 10.14.

Error GLManagerLegacy_MacOS::create_context(GLWindow &win) {
	NSOpenGLPixelFormatAttribute attributes[] = {
		NSOpenGLPFADoubleBuffer,
		NSOpenGLPFAClosestPolicy,
		NSOpenGLPFAOpenGLProfile, NSOpenGLProfileVersion3_2Core,
		NSOpenGLPFAColorSize, 32,
		NSOpenGLPFADepthSize, 24,
		NSOpenGLPFAStencilSize, 8,
		0
	};

	NSOpenGLPixelFormat *pixel_format = [[NSOpenGLPixelFormat alloc] initWithAttributes:attributes];
	ERR_FAIL_NULL_V(pixel_format, ERR_CANT_CREATE);

	win.context = [[NSOpenGLContext alloc] initWithFormat:pixel_format shareContext:shared_context];
	ERR_FAIL_NULL_V(win.context, ERR_CANT_CREATE);
	if (shared_context == nullptr) {
		shared_context = win.context;
	}

	[win.context setView:win.window_view];
	[win.context makeCurrentContext];

	return OK;
}

Error GLManagerLegacy_MacOS::window_create(DisplaySystemEnums::WindowID p_window_id, id p_view, int p_width, int p_height) {
	GLWindow win;
	win.window_view = p_view;

	if (create_context(win) != OK) {
		return FAILED;
	}

	windows[p_window_id] = win;
	window_make_current(p_window_id);

	return OK;
}

void GLManagerLegacy_MacOS::window_resize(DisplaySystemEnums::WindowID p_window_id, int p_width, int p_height) {
	if (!windows.has(p_window_id)) {
		return;
	}

	GLWindow &win = windows[p_window_id];

	GLint dim[2];
	dim[0] = p_width;
	dim[1] = p_height;
	CGLSetParameter((CGLContextObj)[win.context CGLContextObj], kCGLCPSurfaceBackingSize, &dim[0]);
	CGLEnable((CGLContextObj)[win.context CGLContextObj], kCGLCESurfaceBackingSize);
	if (OS::get_singleton()->is_hidpi_allowed()) {
		[win.window_view setWantsBestResolutionOpenGLSurface:YES];
	} else {
		[win.window_view setWantsBestResolutionOpenGLSurface:NO];
	}

	[win.context update];
}

void GLManagerLegacy_MacOS::window_destroy(DisplaySystemEnums::WindowID p_window_id) {
	if (!windows.has(p_window_id)) {
		return;
	}

	if (current_window == p_window_id) {
		current_window = DisplaySystemEnums::INVALID_WINDOW_ID;
	}

	windows.erase(p_window_id);
}

void GLManagerLegacy_MacOS::release_current() {
	if (current_window == DisplaySystemEnums::INVALID_WINDOW_ID) {
		return;
	}

	[NSOpenGLContext clearCurrentContext];
	current_window = DisplaySystemEnums::INVALID_WINDOW_ID;
}

void GLManagerLegacy_MacOS::window_make_current(DisplaySystemEnums::WindowID p_window_id) {
	if (current_window == p_window_id) {
		return;
	}
	if (!windows.has(p_window_id)) {
		return;
	}

	GLWindow &win = windows[p_window_id];
	[win.context makeCurrentContext];

	current_window = p_window_id;
}

void GLManagerLegacy_MacOS::swap_buffers() {
	GLWindow &win = windows[current_window];
	[win.context flushBuffer];
}

void GLManagerLegacy_MacOS::window_set_per_pixel_transparency_enabled(DisplaySystemEnums::WindowID p_window_id, bool p_enabled) {
	if (!windows.has(p_window_id)) {
		return;
	}

	GLWindow &win = windows[p_window_id];
	if (p_enabled) {
		GLint opacity = 0;
		[win.context setValues:&opacity forParameter:NSOpenGLContextParameterSurfaceOpacity];
	} else {
		GLint opacity = 1;
		[win.context setValues:&opacity forParameter:NSOpenGLContextParameterSurfaceOpacity];
	}
	[win.context update];
}

Error GLManagerLegacy_MacOS::initialize() {
	return framework_loaded ? OK : ERR_CANT_CREATE;
}

void GLManagerLegacy_MacOS::set_use_vsync(bool p_use) {
	use_vsync = p_use;

	CGLContextObj ctx = CGLGetCurrentContext();
	if (ctx) {
		GLint swapInterval = p_use ? 1 : 0;
		if (CGLSetParameter(ctx, kCGLCPSwapInterval, &swapInterval) != kCGLNoError) {
			WARN_PRINT("Could not set V-Sync mode.");
		}
		use_vsync = p_use;
	}
}

bool GLManagerLegacy_MacOS::is_using_vsync() const {
	return use_vsync;
}

NSOpenGLContext *GLManagerLegacy_MacOS::get_context(DisplaySystemEnums::WindowID p_window_id) {
	if (!windows.has(p_window_id)) {
		return nullptr;
	}

	GLWindow &win = windows[p_window_id];
	return win.context;
}

GLManagerLegacy_MacOS::GLManagerLegacy_MacOS() {
	NSBundle *framework = [NSBundle bundleWithPath:@"/System/Library/Frameworks/OpenGL.framework"];
	if (framework) {
		void *library_handle = dlopen([framework.executablePath UTF8String], RTLD_NOW);
		if (library_handle) {
			CGLEnable = (CGLEnablePtr)dlsym(library_handle, "CGLEnable");
			CGLSetParameter = (CGLSetParameterPtr)dlsym(library_handle, "CGLSetParameter");
			CGLGetCurrentContext = (CGLGetCurrentContextPtr)dlsym(library_handle, "CGLGetCurrentContext");

			framework_loaded = CGLEnable && CGLSetParameter && CGLGetCurrentContext;
		}
	}
}

GLManagerLegacy_MacOS::~GLManagerLegacy_MacOS() {
	release_current();
}

ORBLIT_CLANG_WARNING_POP

#endif // MACOS_ENABLED && GLES3_ENABLED
