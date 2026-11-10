/**************************************************************************/
/*  directory_create_dialog.h                                             */

#pragma once

#include "scene/ui/dialogs.h"

class EditorValidationPanel;
class Label;
class LineEdit;

class DirectoryCreateDialog : public ConfirmationDialog {
	ORBLIT_CLASS(DirectoryCreateDialog, ConfirmationDialog);

public:
	enum Mode {
		MODE_FILE,
		MODE_DIRECTORY,
	};

private:
	String base_dir;
	Callable accept_callback;
	int mode = MODE_FILE;

	Label *base_path_label = nullptr;
	LineEdit *dir_path = nullptr;
	EditorValidationPanel *validation_panel = nullptr;

	String _sanitize_input(const String &p_input) const;
	String _validate_path(const String &p_path) const;
	void _on_dir_path_changed();

protected:
	virtual void ok_pressed() override;
	virtual void _post_popup() override;

public:
	void config(const String &p_base_dir, const Callable &p_accept_callback, int p_mode, const String &p_title, const String &p_default_name = "");

	DirectoryCreateDialog();
};
