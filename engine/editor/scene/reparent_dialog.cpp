/**************************************************************************/
/*  reparent_dialog.cpp                                                   */

#include "reparent_dialog.h"

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "editor/scene/stage_editor.h"
#include "scene/ui/box_container.h"
#include "scene/ui/check_box.h"

void ReparentDialog::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			connect(SceneStringName(confirmed), callable_mp(this, &ReparentDialog::_reparent));
		} break;

		case NOTIFICATION_EXIT_STAGE: {
			disconnect(SceneStringName(confirmed), callable_mp(this, &ReparentDialog::_reparent));
		} break;
	}
}

void ReparentDialog::_cancel() {
	hide();
}

void ReparentDialog::_reparent() {
	if (tree->get_selected()) {
		emit_signal(SNAME("reparent_requested"), tree->get_selected()->get_path(), keep_transform->is_pressed());
		hide();
	}
}

void ReparentDialog::set_current(const HashSet<Entity *> &p_selection) {
	tree->set_marked(p_selection, false, false);
	tree->set_selected(nullptr);
}

void ReparentDialog::register_members() {
	ClassRegistry::bind_method("_cancel", &ReparentDialog::_cancel);

	ADD_SIGNAL(MethodInfo("reparent_requested", PropertyInfo(Variant::ENTITY_PATH, "path"), PropertyInfo(Variant::BOOL, "keep_global_xform")));
}

ReparentDialog::ReparentDialog() {
	set_title(TTR("Reparent Node"));

	VBoxContainer *vbc = memnew(VBoxContainer);
	add_child(vbc);

	tree = memnew(StageEditor(false));
	tree->set_update_when_invisible(false);
	tree->set_show_enabled_subscene(true);
	tree->get_scene_tree()->connect("item_activated", callable_mp(this, &ReparentDialog::_reparent));
	vbc->add_margin_child(TTR("Select new parent:"), tree, true);

	keep_transform = memnew(CheckBox);
	keep_transform->set_text(TTR("Keep Global Transform"));
	keep_transform->set_pressed(true);
	vbc->add_child(keep_transform);

	set_ok_button_text(TTR("Reparent"));
}
