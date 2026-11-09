/**************************************************************************/
/*  test_reference_rect.cpp                                               */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_reference_rect)

#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/ui/reference_rect.h"

namespace TestReferenceRect {

TEST_CASE("[Stage][ReferenceRect] Default properties") {
	ReferenceRect *reference_rect = memnew(ReferenceRect);

	CHECK(reference_rect->get_border_color() == Color(1, 0, 0, 1));
	CHECK(reference_rect->get_border_width() == 1.0f);
	CHECK(reference_rect->get_editor_only());

	CHECK(reference_rect->get_mouse_filter() == Control::MOUSE_FILTER_STOP);

	CHECK(reference_rect->get_combined_minimum_size() == Size2(0, 0));

	memdelete(reference_rect);
}

TEST_CASE("[Stage][ReferenceRect] Border color") {
	ReferenceRect *reference_rect = memnew(ReferenceRect);

	const Color c = Color(0.25f, 0.5f, 0.75f, 0.9f);
	reference_rect->set_border_color(c);
	CHECK(reference_rect->get_border_color() == c);

	memdelete(reference_rect);
}

TEST_CASE("[Stage][ReferenceRect] Border width") {
	ReferenceRect *reference_rect = memnew(ReferenceRect);

	SUBCASE("Positive width is stored") {
		reference_rect->set_border_width(3.5f);
		CHECK(reference_rect->get_border_width() == 3.5f);
	}

	SUBCASE("Negative width clamps to zero") {
		reference_rect->set_border_width(-2.0f);
		CHECK(reference_rect->get_border_width() == 0.0f);
	}

	SUBCASE("Width above the inspector hint range is still stored") {
		reference_rect->set_border_width(12.0f);
		CHECK(reference_rect->get_border_width() == 12.0f);
	}

	memdelete(reference_rect);
}

TEST_CASE("[Stage][ReferenceRect] Editor only flag") {
	ReferenceRect *reference_rect = memnew(ReferenceRect);

	SUBCASE("editor_only can be disabled") {
		reference_rect->set_editor_only(false);
		CHECK_FALSE(reference_rect->get_editor_only());
	}

	SUBCASE("editor_only can be re-enabled") {
		reference_rect->set_editor_only(false);
		reference_rect->set_editor_only(true);
		CHECK(reference_rect->get_editor_only());
	}

	SUBCASE("Toggling editor_only does not change minimum size") {
		reference_rect->set_editor_only(false);
		CHECK(reference_rect->get_combined_minimum_size() == Size2(0, 0));

		reference_rect->set_editor_only(true);
		CHECK(reference_rect->get_combined_minimum_size() == Size2(0, 0));
	}

	memdelete(reference_rect);
}

TEST_CASE("[Stage][ReferenceRect] Minimum size on tree") {
	ReferenceRect *reference_rect = memnew(ReferenceRect);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(reference_rect);

	reference_rect->set_border_width(4.0f);
	reference_rect->set_editor_only(false);
	reference_rect->set_border_color(Color(0, 1, 0, 1));
	Stage::get_singleton()->process(0);

	CHECK(reference_rect->get_combined_minimum_size() == Size2(0, 0));

	memdelete(reference_rect);
}

} // namespace TestReferenceRect
