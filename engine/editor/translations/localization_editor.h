/**************************************************************************/
/*  localization_editor.h                                                 */

#pragma once

#include "editor/translations/editor_locale_dialog.h"
#include "scene/ui/check_box.h"
#include "scene/ui/tree.h"

class EditorFileDialog;
class EditorQuickOpenDialog;
class FileSystemDock;

class LocalizationEditor : public VBoxContainer {
	ORBLIT_CLASS(LocalizationEditor, VBoxContainer);

	Tree *translation_list = nullptr;

	EditorLocaleDialog *locale_select = nullptr;
	EditorFileDialog *translation_file_open = nullptr;

	Button *translation_res_option_add_button = nullptr;
	EditorFileDialog *translation_res_file_open_dialog = nullptr;
	EditorFileDialog *translation_res_option_file_open_dialog = nullptr;
	Tree *translation_remap = nullptr;
	Tree *translation_remap_options = nullptr;

	Tree *template_source_list = nullptr;
	CheckBox *template_add_builtin = nullptr;
	CheckBox *template_add_title = nullptr;
	EditorFileDialog *template_source_open_dialog = nullptr;
	EditorQuickOpenDialog *template_source_quick_open_dialog = nullptr;
	EditorFileDialog *template_generate_dialog = nullptr;
	Button *template_generate_button = nullptr;

	bool updating_translations = false;
	StringName localization_changed;

	LocalVector<Tree *> trees;
	HashMap<Tree *, String> tree_data_types;
	HashMap<Tree *, StringName> tree_settings;

	void _translation_file_open();
	void _translation_add(const PackedStringArray &p_paths);
	void _translation_delete(Object *p_item, int p_column, int p_button, MouseButton p_mouse_button);

	void _translation_res_file_open();
	void _translation_res_add(const PackedStringArray &p_paths);
	void _translation_res_delete(Object *p_item, int p_column, int p_button, MouseButton p_mouse_button);
	void _translation_res_select();
	void _translation_res_option_file_open();
	void _translation_res_option_add(const PackedStringArray &p_paths);
	void _translation_res_option_changed();
	void _translation_res_option_delete(Object *p_item, int p_column, int p_button, MouseButton p_mouse_button);
	void _translation_res_option_popup(bool p_arrow_clicked);
	void _translation_res_option_selected(const String &p_locale);

	void _template_source_add(const PackedStringArray &p_paths);
	void _template_source_add_one(const String &p_path);
	void _template_source_delete(Object *p_item, int p_column, int p_button, MouseButton p_mouse_button);
	void _template_source_file_open();
	void _template_generate_open();
	void _template_generate_quick_open();
	void _template_generate_command();
	void _template_add_builtin_toggled();
	void _template_add_title_toggled();
	void _template_generate(const String &p_file);
	void _update_template_source_file_extensions();

	void _filesystem_files_moved(const String &p_old_file, const String &p_new_file);
	void _filesystem_file_removed(const String &p_file);

	Variant get_drag_data_fw(const Point2 &p_point, Control *p_from);
	bool can_drop_data_fw(const Point2 &p_point, const Variant &p_data, Control *p_from) const;
	void drop_data_fw(const Point2 &p_point, const Variant &p_data, Control *p_from);

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void add_translation(const String &p_translation);
	void update_translations();
	void connect_filesystem_dock_signals(FileSystemDock *p_fs_dock);

	LocalizationEditor();
};
