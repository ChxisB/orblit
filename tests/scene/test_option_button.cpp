/**************************************************************************/
/*  test_option_button.cpp                                                */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_option_button)

#ifndef ADVANCED_GUI_DISABLED

#include "scene/ui/option_button.h"

namespace TestOptionButton {

TEST_CASE("[Stage][OptionButton] Initialization") {
	OptionButton *test_opt = memnew(OptionButton);

	SUBCASE("There should be no options right after initialization") {
		CHECK_FALSE(test_opt->has_selectable_items());
		CHECK(test_opt->get_item_count() == 0);
	}

	memdelete(test_opt);
}

TEST_CASE("[Stage][OptionButton] Single item") {
	OptionButton *test_opt = memnew(OptionButton);

	SUBCASE("There should a single item after after adding one") {
		test_opt->add_item("single", 1013);

		CHECK(test_opt->has_selectable_items());
		CHECK(test_opt->get_item_count() == 1);
		CHECK(test_opt->get_item_index(1013) == 0);
		CHECK(test_opt->get_item_id(0) == 1013);

		test_opt->remove_item(0);

		CHECK_FALSE(test_opt->has_selectable_items());
		CHECK(test_opt->get_item_count() == 0);
	}

	SUBCASE("There should a single item after after adding an icon") {
		Ref<Texture2D> test_icon = memnew(Texture2D);
		test_opt->add_icon_item(test_icon, "icon", 345);

		CHECK(test_opt->has_selectable_items());
		CHECK(test_opt->get_item_count() == 1);
		CHECK(test_opt->get_item_index(345) == 0);
		CHECK(test_opt->get_item_id(0) == 345);

		test_opt->remove_item(0);

		CHECK_FALSE(test_opt->has_selectable_items());
		CHECK(test_opt->get_item_count() == 0);
	}

	memdelete(test_opt);
}

TEST_CASE("[Stage][OptionButton] Many items") {
	OptionButton *test_opt = memnew(OptionButton);

	SUBCASE("Creating a complex structure and checking getters") {
		// Regular item at index 0.
		Ref<Texture2D> test_icon1 = memnew(Texture2D);
		Ref<Texture2D> test_icon2 = memnew(Texture2D);
		// Regular item at index 3.
		Ref<Texture2D> test_icon4 = memnew(Texture2D);

		test_opt->add_item("first", 100);
		test_opt->add_icon_item(test_icon1, "second_icon", 101);
		test_opt->add_icon_item(test_icon2, "third_icon", 102);
		test_opt->add_item("fourth", 104);
		test_opt->add_icon_item(test_icon4, "fifth_icon", 104);

		// Disable test_icon4.
		test_opt->set_item_disabled(4, true);

		CHECK(test_opt->has_selectable_items());
		CHECK(test_opt->get_item_count() == 5);

		// Check for test_icon2.
		CHECK(test_opt->get_item_index(102) == 2);
		CHECK(test_opt->get_item_id(2) == 102);

		// Remove the two regular items.
		test_opt->remove_item(3);
		test_opt->remove_item(0);

		CHECK(test_opt->has_selectable_items());
		CHECK(test_opt->get_item_count() == 3);

		// Check test_icon4.
		CHECK(test_opt->get_item_index(104) == 2);
		CHECK(test_opt->get_item_id(2) == 104);

		// Remove the two non-disabled icon items.
		test_opt->remove_item(1);
		test_opt->remove_item(0);

		CHECK_FALSE(test_opt->has_selectable_items());
		CHECK(test_opt->get_item_count() == 1);
	}

	SUBCASE("Getters and setters not related to structure") {
		test_opt->add_item("regular", 2019);

		Ref<Texture2D> test_icon = memnew(Texture2D);
		test_opt->add_icon_item(test_icon, "icon", 3092);

		// item_text.
		test_opt->set_item_text(0, "example text");
		CHECK(test_opt->get_item_text(0) == "example text");

		// item_metadata.
		Dictionary m;
		m["bool"] = true;
		m["String"] = "yes";
		test_opt->set_item_metadata(1, m);
		CHECK(test_opt->get_item_metadata(1) == m);

		// item_tooltip.
		test_opt->set_item_tooltip(0, "tooltip guide");
		CHECK(test_opt->get_item_tooltip(0) == "tooltip guide");

		test_opt->remove_item(1);
		test_opt->remove_item(0);
	}

	memdelete(test_opt);
}

} // namespace TestOptionButton

#endif // ADVANCED_GUI_DISABLED
