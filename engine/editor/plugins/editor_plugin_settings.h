/**************************************************************************/
/*  editor_plugin_settings.h                                              */

#pragma once

#include "editor/plugins/plugin_config_dialog.h"

class TextureRect;
class Tree;

class EditorPluginSettings : public VBoxContainer {
	ORBLIT_CLASS(EditorPluginSettings, VBoxContainer);

	enum {
		BUTTON_PLUGIN_EDIT
	};

	enum {
		COLUMN_STATUS,
		COLUMN_NAME,
		COLUMN_VERSION,
		COLUMN_AUTHOR,
		COLUMN_EDIT,
		COLUMN_MAX,
	};

	PluginConfigDialog *plugin_config_dialog = nullptr;
	TextureRect *recovery_mode_icon = nullptr;
	Tree *plugin_list = nullptr;
	bool updating = false;

	void _plugin_activity_changed();
	void _create_clicked();
	void _cell_button_pressed(Object *p_item, int p_column, int p_id, MouseButton p_button);

	static Vector<String> _get_plugins(const String &p_dir);

protected:
	void _notification(int p_what);

public:
	void update_plugins();

	EditorPluginSettings();
};
