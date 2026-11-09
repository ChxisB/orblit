/**************************************************************************/
/*  openxr_select_action_dialog.h                                         */

#pragma once

#include "../action_map/openxr_action_map.h"

#include "scene/ui/dialogs.h"

class VBoxContainer;
class ScrollContainer;

class OpenXRSelectActionDialog : public ConfirmationDialog {
	ORBLIT_CLASS(OpenXRSelectActionDialog, ConfirmationDialog);

private:
	Ref<OpenXRActionMap> action_map;
	String selected_action;
	Dictionary action_buttons;

	VBoxContainer *main_vb = nullptr;
	ScrollContainer *scroll = nullptr;

protected:
	static void register_members();
	void _notification(int p_what);

public:
	void _on_select_action(const String &p_action);
	void open();
	virtual void ok_pressed() override;

	OpenXRSelectActionDialog(const Ref<OpenXRActionMap> &p_action_map);
};
