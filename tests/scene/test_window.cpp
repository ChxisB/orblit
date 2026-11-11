/**************************************************************************/
/*  test_window.cpp                                                       */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_window)

#include "core/input/input_map.h" // IWYU pragma: keep // Used by `SEND_GUI_MOUSE_MOTION_EVENT` macro.
#include "core/object/message_queue.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/ui/control.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/label.h"
#include "scene/ui/panel_container.h"
#include "tests/display_system_mock.h"

namespace TestWindow {

class NotificationControlWindow : public Control {
	ORBLIT_CLASS(NotificationControlWindow, Control);

protected:
	void _notification(int p_what) {
		switch (p_what) {
			case NOTIFICATION_MOUSE_ENTER: {
				mouse_over = true;
			} break;

			case NOTIFICATION_MOUSE_EXIT: {
				mouse_over = false;
			} break;
		}
	}

public:
	bool mouse_over = false;
};

TEST_CASE("[Stage][Window]") {
	Window *root = Stage::get_singleton()->get_root();

	SUBCASE("Control-mouse-over within Window-black bars should not happen") {
		Window *w = memnew(Window);
		root->add_child(w);
		w->set_size(Size2i(400, 200));
		w->set_position(Size2i(0, 0));
		w->set_content_scale_size(Size2i(200, 200));
		w->set_content_scale_mode(Window::CONTENT_SCALE_MODE_CANVAS_ITEMS);
		w->set_content_scale_aspect(Window::CONTENT_SCALE_ASPECT_KEEP);
		NotificationControlWindow *c = memnew(NotificationControlWindow);
		w->add_child(c);
		c->set_size(Size2i(100, 100));
		c->set_position(Size2i(-50, -50));

		CHECK_FALSE(c->mouse_over);
		SEND_GUI_MOUSE_MOTION_EVENT(Point2i(110, 10), MouseButtonMask::NONE, Key::NONE);
		CHECK(c->mouse_over);
		SEND_GUI_MOUSE_MOTION_EVENT(Point2i(90, 10), MouseButtonMask::NONE, Key::NONE);
		CHECK_FALSE(c->mouse_over); // GH-80011

		/* TODO:
		SEND_GUI_MOUSE_BUTTON_EVENT(Point2i(90, 10), MouseButton::LEFT, MouseButtonMask::LEFT, Key::NONE);
		SEND_GUI_MOUSE_BUTTON_RELEASED_EVENT(Point2i(90, 10), MouseButton::LEFT, MouseButtonMask::NONE, Key::NONE);
		CHECK(Control was not pressed);
		*/

		memdelete(c);
		memdelete(w);
	}
}

TEST_CASE("[Stage][Window] a flag set to its current value does not reach the display system") {
	DisplaySystemMock *display = (DisplaySystemMock *)(DisplaySystem::get_singleton());
	Window *root = Stage::get_singleton()->get_root();

	const int before = display->window_flag_set_count;
	root->set_flag(Window::FLAG_BORDERLESS, root->get_flag(Window::FLAG_BORDERLESS));
	CHECK(display->window_flag_set_count == before);

	root->set_flag(Window::FLAG_BORDERLESS, !root->get_flag(Window::FLAG_BORDERLESS));
	CHECK(display->window_flag_set_count == before + 1);
	root->set_flag(Window::FLAG_BORDERLESS, false);
}

TEST_CASE("[Stage][Window] a wrapping label does not inflate a dialog on first popup") {
	AcceptDialog *dialog = memnew(AcceptDialog);
	Stage::get_singleton()->get_root()->add_child(dialog);
	PanelContainer *container = memnew(PanelContainer);
	container->set_custom_minimum_size(Size2(200, 0));
	dialog->add_child(container);
	Label *label = memnew(Label);
	label->set_autowrap_mode(TextSystem::AUTOWRAP_WORD_SMART);
	label->set_text("a b c d e f");
	container->add_child(label);

	dialog->popup_centered();
	MessageQueue::get_singleton()->flush();
	const Size2i first_size = dialog->get_size();
	dialog->hide();
	dialog->popup_centered();
	MessageQueue::get_singleton()->flush();

	CHECK(first_size == dialog->get_size());

	memdelete(dialog);
}

} // namespace TestWindow
