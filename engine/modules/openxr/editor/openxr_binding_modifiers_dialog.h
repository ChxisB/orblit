/**************************************************************************/
/*  openxr_binding_modifiers_dialog.h                                     */

#pragma once

#include "../action_map/openxr_action_map.h"
#include "../action_map/openxr_interaction_profile.h"
#include "openxr_binding_modifier_editor.h"

#include "editor/editor_undo_redo_manager.h"
#include "editor/widgets/create_dialog.h"
#include "scene/ui/box_container.h"
#include "scene/ui/button.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/label.h"
#include "scene/ui/scroll_container.h"

class OpenXRBindingModifiersDialog : public AcceptDialog {
	ORBLIT_CLASS(OpenXRBindingModifiersDialog, AcceptDialog);

private:
	ScrollContainer *binding_modifier_sc = nullptr;
	VBoxContainer *binding_modifiers_vb = nullptr;
	Label *binding_warning_label = nullptr;
	Button *add_binding_modifier_btn = nullptr;
	CreateDialog *create_dialog = nullptr;

	OpenXRBindingModifierEditor *_add_binding_modifier_editor(const Ref<OpenXRBindingModifier> &p_binding_modifier);
	void _create_binding_modifiers();

	void _on_add_binding_modifier();
	void _on_remove_binding_modifier(Object *p_binding_modifier_editor);
	void _on_dialog_created();

protected:
	EditorUndoRedoManager *undo_redo;
	Ref<OpenXRActionMap> action_map;
	Ref<OpenXRInteractionProfile> interaction_profile;
	Ref<OpenXRIPBinding> ip_binding;

	static void register_members();
	void _notification(int p_what);

	// used for undo/redo
	void _do_add_binding_modifier_editor(OpenXRBindingModifierEditor *p_binding_modifier_editor);
	void _do_remove_binding_modifier_editor(OpenXRBindingModifierEditor *p_binding_modifier_editor);

public:
	OpenXRBindingModifiersDialog();

	void setup(const Ref<OpenXRActionMap> &p_action_map, const Ref<OpenXRInteractionProfile> &p_interaction_profile, const Ref<OpenXRIPBinding> &p_ip_binding = Ref<OpenXRIPBinding>());
};
