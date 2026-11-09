/**************************************************************************/
/*  test_margin_container.cpp                                             */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_margin_container)

#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/ui/control.h"
#include "scene/ui/margin_container.h"

namespace TestMarginContainer {

TEST_CASE("[Stage][MarginContainer] Default margins and layout") {
	MarginContainer *margin_container = memnew(MarginContainer);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(margin_container);

	Control *child_control = memnew(Control);
	child_control->set_custom_minimum_size(Size2(20, 10));
	margin_container->add_child(child_control);

	margin_container->set_size(Size2(100, 80));
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			margin_container->get_margin_size(SIDE_LEFT) == 0,
			"Left margin is 0 by default.");

	CHECK_MESSAGE(
			margin_container->get_margin_size(SIDE_TOP) == 0,
			"Top margin is 0 by default.");

	CHECK_MESSAGE(
			margin_container->get_margin_size(SIDE_RIGHT) == 0,
			"Right margin is 0 by default.");

	CHECK_MESSAGE(
			margin_container->get_margin_size(SIDE_BOTTOM) == 0,
			"Bottom margin is 0 by default.");

	CHECK_MESSAGE(
			child_control->get_position().is_equal_approx(Point2(0, 0)),
			"Child control starts at the top-left corner when all margins are 0.");

	CHECK_MESSAGE(
			child_control->get_size().is_equal_approx(Size2(100, 80)),
			"Child control fills the container area when all margins are 0.");

	CHECK_MESSAGE(
			margin_container->get_minimum_size().is_equal_approx(Size2(20, 10)),
			"Minimum size equals the child minimum size when all margins are 0.");

	memdelete(child_control);
	memdelete(margin_container);
}

TEST_CASE("[Stage][MarginContainer] Custom margins affect layout and minimum size") {
	MarginContainer *margin_container = memnew(MarginContainer);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(margin_container);

	margin_container->add_theme_constant_override("margin_left", 10);
	margin_container->add_theme_constant_override("margin_top", 20);
	margin_container->add_theme_constant_override("margin_right", 30);
	margin_container->add_theme_constant_override("margin_bottom", 40);

	Control *child_control = memnew(Control);
	child_control->set_custom_minimum_size(Size2(20, 10));
	margin_container->add_child(child_control);

	margin_container->set_size(Size2(200, 150));
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			child_control->get_position().is_equal_approx(Point2(10, 20)),
			"Child control is offset by left and top margins.");

	CHECK_MESSAGE(
			child_control->get_size().is_equal_approx(Size2(160, 90)),
			"Child control size is reduced by left/right and top/bottom margins.");

	CHECK_MESSAGE(
			margin_container->get_minimum_size().is_equal_approx(Size2(60, 70)),
			"Minimum size equals child minimum size plus all configured margins.");

	memdelete(child_control);
	memdelete(margin_container);
}

TEST_CASE("[Stage][MarginContainer] Multiple children use maximum minimum size") {
	MarginContainer *margin_container = memnew(MarginContainer);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(margin_container);

	margin_container->add_theme_constant_override("margin_left", 1);
	margin_container->add_theme_constant_override("margin_top", 2);
	margin_container->add_theme_constant_override("margin_right", 3);
	margin_container->add_theme_constant_override("margin_bottom", 4);

	Control *child_control_1 = memnew(Control);
	Control *child_control_2 = memnew(Control);
	child_control_1->set_custom_minimum_size(Size2(40, 10));
	child_control_2->set_custom_minimum_size(Size2(20, 50));
	margin_container->add_child(child_control_1);
	margin_container->add_child(child_control_2);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			margin_container->get_minimum_size().is_equal_approx(Size2(44, 56)),
			"Minimum size uses maximum child minimum width/height plus margins.");

	memdelete(child_control_2);
	memdelete(child_control_1);
	memdelete(margin_container);
}

} // namespace TestMarginContainer
