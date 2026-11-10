/**************************************************************************/
/*  editor_asset_installer.h                                              */

#pragma once

#include "scene/ui/dialogs.h"
#include "scene/ui/tree.h"

class CheckBox;
class EditorFileDialog;
class Label;
class LinkButton;

class EditorAssetInstaller : public ConfirmationDialog {
	ORBLIT_CLASS(EditorAssetInstaller, ConfirmationDialog);

	VBoxContainer *source_tree_vb = nullptr;
	Tree *source_tree = nullptr;
	MarginContainer *destination_tree_mc = nullptr;
	Tree *destination_tree = nullptr;
	Label *asset_title_label = nullptr;
	Label *asset_conflicts_label = nullptr;
	LinkButton *asset_conflicts_link = nullptr;

	Button *show_source_files_button = nullptr;
	CheckBox *skip_toplevel_check = nullptr;
	EditorFileDialog *target_dir_dialog = nullptr;

	String package_path;
	String asset_name;
	HashSet<String> asset_files;
	HashMap<String, String> mapped_files;
	HashMap<String, TreeItem *> file_item_map;

	TreeItem *first_file_conflict = nullptr;

	Ref<Texture2D> generic_extension_icon;
	HashMap<String, Ref<Texture2D>> extension_icon_map;

	bool updating_source = false;
	String toplevel_prefix;
	bool skip_toplevel = false;
	String target_dir_path = "res://";

	void _check_has_toplevel();
	void _set_skip_toplevel(bool p_checked);

	void _open_target_dir_dialog();
	void _target_dir_selected(const String &p_target_path);

	void _update_file_mappings();
	void _rebuild_source_tree();
	void _update_source_tree();
	bool _update_source_item_status(TreeItem *p_item, const String &p_path);
	void _rebuild_destination_tree();
	TreeItem *_create_dir_item(Tree *p_tree, TreeItem *p_parent, const String &p_path, HashMap<String, TreeItem *> &p_item_map);
	TreeItem *_create_file_item(Tree *p_tree, TreeItem *p_parent, const String &p_path, int *r_conflicts);

	void _update_conflict_status(int p_conflicts);
	void _update_confirm_button();
	void _toggle_source_tree(bool p_visible, bool p_scroll_to_error = false);

	void _item_checked_cbk();
	bool _fix_conflicted_indeterminate_state(TreeItem *p_item, int p_column);
	bool _is_item_checked(const String &p_source_path) const;

	void _install_asset();
	virtual void ok_pressed() override;

protected:
	void _notification(int p_what);

public:
	void open_asset(const String &p_path, bool p_autoskip_toplevel = false);

	void set_asset_name(const String &p_asset_name);
	String get_asset_name() const;

	EditorAssetInstaller();
};
