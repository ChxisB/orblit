/**************************************************************************/
/*  editor_script_plugin.h                                                */

#pragma once

#include "editor/plugins/editor_plugin.h"

class EditorScriptPlugin : public EditorPlugin {
	ORBLIT_CLASS(EditorScriptPlugin, EditorPlugin);

private:
	List<StringName> commands;

	void run_command(const StringName &p_name);
	void command_palette_about_to_popup();

public:
	EditorScriptPlugin();

	virtual String get_plugin_name() const override { return "EditorScript"; }
};
