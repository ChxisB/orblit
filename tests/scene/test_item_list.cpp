/**************************************************************************/
/*  test_item_list.cpp                                                    */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_item_list)

#ifndef ADVANCED_GUI_DISABLED

#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/ui/item_list.h"
#include "tests/display_system_mock.h"

namespace TestItemList {

TEST_CASE("[Stage][ItemList] Arrow keys after incremental search") {
	ItemList *list = memnew(ItemList);
	list->set_size(Size2(200, 200));
	list->add_item("apple");
	list->add_item("Banana");
	list->add_item("avocado");
	list->add_item("cherry");
	Stage::get_singleton()->get_root()->add_child(list);
	list->grab_focus();

	SUBCASE("Arrow keys move to the next match, ignoring case") {
		// Key::A types an uppercase "A", items are lowercase.
		SEND_GUI_KEY_EVENT(Key::A);
		CHECK_EQ(list->get_current(), 0);

		SEND_GUI_KEY_EVENT(Key::DOWN);
		SEND_GUI_KEY_UP_EVENT(Key::DOWN);
		CHECK_EQ(list->get_current(), 2);

		SEND_GUI_KEY_EVENT(Key::UP);
		SEND_GUI_KEY_UP_EVENT(Key::UP);
		CHECK_EQ(list->get_current(), 0);
	}

	SUBCASE("Down arrow still navigates when there is no next match") {
		SEND_GUI_KEY_EVENT(Key::B);
		CHECK_EQ(list->get_current(), 1);

		SEND_GUI_KEY_EVENT(Key::DOWN);
		SEND_GUI_KEY_UP_EVENT(Key::DOWN);
		CHECK_EQ(list->get_current(), 2);
	}

	SUBCASE("Up arrow still navigates when there is no previous match") {
		SEND_GUI_KEY_EVENT(Key::B);
		CHECK_EQ(list->get_current(), 1);

		SEND_GUI_KEY_EVENT(Key::UP);
		SEND_GUI_KEY_UP_EVENT(Key::UP);
		CHECK_EQ(list->get_current(), 0);
	}

	SUBCASE("Arrow keys navigate normally once the matches run out") {
		SEND_GUI_KEY_EVENT(Key::A);
		SEND_GUI_KEY_EVENT(Key::DOWN);
		SEND_GUI_KEY_UP_EVENT(Key::DOWN);
		SEND_GUI_KEY_EVENT(Key::DOWN);
		SEND_GUI_KEY_UP_EVENT(Key::DOWN);
		CHECK_EQ(list->get_current(), 3);

		SEND_GUI_KEY_EVENT(Key::UP);
		SEND_GUI_KEY_UP_EVENT(Key::UP);
		SEND_GUI_KEY_EVENT(Key::UP);
		SEND_GUI_KEY_UP_EVENT(Key::UP);
		CHECK_EQ(list->get_current(), 1);
	}

	memdelete(list);
}

} // namespace TestItemList

#endif // ADVANCED_GUI_DISABLED
