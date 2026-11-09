/**************************************************************************/
/*  editor_file_dialog.h                                                  */

#pragma once

#include "scene/ui/file_dialog.h"

class DependencyRemoveDialog;

class EditorFileDialog : public FileDialog {
	ORBLIT_CLASS(EditorFileDialog, FileDialog);

	DependencyRemoveDialog *dependency_remove_dialog = nullptr;

protected:
	virtual void _item_menu_id_pressed(int p_option) override;
	virtual void _dir_contents_changed() override;

	virtual bool _should_use_native_popup() const override;
	virtual bool _should_hide_file(const String &p_file) const override;
	virtual Color _get_folder_color(const String &p_path) const override;
	virtual Vector2i _get_list_mode_icon_size() const override;

	static void register_members();
	void _validate_property(PropertyInfo &p_property) const;
	void _notification(int p_what);

public:
};
