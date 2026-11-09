/**************************************************************************/
/*  action_map_editor.h                                                   */

#pragma once

#include "scene/ui/control.h"

class AcceptDialog;
class Button;
class CheckButton;
class EditorEventSearchBar;
class EventListenerLineEdit;
class HBoxContainer;
class InputEventConfigurationDialog;
class LineEdit;
class Tree;

class ActionMapEditor : public Control {
	ORBLIT_CLASS(ActionMapEditor, Control);

public:
	struct ActionInfo {
		String name;
		Dictionary action;
		bool has_initial = false;
		Dictionary action_initial;

		Ref<Texture2D> icon;
		bool editable = true;
	};

private:
	enum ItemButton {
		BUTTON_ADD_EVENT,
		BUTTON_EDIT_EVENT,
		BUTTON_REMOVE_ACTION,
		BUTTON_REMOVE_EVENT,
		BUTTON_REVERT_ACTION,
	};

	Vector<ActionInfo> actions_cache;
	Tree *action_tree = nullptr;

	// Storing which action/event is currently being edited in the InputEventConfigurationDialog.

	Dictionary current_action;
	String current_action_name;
	int current_action_event_index = -1;

	// Popups

	InputEventConfigurationDialog *event_config_dialog = nullptr;
	AcceptDialog *message = nullptr;

	// Filtering and Adding actions

	bool show_builtin_actions = false;
	CheckButton *show_builtin_actions_checkbutton = nullptr;
	EditorEventSearchBar *action_list_search_bar = nullptr;

	HBoxContainer *add_hbox = nullptr;
	LineEdit *add_edit = nullptr;
	Button *add_button = nullptr;

	void _event_config_confirmed();

	void _add_action_pressed();
	void _add_edit_text_changed(const String &p_name);
	String _check_new_action_name(const String &p_name);
	bool _has_action(const String &p_name) const;
	void _add_action(const String &p_name);
	void _action_edited();

	void _tree_button_pressed(Object *p_item, int p_column, int p_id, MouseButton p_button);
	void _tree_item_activated();
	void _on_search_bar_value_changed();
	bool _should_display_action(const String &p_name, const Array &p_events) const;

	Variant get_drag_data_fw(const Point2 &p_point, Control *p_from);
	bool can_drop_data_fw(const Point2 &p_point, const Variant &p_data, Control *p_from) const;
	void drop_data_fw(const Point2 &p_point, const Variant &p_data, Control *p_from);

	void _set_show_builtin_actions(bool p_show);

protected:
	void _notification(int p_what);
	static void register_members();

public:
	LineEdit *get_search_box() const;
	LineEdit *get_path_box() const;
	InputEventConfigurationDialog *get_configuration_dialog();

	// Dictionary represents an Action with "events" (Array) and "deadzone" (float) items. Pass with no param to update list from cached action map.
	void update_action_list(const Vector<ActionInfo> &p_action_infos = Vector<ActionInfo>());
	void show_message(const String &p_message);

	ActionMapEditor();
};
