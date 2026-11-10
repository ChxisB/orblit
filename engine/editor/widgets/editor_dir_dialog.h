/**************************************************************************/
/*  editor_dir_dialog.h                                                   */

#pragma once

#include "scene/ui/dialogs.h"

class DirectoryCreateDialog;
class EditorFileSystemDirectory;
class Tree;
class TreeItem;

class EditorDirDialog : public ConfirmationDialog {
	ORBLIT_CLASS(EditorDirDialog, ConfirmationDialog);

	DirectoryCreateDialog *makedialog = nullptr;

	Button *makedir = nullptr;
	Button *copy = nullptr;
	HashSet<String> opened_paths;
	String new_dir_path;
	String base_directory_path;

	Tree *tree = nullptr;
	bool updating = false;

	void _item_collapsed(Object *p_item);
	void _item_activated();
	void _update_dir(const Color &p_default_folder_color, const Dictionary &p_assigned_folder_colors, const HashMap<String, Color> &p_folder_colors, bool p_is_dark_icon_and_font, TreeItem *p_item, EditorFileSystemDirectory *p_dir, const String &p_select_path = String());

	void _make_dir();
	void _make_dir_confirm(const String &p_path, const String &p_base_dir);

	void _copy_pressed();
	void ok_pressed() override;

	bool must_reload = false;

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void config(const Vector<String> &p_paths);
	void reload(const String &p_path = "");

	EditorDirDialog();
};
