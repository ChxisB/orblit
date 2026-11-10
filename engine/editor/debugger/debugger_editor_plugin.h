/**************************************************************************/
/*  debugger_editor_plugin.h                                              */

#pragma once

#include "editor/plugins/editor_plugin.h"

class EditorFileServer;
class MenuButton;
class PopupMenu;
class RunInstancesDialog;

class DebuggerEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(DebuggerEditorPlugin, EditorPlugin);

private:
	PopupMenu *debug_menu = nullptr;
	EditorFileServer *file_server = nullptr;
	RunInstancesDialog *run_instances_dialog = nullptr;

	enum MenuOptions {
		RUN_FILE_SERVER,
		RUN_LIVE_DEBUG,
		RUN_DEBUG_COLLISIONS,
		RUN_DEBUG_PATHS,
		RUN_DEBUG_NAVIGATION,
		RUN_DEBUG_AVOIDANCE,
		RUN_DEBUG_CANVAS_REDRAW,
		RUN_DEPLOY_REMOTE_DEBUG,
		RUN_RELOAD_SCRIPTS,
		SERVER_KEEP_OPEN,
		RUN_MULTIPLE_INSTANCES,
	};

	bool initializing = true;

	void _update_debug_options();
	void _notification(int p_what);
	void _menu_option(int p_option);

public:
	virtual String get_plugin_name() const override { return "Debugger"; }

	DebuggerEditorPlugin(PopupMenu *p_menu);
	~DebuggerEditorPlugin();
};
