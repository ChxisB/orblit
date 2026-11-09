/**************************************************************************/
/*  plugin_config_dialog.h                                                */

#pragma once

#include "scene/ui/dialogs.h"
#include "scene/ui/line_edit.h"
#include "scene/ui/option_button.h"
#include "scene/ui/text_edit.h"

class ConfigFile;
class EditorValidationPanel;

class PluginConfigDialog : public ConfirmationDialog {
	ORBLIT_CLASS(PluginConfigDialog, ConfirmationDialog);

	enum {
		MSG_ID_PLUGIN,
		MSG_ID_SUBFOLDER,
		MSG_ID_SCRIPT,
	};

	LineEdit *name_edit = nullptr;
	LineEdit *subfolder_edit = nullptr;
	TextEdit *desc_edit = nullptr;
	LineEdit *author_edit = nullptr;
	LineEdit *version_edit = nullptr;
	OptionButton *script_option_edit = nullptr;
	LineEdit *script_edit = nullptr;

	LocalVector<Control *> plugin_edit_hidden_controls;

	EditorValidationPanel *validation_panel = nullptr;

	bool _edit_mode = false;

	void _clear_fields();
	void _on_confirmed();
	void _on_canceled();
	void _on_required_text_changed();
	void _create_script_for_plugin(const String &p_plugin_path, Ref<ConfigFile> p_config_file, int p_script_lang_index);
	String _get_subfolder();

	static String _to_absolute_plugin_path(const String &p_plugin_name);

protected:
	virtual void _notification(int p_what);
	static void register_members();

public:
	void config(const String &p_config_path);

	PluginConfigDialog();
};
