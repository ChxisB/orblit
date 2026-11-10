/**************************************************************************/
/*  openxr_interaction_profile_editor.h                                   */

#pragma once

#include "../action_map/openxr_action_map.h"
#include "../action_map/openxr_interaction_profile.h"
#include "../action_map/openxr_interaction_profile_metadata.h"
#include "openxr_binding_modifiers_dialog.h"
#include "openxr_select_action_dialog.h"

#include "editor/editor_undo_redo_manager.h"
#include "scene/ui/box_container.h"
#include "scene/ui/button.h"

class OpenXRInteractionProfileEditorBase : public HBoxContainer {
	ORBLIT_CLASS(OpenXRInteractionProfileEditorBase, HBoxContainer);

private:
	OpenXRBindingModifiersDialog *binding_modifiers_dialog = nullptr;
	VBoxContainer *toolbar_vb = nullptr;
	Button *binding_modifiers_btn = nullptr;

	void _on_open_binding_modifiers();

protected:
	EditorUndoRedoManager *undo_redo;
	Ref<OpenXRInteractionProfile> interaction_profile;
	Ref<OpenXRActionMap> action_map;

	ScrollContainer *interaction_profile_sc = nullptr;

	bool is_dirty = false;

	static void register_members();
	void _notification(int p_what);

	const OpenXRInteractionProfileMetadata::InteractionProfile *profile_def = nullptr;

public:
	String tooltip; // Tooltip text to show on tab

	Ref<OpenXRInteractionProfile> get_interaction_profile() { return interaction_profile; }

	virtual void _update_interaction_profile();
	virtual void _theme_changed();

	void _do_update_interaction_profile();
	void _set_dirty();
	void _add_binding(const String &p_action, const String &p_path);
	void _remove_binding(const String &p_action, const String &p_path);

	void remove_all_for_action_set(const Ref<OpenXRActionSet> &p_action_set);
	void remove_all_for_action(const Ref<OpenXRAction> &p_action);

	virtual void setup(const Ref<OpenXRActionMap> &p_action_map, const Ref<OpenXRInteractionProfile> &p_interaction_profile);

	OpenXRInteractionProfileEditorBase();
};

class OpenXRInteractionProfileEditor : public OpenXRInteractionProfileEditorBase {
	ORBLIT_CLASS(OpenXRInteractionProfileEditor, OpenXRInteractionProfileEditorBase);

private:
	String selecting_for_io_path;
	HBoxContainer *interaction_profile_hb = nullptr;

	OpenXRSelectActionDialog *select_action_dialog = nullptr;

	void _add_io_path(VBoxContainer *p_container, const OpenXRInteractionProfileMetadata::IOPath *p_io_path);

public:
	void select_action_for(const String &p_io_path);
	void _on_action_selected(const String &p_action);
	void _on_remove_pressed(const String &p_action, const String &p_for_io_path);

	virtual void _update_interaction_profile() override;
	virtual void _theme_changed() override;
	virtual void setup(const Ref<OpenXRActionMap> &p_action_map, const Ref<OpenXRInteractionProfile> &p_interaction_profile) override;

	OpenXRInteractionProfileEditor();
};
