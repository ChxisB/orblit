/**************************************************************************/
/*  import_defaults_editor.h                                              */

#pragma once

#include "scene/ui/box_container.h"
#include "scene/ui/button.h"
#include "scene/ui/option_button.h"

class ImportDefaultsEditorSettings;
class EditorInspector;

class ImportDefaultsEditor : public VBoxContainer {
	ORBLIT_CLASS(ImportDefaultsEditor, VBoxContainer)

	OptionButton *importers = nullptr;
	Button *save_defaults = nullptr;
	Button *reset_defaults = nullptr;

	EditorInspector *inspector = nullptr;

	ImportDefaultsEditorSettings *settings = nullptr;

	void _update_importer();
	void _importer_selected(int p_index);

	void _reset();
	void _save();

protected:
	void _notification(int p_what);

public:
	void clear();

	ImportDefaultsEditor();
	~ImportDefaultsEditor();
};
