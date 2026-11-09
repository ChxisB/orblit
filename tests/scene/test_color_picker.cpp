/**************************************************************************/
/*  test_color_picker.cpp                                                 */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_color_picker)

#ifndef ADVANCED_GUI_DISABLED

#include "scene/ui/color_picker.h"
#include "scene/stage/stage.h"
#include "tests/display_system_mock.h"

namespace TestColorPicker {

TEST_CASE("[Stage][ColorPicker]") {
	ColorPicker *cp = memnew(ColorPicker);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(cp);

	SUBCASE("[COLOR_PICKER] Mouse movement after Slider release") {
		Point2i pos_left = Point2i(50, 340); // On the left side of the red slider.
		Point2i pos_right = Point2i(200, 340); // On the right side of the red slider.
		SEND_GUI_MOUSE_MOTION_EVENT(pos_left, MouseButtonMask::NONE, Key::NONE);
		SEND_GUI_MOUSE_BUTTON_EVENT(pos_left, MouseButton::LEFT, MouseButtonMask::LEFT, Key::NONE);
		CHECK(cp->get_pick_color().r < 0.5);
		SEND_GUI_MOUSE_MOTION_EVENT(pos_right, MouseButtonMask::LEFT, Key::NONE);
		CHECK(cp->get_pick_color().r > 0.5);
		SEND_GUI_MOUSE_BUTTON_RELEASED_EVENT(pos_right, MouseButton::LEFT, MouseButtonMask::NONE, Key::NONE);
		SEND_GUI_MOUSE_MOTION_EVENT(pos_left, MouseButtonMask::NONE, Key::NONE);
		CHECK(cp->get_pick_color().r > 0.5); // Issue GH-77773.
	}
}

} // namespace TestColorPicker

#endif // ADVANCED_GUI_DISABLED
