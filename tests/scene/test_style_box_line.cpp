/**************************************************************************/
/*  test_style_box_line.cpp                                               */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_style_box_line)

#include "scene/resources/style_box_line.h"

namespace TestStyleBoxLine {

TEST_CASE("[StyleBoxLine] Constructor") {
	Ref<StyleBoxLine> style_box_line = memnew(StyleBoxLine);

	CHECK(style_box_line->get_color() == Color(0, 0, 0, 1));
	CHECK(style_box_line->get_thickness() == 1);
	CHECK(style_box_line->is_vertical() == false);
	CHECK(style_box_line->get_grow_begin() == 1.0);
	CHECK(style_box_line->get_grow_end() == 1.0);
}

TEST_CASE("[StyleBoxLine] set_color, get_color") {
	Ref<StyleBoxLine> style_box_line = memnew(StyleBoxLine);
	Color color = Color(0.1, 0.2, 0.3, 1.0);

	style_box_line->set_color(color);
	CHECK(style_box_line->get_color() == color);
}

TEST_CASE("[StyleBoxLine] set_thickness, get_thickness") {
	Ref<StyleBoxLine> style_box_line = memnew(StyleBoxLine);

	style_box_line->set_thickness(5);
	CHECK(style_box_line->get_thickness() == 5);
}

TEST_CASE("[StyleBoxLine] set_vertical, is_vertical") {
	Ref<StyleBoxLine> style_box_line = memnew(StyleBoxLine);

	style_box_line->set_vertical(true);
	CHECK(style_box_line->is_vertical() == true);
}

TEST_CASE("[StyleBoxLine] set_vertical, is_vertical") {
	Ref<StyleBoxLine> style_box_line = memnew(StyleBoxLine);

	style_box_line->set_vertical(true);
	CHECK(style_box_line->is_vertical() == true);
}

TEST_CASE("[StyleBoxLine] set_grow_begin, get_grow_begin, set_grow_end, get_grow_end") {
	Ref<StyleBoxLine> style_box_line = memnew(StyleBoxLine);
	float grow_value = 3.5;

	SUBCASE("set_grow_begin, get_grow_begin") {
		style_box_line->set_grow_begin(grow_value);
		CHECK(style_box_line->get_grow_begin() == grow_value);
	}

	SUBCASE("set_grow_end, get_grow_end") {
		style_box_line->set_grow_end(grow_value);
		CHECK(style_box_line->get_grow_end() == grow_value);
	}
}

} // namespace TestStyleBoxLine
