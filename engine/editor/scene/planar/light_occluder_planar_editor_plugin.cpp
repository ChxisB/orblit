/**************************************************************************/
/*  light_occluder_planar_editor_plugin.cpp                                   */

#include "light_occluder_planar_editor_plugin.h"

#include "editor/editor_undo_redo_manager.h"

Ref<OccluderPolygon2D> LightOccluder2DEditor::_ensure_occluder() const {
	Ref<OccluderPolygon2D> occluder = node->get_occluder_polygon();
	if (occluder.is_null()) {
		occluder.instantiate();
		node->set_occluder_polygon(occluder);
	}
	return occluder;
}

Entity2D *LightOccluder2DEditor::_get_node() const {
	return node;
}

void LightOccluder2DEditor::_set_node(Entity *p_polygon) {
	node = Object::cast_to<LightOccluder2D>(p_polygon);
}

bool LightOccluder2DEditor::_is_line() const {
	Ref<OccluderPolygon2D> occluder = node->get_occluder_polygon();
	if (occluder.is_valid()) {
		return !occluder->is_closed();
	} else {
		return false;
	}
}

int LightOccluder2DEditor::_get_polygon_count() const {
	Ref<OccluderPolygon2D> occluder = node->get_occluder_polygon();
	if (occluder.is_valid()) {
		return occluder->get_polygon().size();
	} else {
		return 0;
	}
}

Variant LightOccluder2DEditor::_get_polygon(int p_idx) const {
	Ref<OccluderPolygon2D> occluder = node->get_occluder_polygon();
	if (occluder.is_valid()) {
		return occluder->get_polygon();
	} else {
		return Variant(Vector<Vector2>());
	}
}

void LightOccluder2DEditor::_set_polygon(int p_idx, const Variant &p_polygon) const {
	Ref<OccluderPolygon2D> occluder = _ensure_occluder();
	occluder->set_polygon(p_polygon);
}

void LightOccluder2DEditor::_action_set_polygon(int p_idx, const Variant &p_previous, const Variant &p_polygon) {
	Ref<OccluderPolygon2D> occluder = _ensure_occluder();
	EditorUndoRedoManager *undo_redo = EditorUndoRedoManager::get_singleton();
	undo_redo->add_do_method(occluder.ptr(), "set_polygon", p_polygon);
	undo_redo->add_undo_method(occluder.ptr(), "set_polygon", p_previous);
}

bool LightOccluder2DEditor::_has_resource() const {
	return node && node->get_occluder_polygon().is_valid();
}

void LightOccluder2DEditor::_create_resource() {
	if (!node) {
		return;
	}

	EditorUndoRedoManager *undo_redo = EditorUndoRedoManager::get_singleton();
	undo_redo->create_action(TTR("Create Occluder Polygon"));
	undo_redo->add_do_method(node, "set_occluder_polygon", Ref<OccluderPolygon2D>(memnew(OccluderPolygon2D)));
	undo_redo->add_undo_method(node, "set_occluder_polygon", Variant(Ref<RefCounted>()));
	undo_redo->commit_action();

	_menu_option(MODE_CREATE);
}

LightOccluder2DEditorPlugin::LightOccluder2DEditorPlugin() :
		AbstractPolygon2DEditorPlugin(memnew(LightOccluder2DEditor), "LightOccluder2D") {
}
