/**************************************************************************/
/*  openxr_select_interaction_profile_dialog.h                            */

#pragma once

#include "scene/ui/box_container.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/label.h"
#include "scene/ui/scroll_container.h"

class OpenXRSelectInteractionProfileDialog : public ConfirmationDialog {
	ORBLIT_CLASS(OpenXRSelectInteractionProfileDialog, ConfirmationDialog);

private:
	String selected_interaction_profile;
	Dictionary ip_buttons;

	VBoxContainer *main_vb = nullptr;
	ScrollContainer *scroll = nullptr;
	Label *all_selected = nullptr;

protected:
	static void register_members();
	void _notification(int p_what);

public:
	void _on_select_interaction_profile(const String &p_interaction_profile);
	void open(const PackedStringArray &p_do_not_include);
	virtual void ok_pressed() override;

	OpenXRSelectInteractionProfileDialog();
};
