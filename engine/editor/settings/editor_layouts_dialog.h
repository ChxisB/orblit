/**************************************************************************/
/*  editor_layouts_dialog.h                                               */

#pragma once

#include "scene/ui/dialogs.h"

class EditorValidationPanel;
class LineEdit;
class ItemList;

class EditorLayoutsDialog : public ConfirmationDialog {
	ORBLIT_CLASS(EditorLayoutsDialog, ConfirmationDialog);

	bool save_mode = true;

	LineEdit *name = nullptr;
	EditorValidationPanel *validation = nullptr;
	ItemList *layout_names = nullptr;
	VBoxContainer *makevb = nullptr;

	void _deselect_layout_names();
	void _validate_name();
	void _multi_selected();
	void _item_activated();

protected:
	static void register_members();
	virtual void ok_pressed() override;
	virtual void _post_popup() override;

public:
	EditorLayoutsDialog();

	void set_save_mode_enabled(bool p_enabled);
};
