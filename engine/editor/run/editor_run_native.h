/**************************************************************************/
/*  editor_run_native.h                                                   */

#pragma once

#include "scene/ui/box_container.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/menu_button.h"
#include "scene/ui/rich_text_label.h"

class EditorRunNative : public HBoxContainer {
	ORBLIT_CLASS(EditorRunNative, HBoxContainer);

	RichTextLabel *result_dialog_log = nullptr;
	AcceptDialog *result_dialog = nullptr;
	ConfirmationDialog *run_native_confirm = nullptr;
	bool run_confirmed = false;

	MenuButton *remote_debug = nullptr;
	bool first = true;

	int resume_id = -1;

	void _confirm_run_native();

protected:
	static void register_members();
	void _notification(int p_what);

public:
	Error start_run_native(int p_id);
	void resume_run_native();

	bool is_deploy_debug_remote_enabled() const;

	EditorRunNative();
};
