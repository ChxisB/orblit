/**************************************************************************/
/*  test_label.cpp                                                        */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_label)

#include "modules/modules_enabled.gen.h" // IWYU pragma: keep. Needed for MODULE_TEXT_SYSTEM_FALLBACK_ENABLED and MODULE_TEXT_SYSTEM_ADVANCED_ENABLED definitions.

#if defined(MODULE_TEXT_SYSTEM_FALLBACK_ENABLED) || defined(MODULE_TEXT_SYSTEM_ADVANCED_ENABLED)

#include "scene/ui/label.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace TestLabel {

TEST_CASE("[Stage][Label] Custom minimum size") {
	// This is an anti-regression test case introduced in GH-116640. When the old minimum size behavior is removed, this test case should be removed too.
	Label *test_label = memnew(Label);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(test_label);

	real_t min_width = 50;

	test_label->set_custom_minimum_size(Size2(min_width, 0));
	test_label->set_text("This is a long text that should be wrapped and exceeds minimum size.");
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase to fit the text with AUTOWRAP_OFF.");

	test_label->set_autowrap_mode(TextSystem::AUTOWRAP_ARBITRARY);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			Math::is_equal_approx(test_label->get_size().width, min_width),
			"Label width will be equal to custom minimum width with AUTOWRAP_ARBITRARY.");

	test_label->set_autowrap_mode(TextSystem::AUTOWRAP_WORD);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			Math::is_equal_approx(test_label->get_size().width, min_width),
			"Label width will be equal to custom minimum width with AUTOWRAP_WORD.");

	test_label->set_autowrap_mode(TextSystem::AUTOWRAP_WORD_SMART);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			Math::is_equal_approx(test_label->get_size().width, min_width),
			"Label width will be equal to custom minimum width with AUTOWRAP_WORD_SMART.");

	test_label->set_autowrap_mode(TextSystem::AUTOWRAP_OFF);
	test_label->set_clip_text(true);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			Math::is_equal_approx(test_label->get_size().width, min_width),
			"Label width will be equal to custom minimum width with clip_text.");

	test_label->set_clip_text(false);
	test_label->set_text_overrun_behavior(TextSystem::OVERRUN_TRIM_CHAR);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			Math::is_equal_approx(test_label->get_size().width, min_width),
			"Label width will be equal to custom minimum width with OVERRUN_TRIM_CHAR.");

	test_label->set_text_overrun_behavior(TextSystem::OVERRUN_TRIM_WORD);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			Math::is_equal_approx(test_label->get_size().width, min_width),
			"Label width will be equal to custom minimum width with OVERRUN_TRIM_WORD.");

	test_label->set_text_overrun_behavior(TextSystem::OVERRUN_TRIM_ELLIPSIS);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			Math::is_equal_approx(test_label->get_size().width, min_width),
			"Label width will be equal to custom minimum width with OVERRUN_TRIM_ELLIPSIS.");

	test_label->set_text_overrun_behavior(TextSystem::OVERRUN_TRIM_WORD_ELLIPSIS);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			Math::is_equal_approx(test_label->get_size().width, min_width),
			"Label width will be equal to custom minimum width with OVERRUN_TRIM_WORD_ELLIPSIS.");

	test_label->set_text_overrun_behavior(TextSystem::OVERRUN_TRIM_ELLIPSIS_FORCE);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			Math::is_equal_approx(test_label->get_size().width, min_width),
			"Label width will be equal to custom minimum width with OVERRUN_TRIM_ELLIPSIS_FORCE.");

	test_label->set_text_overrun_behavior(TextSystem::OVERRUN_TRIM_WORD_ELLIPSIS_FORCE);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			Math::is_equal_approx(test_label->get_size().width, min_width),
			"Label width will be equal to custom minimum width with OVERRUN_TRIM_WORD_ELLIPSIS_FORCE.");

	memdelete(test_label);
}

TEST_CASE("[Stage][Label] Sizing") {
	Label *test_label = memnew(Label);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(test_label);

	real_t max_width = 50;
	real_t min_width = 25;

	test_label->set_custom_maximum_size(Size2(max_width, -1));
	test_label->set_custom_minimum_size(Size2(min_width, 0));
	test_label->set_text("This is a long text that should be wrapped and exceeds minimum size.");
	Stage::get_singleton()->process(0);

	real_t label_height = test_label->get_size().height;

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with AUTOWRAP_OFF.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with AUTOWRAP_OFF.");

	test_label->set_autowrap_mode(TextSystem::AUTOWRAP_ARBITRARY);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with AUTOWRAP_ARBITRARY.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with AUTOWRAP_ARBITRARY.");
	CHECK_MESSAGE(
			test_label->get_size().height > label_height,
			"Label height will increase with AUTOWRAP_ARBITRARY.");

	test_label->set_autowrap_mode(TextSystem::AUTOWRAP_WORD);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with AUTOWRAP_WORD.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with AUTOWRAP_WORD.");
	CHECK_MESSAGE(
			test_label->get_size().height > label_height,
			"Label height will increase with AUTOWRAP_WORD.");

	test_label->set_autowrap_mode(TextSystem::AUTOWRAP_WORD_SMART);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with AUTOWRAP_WORD_SMART.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with AUTOWRAP_WORD_SMART.");
	CHECK_MESSAGE(
			test_label->get_size().height > label_height,
			"Label height will increase with AUTOWRAP_WORD_SMART.");

	test_label->set_autowrap_mode(TextSystem::AUTOWRAP_OFF);
	test_label->set_clip_text(true);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with clip_text.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with clip_text.");
	CHECK_MESSAGE(
			test_label->get_size().height == label_height,
			"Label height will not increase with clip_text.");

	test_label->set_clip_text(false);
	test_label->set_text_overrun_behavior(TextSystem::OVERRUN_TRIM_CHAR);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with OVERRUN_TRIM_CHAR.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with OVERRUN_TRIM_CHAR.");
	CHECK_MESSAGE(
			test_label->get_size().height == label_height,
			"Label height will not increase with OVERRUN_TRIM_CHAR.");

	test_label->set_text_overrun_behavior(TextSystem::OVERRUN_TRIM_WORD);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with OVERRUN_TRIM_WORD.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with OVERRUN_TRIM_WORD.");
	CHECK_MESSAGE(
			test_label->get_size().height == label_height,
			"Label height will not increase with OVERRUN_TRIM_WORD.");

	test_label->set_text_overrun_behavior(TextSystem::OVERRUN_TRIM_ELLIPSIS);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with OVERRUN_TRIM_ELLIPSIS.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with OVERRUN_TRIM_ELLIPSIS.");
	CHECK_MESSAGE(
			test_label->get_size().height == label_height,
			"Label height will not increase with OVERRUN_TRIM_ELLIPSIS.");

	test_label->set_text_overrun_behavior(TextSystem::OVERRUN_TRIM_WORD_ELLIPSIS);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with OVERRUN_TRIM_WORD_ELLIPSIS.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with OVERRUN_TRIM_WORD_ELLIPSIS.");
	CHECK_MESSAGE(
			test_label->get_size().height == label_height,
			"Label height will not increase with OVERRUN_TRIM_WORD_ELLIPSIS.");

	test_label->set_text_overrun_behavior(TextSystem::OVERRUN_TRIM_ELLIPSIS_FORCE);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with OVERRUN_TRIM_ELLIPSIS_FORCE.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with OVERRUN_TRIM_ELLIPSIS_FORCE.");
	CHECK_MESSAGE(
			test_label->get_size().height == label_height,
			"Label height will not increase with OVERRUN_TRIM_ELLIPSIS_FORCE.");

	test_label->set_text_overrun_behavior(TextSystem::OVERRUN_TRIM_WORD_ELLIPSIS_FORCE);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with OVERRUN_TRIM_WORD_ELLIPSIS_FORCE.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with OVERRUN_TRIM_WORD_ELLIPSIS_FORCE.");
	CHECK_MESSAGE(
			test_label->get_size().height == label_height,
			"Label height will not increase with OVERRUN_TRIM_WORD_ELLIPSIS_FORCE.");

	memdelete(test_label);
}

} // namespace TestLabel

#endif // MODULE_TEXT_SYSTEM_FALLBACK_ENABLED || MODULE_TEXT_SYSTEM_ADVANCED_ENABLED
