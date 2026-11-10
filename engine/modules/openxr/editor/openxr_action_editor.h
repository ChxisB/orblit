/**************************************************************************/
/*  openxr_action_editor.h                                                */

#pragma once

#include "../action_map/openxr_action.h"

#include "editor/editor_undo_redo_manager.h"
#include "scene/ui/box_container.h"
#include "scene/ui/button.h"
#include "scene/ui/line_edit.h"
#include "scene/ui/option_button.h"

class OpenXRActionEditor : public HBoxContainer {
	ORBLIT_CLASS(OpenXRActionEditor, HBoxContainer);

private:
	EditorUndoRedoManager *undo_redo;
	Ref<OpenXRAction> action;
	bool action_name_dirty = false;

	LineEdit *action_name = nullptr;
	LineEdit *action_localized_name = nullptr;
	OptionButton *action_type_button = nullptr;
	Button *rem_action = nullptr;

	void _theme_changed();
	void _on_action_name_changed(const String &p_new_text);
	void _on_action_localized_name_changed(const String &p_new_text);
	void _on_item_selected(int p_idx);
	void _on_remove_action();
	void _on_focus_exit();

protected:
	static void register_members();
	void _notification(int p_what);

	// used for undo/redo
	void _do_set_name(const String &p_new_text);
	void _do_set_localized_name(const String &p_new_text);
	void _do_set_action_type(OpenXRAction::ActionType p_action_type);

public:
	Ref<OpenXRAction> get_action() { return action; }
	OpenXRActionEditor(const Ref<OpenXRAction> &p_action);
};
