/**************************************************************************/
/*  gl_manager_x11.h                                                      */

#pragma once

#if defined(X11_ENABLED) && defined(GLES3_ENABLED)

#include "core/containers/local_vector.h"
#include "core/math/vector2i.h"
#include "systems/display/display_system_enums.h"

#ifdef SOWRAP_ENABLED
#include "dynwrappers/xlib-so_wrap.h"
#else
#include <X11/Xlib.h>
#include <X11/Xutil.h>
#endif

struct GLManager_X11_Private;

class GLManager_X11 {
public:
	enum ContextType {
		GLES_3_0_COMPATIBLE,
	};

private:
	// any data specific to the window
	struct GLWindow {
		bool in_use = false;

		// the external ID .. should match the GL window number .. unused I think
		DisplaySystemEnums::WindowID window_id = DisplaySystemEnums::INVALID_WINDOW_ID;
		int width = 0;
		int height = 0;
		::Window x11_window;
		int gldisplay_id = 0;
	};

	struct GLDisplay {
		GLDisplay() {}
		~GLDisplay();
		GLManager_X11_Private *context = nullptr;
		::Display *x11_display = nullptr;
		XVisualInfo x_vi = {};
		void *x_fbconfig = nullptr;
	};

	// just for convenience, window and display struct
	struct XWinDisp {
		::Window x11_window;
		::Display *x11_display = nullptr;
	} _x_windisp;

	LocalVector<GLWindow> _windows;
	LocalVector<GLDisplay> _displays;

	GLWindow *_current_window = nullptr;

	void _internal_set_current_window(GLWindow *p_win);

	GLWindow &get_window(unsigned int id) { return _windows[id]; }
	const GLWindow &get_window(unsigned int id) const { return _windows[id]; }

	const GLDisplay &get_current_display() const { return _displays[_current_window->gldisplay_id]; }
	const GLDisplay &get_display(unsigned int id) { return _displays[id]; }

	bool double_buffer;
	bool direct_render;
	int glx_minor, glx_major;
	bool use_vsync;
	ContextType context_type;

private:
	int _find_or_create_display(Display *p_x11_display);
	Error _create_context(GLDisplay &gl_display);

public:
	XVisualInfo get_vi(Display *p_display, Error &r_error);
	Error window_create(DisplaySystemEnums::WindowID p_window_id, ::Window p_window, Display *p_display, int p_width, int p_height);
	void window_destroy(DisplaySystemEnums::WindowID p_window_id);
	void window_resize(DisplaySystemEnums::WindowID p_window_id, int p_width, int p_height);

	void release_current();
	void swap_buffers();

	void window_make_current(DisplaySystemEnums::WindowID p_window_id);

	Error initialize(Display *p_display);

	void set_use_vsync(bool p_use);
	bool is_using_vsync() const;

	void *get_glx_context(DisplaySystemEnums::WindowID p_window_id);
	void *get_glx_fbconfig(DisplaySystemEnums::WindowID p_window_id);
	VisualID get_glx_visualid(DisplaySystemEnums::WindowID p_window_id);

	Error open_display(Display *p_display);
	GLManager_X11(const Vector2i &p_size, ContextType p_context_type);
	~GLManager_X11();
};

#endif // X11_ENABLED && GLES3_ENABLED
