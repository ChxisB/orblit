/**************************************************************************/
/*  gl_manager_windows_native.h                                           */

#pragma once

#if defined(WINDOWS_ENABLED) && defined(GLES3_ENABLED)

#include "core/containers/local_vector.h"
#include "core/containers/rb_map.h"
#include "systems/display/display_system_enums.h"

#include <windows.h>

typedef bool(APIENTRY *PFNWGLSWAPINTERVALEXTPROC)(int interval);
typedef int(APIENTRY *PFNWGLGETSWAPINTERVALEXTPROC)(void);

class GLManagerNative_Windows {
private:
	// any data specific to the window
	struct GLWindow {
		bool use_vsync = false;

		// windows specific
		HDC hDC;
		HWND hwnd;

		int gldisplay_id = 0;
	};

	struct GLDisplay {
		// windows specific
		HGLRC hRC;
	};

	RBMap<DisplaySystemEnums::WindowID, GLWindow> _windows;
	LocalVector<GLDisplay> _displays;

	GLWindow *_current_window = nullptr;

	PFNWGLSWAPINTERVALEXTPROC wglSwapIntervalEXT = nullptr;

	GLWindow &get_window(unsigned int id) { return _windows[id]; }
	const GLWindow &get_window(unsigned int id) const { return _windows[id]; }

	const GLDisplay &get_current_display() const { return _displays[_current_window->gldisplay_id]; }
	const GLDisplay &get_display(unsigned int id) { return _displays[id]; }

	bool direct_render;
	int glx_minor, glx_major;

private:
	void _nvapi_setup_profile();
	int _find_or_create_display(GLWindow &win);
	Error _create_context(GLWindow &win, GLDisplay &gl_display);

public:
	Error window_create(DisplaySystemEnums::WindowID p_window_id, HWND p_hwnd, HINSTANCE p_hinstance, int p_width, int p_height);
	void window_destroy(DisplaySystemEnums::WindowID p_window_id);
	void window_resize(DisplaySystemEnums::WindowID p_window_id, int p_width, int p_height) {}

	void release_current();
	void swap_buffers();

	void window_make_current(DisplaySystemEnums::WindowID p_window_id);

	Error initialize();

	void set_use_vsync(DisplaySystemEnums::WindowID p_window_id, bool p_use);
	bool is_using_vsync(DisplaySystemEnums::WindowID p_window_id) const;

	HDC get_hdc(DisplaySystemEnums::WindowID p_window_id);
	HGLRC get_hglrc(DisplaySystemEnums::WindowID p_window_id);

	GLManagerNative_Windows();
	~GLManagerNative_Windows();
};

#endif // WINDOWS_ENABLED && GLES3_ENABLED
