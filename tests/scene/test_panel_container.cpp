/**************************************************************************/
/*  test_panel_container.cpp                                              */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_panel_container)

#include "scene/resources/style_box.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/ui/control.h"
#include "scene/ui/panel_container.h"

namespace TestPanelContainer {

TEST_CASE("[Stage][PanelContainer] Default properties") {
	PanelContainer *panel_container = memnew(PanelContainer);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(panel_container);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			panel_container->get_mouse_filter() == Control::MOUSE_FILTER_STOP,
			"PanelContainer mouse filter is set to MOUSE_FILTER_STOP by default.");

	memdelete(panel_container);
}

TEST_CASE("[Stage][PanelContainer] StyleBox affects layout and minimum size") {
	PanelContainer *panel_container = memnew(PanelContainer);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(panel_container);

	Ref<StyleBoxEmpty> panel_style = memnew(StyleBoxEmpty);
	panel_style->set_content_margin_individual(5, 6, 7, 8);
	panel_container->add_theme_style_override("panel", panel_style);

	Control *child_control = memnew(Control);
	child_control->set_custom_minimum_size(Size2(20, 10));
	panel_container->add_child(child_control);

	panel_container->set_size(Size2(100, 80));
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			child_control->get_position().is_equal_approx(Point2(5, 6)),
			"Child control is offset by the panel StyleBox margins.");

	CHECK_MESSAGE(
			child_control->get_size().is_equal_approx(Size2(88, 66)),
			"Child control size is reduced by the panel StyleBox minimum size.");

	CHECK_MESSAGE(
			panel_container->get_minimum_size().is_equal_approx(Size2(32, 24)),
			"Minimum size equals child minimum size plus panel StyleBox minimum size.");

	memdelete(child_control);
	memdelete(panel_container);
}

TEST_CASE("[Stage][PanelContainer] Multiple children use maximum minimum size") {
	PanelContainer *panel_container = memnew(PanelContainer);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(panel_container);

	Ref<StyleBoxEmpty> panel_style = memnew(StyleBoxEmpty);
	panel_style->set_content_margin_individual(2, 3, 4, 5);
	panel_container->add_theme_style_override("panel", panel_style);

	Control *child_control_1 = memnew(Control);
	Control *child_control_2 = memnew(Control);
	child_control_1->set_custom_minimum_size(Size2(40, 10));
	child_control_2->set_custom_minimum_size(Size2(20, 50));
	panel_container->add_child(child_control_1);
	panel_container->add_child(child_control_2);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			panel_container->get_minimum_size().is_equal_approx(Size2(46, 58)),
			"Minimum size uses maximum child minimum width/height plus panel StyleBox minimum size.");

	memdelete(child_control_2);
	memdelete(child_control_1);
	memdelete(panel_container);
}

} // namespace TestPanelContainer
