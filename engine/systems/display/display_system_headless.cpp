/**************************************************************************/
/*  display_system_headless.cpp                                           */

#include "display_system_headless.h"

#include "core/input/input.h"
#include "core/input/input_event.h"
#include "systems/display/native_menu.h"
#include "systems/rendering/dummy/rasterizer_dummy.h"

DisplaySystem *DisplaySystemHeadless::create_func(const String &p_rendering_driver, DisplaySystemEnums::WindowMode p_mode, DisplaySystemEnums::VSyncMode p_vsync_mode, uint32_t p_flags, const Vector2i *p_position, const Vector2i &p_resolution, int p_screen, DisplaySystemEnums::Context p_context, int64_t p_parent_window, Error &r_error) {
	r_error = OK;
	RasterizerDummy::make_current();
	return memnew(DisplaySystemHeadless());
}

void DisplaySystemHeadless::_dispatch_input_events(const Ref<InputEvent> &p_event) {
	static_cast<DisplaySystemHeadless *>(get_singleton())->_dispatch_input_event(p_event);
}

void DisplaySystemHeadless::_dispatch_input_event(const Ref<InputEvent> &p_event) {
	if (input_event_callback.is_valid()) {
		input_event_callback.call(p_event);
	}
}

void DisplaySystemHeadless::process_events() {
	Input::get_singleton()->flush_buffered_events();
}

DisplaySystemHeadless::DisplaySystemHeadless() {
	native_menu = memnew(NativeMenu);
	Input::get_singleton()->set_event_dispatch_function(_dispatch_input_events);
}

DisplaySystemHeadless::~DisplaySystemHeadless() {
	if (native_menu) {
		memdelete(native_menu);
		native_menu = nullptr;
	}
}
