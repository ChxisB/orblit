/**************************************************************************/
/*  dialogue_box.cpp                                                      */

#include "dialogue_box.h"

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "scene/scene_string_names.h"
#include "scene/ui/box_container.h"
#include "scene/ui/button.h"
#include "scene/ui/label.h"

namespace {

constexpr int CONTINUE = -1;
constexpr float WIDTH = 480;

} // namespace

void DialogueBox::register_members() {
	ClassRegistry::bind_method(D_METHOD("show_message", "speaker", "text", "choices"), &DialogueBox::show_message);
	ClassRegistry::bind_method(D_METHOD("choose", "index"), &DialogueBox::choose);
	ClassRegistry::bind_method(D_METHOD("get_speaker"), &DialogueBox::get_speaker);
	ClassRegistry::bind_method(D_METHOD("get_text"), &DialogueBox::get_text);
	ClassRegistry::bind_method(D_METHOD("get_choices"), &DialogueBox::get_choices);
	ADD_SIGNAL(MethodInfo("finished", PropertyInfo(Variant::INT, "index")));
}

void DialogueBox::_add_button(const String &p_text, int p_index) {
	Button *button = memnew(Button);
	button->set_text(p_text);
	button->connect(SceneStringName(pressed), callable_mp(this, &DialogueBox::choose).bind(p_index));
	choice_column->add_child(button);
}

void DialogueBox::show_message(const String &p_speaker, const String &p_text, const PackedStringArray &p_choices) {
	speaker_label->set_text(p_speaker);
	speaker_label->set_visible(!p_speaker.is_empty());
	text_label->set_text(p_text);
	choices = p_choices;
	for (int i = choice_column->get_child_count() - 1; i >= 0; i--) {
		Entity *old = choice_column->get_child(i);
		choice_column->remove_child(old);
		old->queue_destroy();
	}
	if (p_choices.is_empty()) {
		_add_button("Continue", CONTINUE);
	}
	for (int i = 0; i < p_choices.size(); i++) {
		_add_button(p_choices[i], i);
	}
}

void DialogueBox::choose(int p_index) {
	emit_signal("finished", p_index);
}

String DialogueBox::get_speaker() const {
	return speaker_label->get_text();
}

String DialogueBox::get_text() const {
	return text_label->get_text();
}

// The column and its labels are internal children, so packing a box into a prefab keeps only the box.
// shortcut: a fixed width at the bottom center with the default theme. A game that needs more
// makes its own prefab.
DialogueBox::DialogueBox() {
	set_anchors_preset(PRESET_CENTER_BOTTOM);
	set_h_grow_direction(GROW_DIRECTION_BOTH);
	set_v_grow_direction(GROW_DIRECTION_BEGIN);
	set_custom_minimum_size(Size2(WIDTH, 0));

	VBoxContainer *column = memnew(VBoxContainer);
	add_child(column, false, INTERNAL_MODE_FRONT);
	speaker_label = memnew(Label);
	column->add_child(speaker_label);
	text_label = memnew(Label);
	text_label->set_autowrap_mode(TextSystem::AUTOWRAP_WORD_SMART);
	column->add_child(text_label);
	choice_column = memnew(VBoxContainer);
	column->add_child(choice_column);
}
