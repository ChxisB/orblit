/**************************************************************************/
/*  dialogue_box.h                                                        */

#pragma once

#include "scene/ui/panel_container.h"

class Label;
class VBoxContainer;

// A box that shows one line of dialogue with the choices under it. A Dialogue message step puts
// one on the stage. A game can swap it for a prefab whose root has the same show_message method
// and finished signal.
class DialogueBox : public PanelContainer {
	ORBLIT_CLASS(DialogueBox, PanelContainer);

	Label *speaker_label = nullptr;
	Label *text_label = nullptr;
	VBoxContainer *choice_column = nullptr;
	PackedStringArray choices;

	void _add_button(const String &p_text, int p_index);

protected:
	static void register_members();

public:
	// Shows the line, with one button per choice or one Continue button when there are none.
	void show_message(const String &p_speaker, const String &p_text, const PackedStringArray &p_choices);
	// Emits finished with the number of a choice, counted from zero, or -1 for Continue.
	void choose(int p_index);

	String get_speaker() const;
	String get_text() const;
	PackedStringArray get_choices() const { return choices; }

	DialogueBox();
};
