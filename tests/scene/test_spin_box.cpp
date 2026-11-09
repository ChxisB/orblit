/**************************************************************************/
/*  test_spin_box.cpp                                                     */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_spin_box)

#ifndef ADVANCED_GUI_DISABLED

#include "core/math/math_defs.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "scene/ui/spin_box.h"

namespace TestSpinBox {

TEST_CASE("[Stage][SpinBox] Line edit and range defaults") {
	SpinBox *spin = memnew(SpinBox);
	LineEdit *le = spin->get_line_edit();

	CHECK(le != nullptr);
	CHECK(spin->get_value() == doctest::Approx(0.0));
	CHECK(spin->get_min() == doctest::Approx(0.0));
	CHECK(spin->get_max() == doctest::Approx(100.0));
	CHECK(spin->get_step() == doctest::Approx(1.0));
	CHECK_FALSE(spin->is_greater_allowed());
	CHECK_FALSE(spin->is_lesser_allowed());

	memdelete(spin);
}

TEST_CASE("[Stage][SpinBox] Range clamping and allow flags") {
	SpinBox *spin = memnew(SpinBox);
	spin->set_max(10.0);
	spin->set_value(20.0);
	CHECK(spin->get_value() == doctest::Approx(10.0));

	spin->set_allow_greater(true);
	spin->set_value(20.0);
	CHECK(spin->get_value() == doctest::Approx(20.0));

	spin->set_min(0.0);
	spin->set_allow_lesser(false);
	spin->set_value(-5.0);
	CHECK(spin->get_value() == doctest::Approx(0.0));

	spin->set_allow_lesser(true);
	spin->set_value(-3.0);
	CHECK(spin->get_value() == doctest::Approx(-3.0));

	memdelete(spin);
}

TEST_CASE("[Stage][SpinBox] Prefix and suffix in line edit text") {
	SpinBox *spin = memnew(SpinBox);
	spin->set_format("px %s deg");
	spin->set_value(42.0);

	Window *root = Stage::get_singleton()->get_root();
	root->add_child(spin);
	Stage::get_singleton()->process(0);

	LineEdit *le = spin->get_line_edit();
	const String t = le->get_text();
	CHECK(t.contains("px"));
	CHECK(t.contains("deg"));
	CHECK_FALSE(t.is_empty());
	CHECK(spin->get_value() == doctest::Approx(42.0));

	memdelete(spin);
}

TEST_CASE("[Stage][SpinBox] Delegation to inner line edit") {
	SpinBox *spin = memnew(SpinBox);

	spin->set_horizontal_alignment(HORIZONTAL_ALIGNMENT_RIGHT);
	CHECK(spin->get_horizontal_alignment() == HORIZONTAL_ALIGNMENT_RIGHT);

	spin->set_editable(false);
	CHECK_FALSE(spin->is_editable());
	spin->set_editable(true);
	CHECK(spin->is_editable());

	spin->set_select_all_on_focus(true);
	CHECK(spin->is_select_all_on_focus());

	memdelete(spin);
}

TEST_CASE("[Stage][SpinBox] update_on_text_changed toggle") {
	SpinBox *spin = memnew(SpinBox);

	CHECK_FALSE(spin->get_update_on_text_changed());
	spin->set_update_on_text_changed(true);
	CHECK(spin->get_update_on_text_changed());
	Stage::get_singleton()->process(0);
	spin->set_update_on_text_changed(false);
	CHECK_FALSE(spin->get_update_on_text_changed());

	memdelete(spin);
}

TEST_CASE("[Stage][SpinBox] apply parses expression") {
	SpinBox *spin = memnew(SpinBox);
	spin->set_max(1000.0);
	LineEdit *le = spin->get_line_edit();

	le->set_text("10+5");
	spin->apply();
	CHECK(spin->get_value() == doctest::Approx(15.0));

	const double before_invalid = spin->get_value();
	le->set_text("___not_an_expression___");
	spin->apply();
	CHECK(spin->get_value() == doctest::Approx(before_invalid));

	memdelete(spin);
}

TEST_CASE("[Stage][SpinBox] Custom arrow step and round getters") {
	SpinBox *spin = memnew(SpinBox);

	spin->set_custom_arrow_step(2.5);
	CHECK(spin->get_custom_arrow_step() == doctest::Approx(2.5));

	spin->set_custom_arrow_round(true);
	CHECK(spin->is_custom_arrow_rounding());
	spin->set_custom_arrow_round(false);
	CHECK_FALSE(spin->is_custom_arrow_rounding());

	memdelete(spin);
}

TEST_CASE("[Stage][SpinBox] Combined minimum size in tree") {
	SpinBox *spin = memnew(SpinBox);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(spin);
	Stage::get_singleton()->process(0);

	const Size2 cms = spin->get_combined_minimum_size();
	CHECK(cms.width > 0);
	CHECK(cms.height > 0);

	memdelete(spin);
}

} // namespace TestSpinBox

#endif // ADVANCED_GUI_DISABLED
