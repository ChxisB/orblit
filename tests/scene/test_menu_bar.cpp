/**************************************************************************/
/*  test_menu_bar.cpp                                                     */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_menu_bar)

#ifndef ADVANCED_GUI_DISABLED

#include "core/input/input_event.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/ui/menu_bar.h"
#include "scene/ui/popup_menu.h"

namespace TestMenuBar {

TEST_CASE("[Stage][MenuBar] An open popup follows the bar when the window moves") {
	MenuBar *menu_bar = memnew(MenuBar);
	PopupMenu *popup = memnew(PopupMenu);
	popup->set_name("File");
	popup->add_item("Open");
	menu_bar->add_child(popup);
	Stage::get_singleton()->get_root()->add_child(menu_bar);
	menu_bar->set_position(Point2(10, 10));

	Ref<InputEventMouseButton> click;
	click.instantiate();
	click->set_button_index(MouseButton::LEFT);
	click->set_pressed(true);
	click->set_position(Point2(5, 5));
	menu_bar->gui_input(click);
	REQUIRE(popup->is_visible());
	const Point2i opened_at = popup->get_position();

	menu_bar->set_position(Point2(60, 10));
	menu_bar->notification(Entity::NOTIFICATION_WM_POSITION_CHANGED);

	CHECK(popup->get_position().x == opened_at.x + 50);

	popup->hide();
	menu_bar->set_position(Point2(100, 10));
	menu_bar->notification(Entity::NOTIFICATION_WM_POSITION_CHANGED);
	CHECK_FALSE(popup->is_visible());

	memdelete(menu_bar);
}

} // namespace TestMenuBar

#endif // ADVANCED_GUI_DISABLED
