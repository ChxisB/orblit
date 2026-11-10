/**************************************************************************/
/*  openxr_action_set_editor.h                                            */

#pragma once

#include "../action_map/openxr_action_map.h"
#include "../action_map/openxr_action_set.h"
#include "openxr_action_editor.h"

class EditorSpinSlider;
class BoxContainer;
class Button;
class LineEdit;
class PanelContainer;
class TextEdit;

class OpenXRActionSetEditor : public HBoxContainer {
	ORBLIT_CLASS(OpenXRActionSetEditor, HBoxContainer);

private:
	EditorUndoRedoManager *undo_redo;
	Ref<OpenXRActionMap> action_map;
	Ref<OpenXRActionSet> action_set;

	bool is_expanded = true;

	PanelContainer *panel = nullptr;
	Button *fold_btn = nullptr;
	VBoxContainer *main_vb = nullptr;
	HBoxContainer *action_set_hb = nullptr;
	LineEdit *action_set_name = nullptr;
	LineEdit *action_set_localized_name = nullptr;
	EditorSpinSlider *action_set_priority = nullptr;
	Button *add_action = nullptr;
	Button *rem_action_set = nullptr;
	VBoxContainer *actions_vb = nullptr;

	void _set_fold_icon();
	void _theme_changed();
	OpenXRActionEditor *_add_action_editor(const Ref<OpenXRAction> &p_action);

	void _on_toggle_expand();
	void _on_action_set_name_changed(const String &p_new_text);
	void _on_action_set_localized_name_changed(const String &p_new_text);
	void _on_action_set_priority_changed(const double p_new_value);
	void _on_add_action();
	void _on_remove_action_set();

	void _on_remove_action(Object *p_action_editor);
	void _on_rename_action(Object *p_action_editor);

protected:
	static void register_members();
	void _notification(int p_what);

	// used for undo/redo
	void _do_set_name(const String &p_new_text);
	void _do_set_localized_name(const String &p_new_text);
	void _do_set_priority(int64_t value);
	void _do_add_action_editor(OpenXRActionEditor *p_action_editor);
	void _do_remove_action_editor(OpenXRActionEditor *p_action_editor);

public:
	Ref<OpenXRActionSet> get_action_set() { return action_set; }
	void set_focus_on_entry();

	void remove_all_actions();

	OpenXRActionSetEditor(const Ref<OpenXRActionMap> &p_action_map, const Ref<OpenXRActionSet> &p_action_set);
};
