/**************************************************************************/
/*  shader_create_dialog.h                                                */

#pragma once

#include "scene/ui/check_box.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/grid_container.h"
#include "scene/ui/line_edit.h"
#include "scene/ui/option_button.h"

class EditorFileDialog;
class EditorValidationPanel;

class ShaderCreateDialog : public ConfirmationDialog {
	ORBLIT_CLASS(ShaderCreateDialog, ConfirmationDialog);

	enum {
		MSG_ID_SHADER,
		MSG_ID_PATH,
		MSG_ID_BUILT_IN,
	};

	struct ShaderTypeData {
		List<String> extensions;
		String default_extension;
		bool use_templates = false;
	};

	List<ShaderTypeData> type_data;

	GridContainer *gc = nullptr;
	EditorValidationPanel *validation_panel = nullptr;
	OptionButton *type_menu = nullptr;
	OptionButton *mode_menu = nullptr;
	OptionButton *template_menu = nullptr;
	CheckBox *internal = nullptr;
	LineEdit *file_path = nullptr;
	Button *path_button = nullptr;
	EditorFileDialog *file_browse = nullptr;
	AcceptDialog *alert = nullptr;

	String initial_base_path;
	String path_error;
	bool is_new_shader_created = true;
	bool is_path_valid = false;
	bool is_built_in = false;
	bool built_in_enabled = true;
	bool load_enabled = false;
	bool re_check_path = false;
	int current_type = -1;
	int current_mode = 0;
	int current_template = 0;
	bool missing_base_dir = false;

	virtual void _update_language_info();

	void _path_hbox_sorted();
	void _path_changed(const String &p_path = String());
	void _path_submitted(const String &p_path = String());
	void _type_changed(int p_type = 0);
	void _built_in_toggled(bool p_enabled);
	void _template_changed(int p_template = 0);
	void _mode_changed(int p_mode = 0);
	void _browse_path();
	void _file_selected(const String &p_file);
	void _refresh_type_icons();
	String _validate_path(const String &p_path);
	virtual void ok_pressed() override;
	void _create_new();
	void _load_exist();
	void _update_dialog();

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void config(const String &p_base_path, bool p_built_in_enabled = true, bool p_load_enabled = true, const String &p_preferred_type = "", int p_preferred_mode = -1);
	ShaderCreateDialog();
};
