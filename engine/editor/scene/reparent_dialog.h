/**************************************************************************/
/*  reparent_dialog.h                                                     */

#pragma once

#include "scene/ui/dialogs.h"

class CheckBox;
class StageEditor;

class ReparentDialog : public ConfirmationDialog {
	ORBLIT_CLASS(ReparentDialog, ConfirmationDialog);

	StageEditor *tree = nullptr;
	CheckBox *keep_transform = nullptr;

	void _reparent();
	void _cancel();

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void set_current(const HashSet<Entity *> &p_selection);

	ReparentDialog();
};
