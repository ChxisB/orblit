/**************************************************************************/
/*  test_rich_text_label.cpp                                              */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_rich_text_label)

#ifndef ADVANCED_GUI_DISABLED

#include "scene/ui/rich_text_label.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"

namespace TestRichTextLabel {

TEST_CASE("[Stage][RichTextLabel] Custom minimum size with fit content") {
	// This is an anti-regression test case introduced in GH-116640. When the old minimum size behavior is removed, this test case should be removed too.
	RichTextLabel *test_label = memnew(RichTextLabel);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(test_label);

	real_t min_width = 50;

	test_label->set_fit_content(true);
	test_label->set_custom_minimum_size(Size2(min_width, 0));
	test_label->set_autowrap_mode(TextSystem::AUTOWRAP_OFF);
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

	memdelete(test_label);
}

TEST_CASE("[Stage][RichTextLabel] Sizing with fit content") {
	RichTextLabel *test_label = memnew(RichTextLabel);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(test_label);

	real_t max_width = 50;
	real_t min_width = 25;

	test_label->set_fit_content(true);
	test_label->set_custom_maximum_size(Size2(max_width, 0));
	test_label->set_custom_minimum_size(Size2(min_width, 0));
	test_label->set_autowrap_mode(TextSystem::AUTOWRAP_OFF);
	test_label->set_text("This is a long text that should be wrapped and exceeds minimum size.");
	Stage::get_singleton()->process(0);

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

	test_label->set_autowrap_mode(TextSystem::AUTOWRAP_WORD);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with AUTOWRAP_WORD.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with AUTOWRAP_WORD.");

	test_label->set_autowrap_mode(TextSystem::AUTOWRAP_WORD_SMART);
	Stage::get_singleton()->process(0);

	CHECK_MESSAGE(
			test_label->get_size().width <= max_width,
			"Label width will increase up to the custom maximum width with AUTOWRAP_WORD_SMART.");
	CHECK_MESSAGE(
			test_label->get_size().width > min_width,
			"Label width will increase beyond the custom minimum width with AUTOWRAP_WORD_SMART.");

	memdelete(test_label);
}

TEST_CASE("[Stage][RichTextLabel] Font size wrapping paragraph does not add empty first line") {
	RichTextLabel *test_label = memnew(RichTextLabel);
	Window *root = Stage::get_singleton()->get_root();
	root->add_child(test_label);

	test_label->set_use_bbcode(true);

	test_label->set_text("[p][font_size=22]aaa[/font_size][/p]");
	Stage::get_singleton()->process(0);
	CHECK(test_label->get_paragraph_count() == 1);
	CHECK(test_label->get_character_line(0) == 0);

	test_label->set_text("[font_size=22][p]aaa[/p][/font_size]");
	Stage::get_singleton()->process(0);
	CHECK(test_label->get_paragraph_count() == 1);
	CHECK(test_label->get_character_line(0) == 0);

	memdelete(test_label);
}

} // namespace TestRichTextLabel

#endif // ADVANCED_GUI_DISABLED
