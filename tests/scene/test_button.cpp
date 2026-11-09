/**************************************************************************/
/*  test_button.cpp                                                       */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_button)

#include "scene/ui/button.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "tests/display_system_mock.h"

namespace TestButton {

TEST_CASE("[Stage][Button] is_hovered()") {
	// Create new button instance.
	Button *button = memnew(Button);
	CHECK(button != nullptr);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(button);

	// Set up button's size and position.
	button->set_size(Size2i(50, 50));
	button->set_position(Size2i(10, 10));

	// Button should initially be not hovered.
	CHECK(button->is_hovered() == false);

	// Simulate mouse entering the button.
	SEND_GUI_MOUSE_MOTION_EVENT(Point2i(25, 25), MouseButtonMask::NONE, Key::NONE);
	CHECK(button->is_hovered() == true);

	// Simulate mouse exiting the button.
	SEND_GUI_MOUSE_MOTION_EVENT(Point2i(150, 150), MouseButtonMask::NONE, Key::NONE);
	CHECK(button->is_hovered() == false);

	memdelete(button);
}

} // namespace TestButton
