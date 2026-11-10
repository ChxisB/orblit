/**************************************************************************/
/*  fbx_importer_manager.h                                                */

#pragma once

#include "editor/widgets/editor_file_dialog.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/line_edit.h"

class FBXImporterManager : public ConfirmationDialog {
	ORBLIT_CLASS(FBXImporterManager, ConfirmationDialog)

	bool is_importing = false;

	LineEdit *fbx_path = nullptr;
	Button *fbx_path_browse = nullptr;
	EditorFileDialog *browse_dialog = nullptr;
	Label *path_status = nullptr;

	void _validate_path(const String &p_path);
	void _select_file(const String &p_path);
	void _path_confirmed();
	void _cancel_setup();
	void _browse_install();
	void _link_clicked();

	static FBXImporterManager *singleton;

protected:
	void _notification(int p_what);

public:
	static FBXImporterManager *get_singleton() { return singleton; }

	void show_dialog(bool p_exclusive = false);

	FBXImporterManager();
};
