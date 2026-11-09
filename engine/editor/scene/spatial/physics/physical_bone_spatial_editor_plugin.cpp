/**************************************************************************/
/*  physical_bone_spatial_editor_plugin.cpp                                    */

#include "physical_bone_spatial_editor_plugin.h"

#include "core/object/callable_mp.h"
#include "editor/editor_node.h"
#include "editor/editor_string_names.h"
#include "editor/scene/spatial/entity_spatial_editor_plugin.h"
#include "scene/spatial/physics/physical_bone_spatial.h"

void PhysicalBone3DEditor::_on_toggle_button_transform_joint(bool p_is_pressed) {
	_set_move_joint();
}

void PhysicalBone3DEditor::_set_move_joint() {
	if (selected) {
		selected->_set_gizmo_move_joint(button_transform_joint->is_pressed());
		Entity3DEditor::get_singleton()->update_transform_gizmo();
	}
}

PhysicalBone3DEditor::PhysicalBone3DEditor() {
	spatial_editor_hb = memnew(HBoxContainer);
	spatial_editor_hb->set_h_size_flags(Control::SIZE_EXPAND_FILL);
	spatial_editor_hb->set_alignment(BoxContainer::ALIGNMENT_BEGIN);
	Entity3DEditor::get_singleton()->add_control_to_menu_panel(spatial_editor_hb);

	button_transform_joint = memnew(Button);
	button_transform_joint->set_theme_type_variation(SceneStringName(FlatButton));
	spatial_editor_hb->add_child(button_transform_joint);

	button_transform_joint->set_text(TTR("Move Joint"));
	// TODO: Rework this as a dedicated toolbar control so we can hook into theme changes and update it
	// when the editor theme updates.
	button_transform_joint->set_button_icon(EditorNode::get_singleton()->get_editor_theme()->get_icon(SNAME("PhysicalBone3D"), EditorStringName(EditorIcons)));
	button_transform_joint->set_toggle_mode(true);
	button_transform_joint->connect(SceneStringName(toggled), callable_mp(this, &PhysicalBone3DEditor::_on_toggle_button_transform_joint));

	hide();
}

void PhysicalBone3DEditor::set_selected(PhysicalBone3D *p_pb) {
	button_transform_joint->set_pressed(false);

	_set_move_joint();
	selected = p_pb;
	_set_move_joint();
}

void PhysicalBone3DEditor::hide() {
	spatial_editor_hb->hide();
}

void PhysicalBone3DEditor::show() {
	spatial_editor_hb->show();
}

void PhysicalBone3DEditorPlugin::make_visible(bool p_visible) {
	if (p_visible) {
		physical_bone_editor.show();
	} else {
		physical_bone_editor.hide();
		physical_bone_editor.set_selected(nullptr);
		selected = nullptr;
	}
}

void PhysicalBone3DEditorPlugin::edit(Object *p_node) {
	PhysicalBone3D *bone = Object::cast_to<PhysicalBone3D>(p_node);
	if (bone) {
		selected = bone;
		physical_bone_editor.set_selected(selected);
	}
}
