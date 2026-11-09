/**************************************************************************/
/*  project_upgrade_tool.h                                                */

#pragma once

#include "core/object/object.h"

class ConfirmationDialog;
class EditorFileSystemDirectory;

class ProjectUpgradeTool : public Object {
	ORBLIT_CLASS(ProjectUpgradeTool, Object);

	ConfirmationDialog *upgrade_dialog = nullptr;

	void _add_files(EditorFileSystemDirectory *p_dir, Vector<String> &r_reimport_paths, Vector<String> &r_resave_scenes, Vector<String> &r_resave_resources);

	const String META_REIMPORT_PATHS = "reimport_paths";
	const String META_RESAVE_SCENES = "resave_scenes";
	const String META_RESAVE_RESOURCES = "resave_resources";

public:
	const String META_PROJECT_UPGRADE_TOOL = "project_upgrade_tool";
	const String META_RUN_ON_RESTART = "run_on_restart";
	const StringName UPGRADE_FINISHED = "upgrade_finished";

protected:
	static void register_members();

public:
	void popup_dialog();
	void prepare_upgrade();
	void begin_upgrade();
	void finish_upgrade();
};
