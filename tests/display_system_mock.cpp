/**************************************************************************/
/*  display_system_mock.cpp                                               */

#include "display_system_mock.h"

#include "core/input/input.h"
#include "core/input/input_event.h"
#include "systems/rendering/dummy/rasterizer_dummy.h"

Vector<String> DisplaySystemMock::get_rendering_drivers_func() {
	Vector<String> drivers;
	drivers.push_back("dummy");
	return drivers;
}

void DisplaySystemMock::_set_mouse_position(const Point2i &p_position) {
	if (mouse_position == p_position) {
		return;
	}
	mouse_position = p_position;
	_set_window_over(Rect2i(Point2i(0, 0), window_get_size()).has_point(p_position));
}
void DisplaySystemMock::_set_window_over(bool p_over) {
	if (p_over == window_over) {
		return;
	}
	window_over = p_over;
	_send_window_event(p_over ? DisplaySystemEnums::WINDOW_EVENT_MOUSE_ENTER : DisplaySystemEnums::WINDOW_EVENT_MOUSE_EXIT);
}
void DisplaySystemMock::_send_window_event(DisplaySystemEnums::WindowEvent p_event) {
	if (event_callback.is_valid()) {
		Variant event = int(p_event);
		event_callback.call(event);
	}
}

bool DisplaySystemMock::has_feature(DisplaySystemEnums::Feature p_feature) const {
	switch (p_feature) {
		case DisplaySystemEnums::FEATURE_MOUSE:
		case DisplaySystemEnums::FEATURE_CURSOR_SHAPE:
		case DisplaySystemEnums::FEATURE_CLIPBOARD:
		case DisplaySystemEnums::FEATURE_CLIPBOARD_PRIMARY:
			return true;
		default: {
		}
	}
	return false;
}

DisplaySystem *DisplaySystemMock::create_func(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error) {
	r_error = OK;
	RasterizerDummy::make_current();
	return memnew(DisplaySystemMock());
}

void DisplaySystemMock::simulate_event(Ref<InputEvent> p_event) {
	Ref<InputEvent> event = p_event;
	Ref<InputEventMouse> me = p_event;
	if (me.is_valid()) {
		Ref<InputEventMouseMotion> mm = p_event;
		if (mm.is_valid()) {
			mm->set_relative(mm->get_position() - mouse_position);
			event = mm;
		}
		_set_mouse_position(me->get_position());
	}
	Input::get_singleton()->parse_input_event(event);
}
