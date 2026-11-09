/**************************************************************************/
/*  group_settings_editor.h                                               */

#pragma once

#include "scene/ui/dialogs.h"

class CheckBox;
class EditorFileSystemDirectory;
class EditorValidationPanel;
class FileSystemDock;
class Label;
class Tree;

class GroupSettingsEditor : public VBoxContainer {
	ORBLIT_CLASS(GroupSettingsEditor, VBoxContainer);

	const String GLOBAL_GROUP_PREFIX = "global_group/";
	const StringName group_changed = "group_changed";

	HashMap<StringName, String> groups_cache;

	bool updating_groups = false;

	AcceptDialog *message = nullptr;
	Tree *tree = nullptr;
	LineEdit *group_name = nullptr;
	LineEdit *group_description = nullptr;
	Button *add_button = nullptr;

	ConfirmationDialog *remove_dialog = nullptr;
	CheckBox *remove_check_box = nullptr;
	Label *remove_label = nullptr;

	ConfirmationDialog *rename_group_dialog = nullptr;
	LineEdit *rename_group = nullptr;
	CheckBox *rename_check_box = nullptr;
	EditorValidationPanel *rename_validation_panel = nullptr;

	void _show_remove_dialog();
	void _show_rename_dialog();

	String _check_new_group_name(const String &p_name);
	void _check_rename();

	void _add_group();
	void _add_group(const String &p_name, const String &p_description);

	void _modify_references(const StringName &p_name, const StringName &p_new_name, bool p_is_rename);

	void _confirm_rename();
	void _confirm_delete();

	void _text_submitted(const String &p_text);
	void _group_name_text_changed(const String &p_name);

	void _item_edited();
	void _item_button_pressed(Object *p_item, int p_column, int p_id, MouseButton p_button);

protected:
	void _notification(int p_what);
	static void register_members();

public:
	LineEdit *get_name_box() const;
	void show_message(const String &p_message);

	void remove_references(const StringName &p_name);
	void rename_references(const StringName &p_old_name, const StringName &p_new_name);

	bool remove_node_references(Entity *p_node, const StringName &p_name);
	bool rename_node_references(Entity *p_node, const StringName &p_old_name, const StringName &p_new_name);

	void update_groups();
	void connect_filesystem_dock_signals(FileSystemDock *p_fs_dock);

	GroupSettingsEditor();
};
