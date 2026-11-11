/**************************************************************************/
/*  test_button.cpp                                                       */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_button)

#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/ui/button.h"
#include "tests/display_system_mock.h"
#include "tests/signal_watcher.h"

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

TEST_CASE("[Stage][Button] A touch on a button in a shifted viewport presses it") {
	Button *button = memnew(Button);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(button);
	button->set_size(Size2i(50, 50));
	button->set_position(Size2i(10, 10));
	root->set_canvas_transform(Transform2D().translated(Vector2(300, 0)));

	SIGNAL_WATCH(button, "pressed");

	// The button now sits at x 310 on screen, so this touch lands on it.
	SEND_GUI_TOUCH_EVENT(Point2i(335, 35), true, false);
	SEND_GUI_TOUCH_EVENT(Point2i(335, 35), false, false);
	SIGNAL_CHECK("pressed", { {} });

	// A touch on the old, unshifted spot misses the button.
	SEND_GUI_TOUCH_EVENT(Point2i(35, 35), true, false);
	SEND_GUI_TOUCH_EVENT(Point2i(35, 35), false, false);
	SIGNAL_CHECK_FALSE("pressed");

	SIGNAL_UNWATCH(button, "pressed");
	root->set_canvas_transform(Transform2D());
	memdelete(button);
}

TEST_CASE("[Stage][Button] A long press notification does not press a button again") {
	Button *button = memnew(Button);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(button);
	button->set_size(Size2i(50, 50));
	button->set_position(Size2i(10, 10));
	button->set_action_mode(BaseButton::ACTION_MODE_BUTTON_PRESS);

	SIGNAL_WATCH(button, "pressed");

	SEND_GUI_TOUCH_EVENT(Point2i(35, 35), true, false);
	SIGNAL_CHECK("pressed", { {} });

	// The platform cancels the touch, then reports the same hold as a long press.
	{
		_CREATE_GUI_TOUCH_EVENT(Point2i(35, 35), false, false);
		event->set_canceled(true);
		_SEND_DISPLAYSYSTEM_EVENT(event);
		MessageQueue::get_singleton()->flush();
	}
	{
		_CREATE_GUI_TOUCH_EVENT(Point2i(35, 35), true, false);
		event->set_long_press(true);
		_SEND_DISPLAYSYSTEM_EVENT(event);
		MessageQueue::get_singleton()->flush();
	}
	SIGNAL_CHECK_FALSE("pressed");

	{
		_CREATE_GUI_TOUCH_EVENT(Point2i(35, 35), false, false);
		event->set_long_press(true);
		_SEND_DISPLAYSYSTEM_EVENT(event);
		MessageQueue::get_singleton()->flush();
	}
	SIGNAL_CHECK_FALSE("pressed");

	SIGNAL_UNWATCH(button, "pressed");
	memdelete(button);
}

} // namespace TestButton
