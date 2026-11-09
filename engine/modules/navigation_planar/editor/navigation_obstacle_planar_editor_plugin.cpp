/**************************************************************************/
/*  navigation_obstacle_planar_editor_plugin.cpp                              */

#include "navigation_obstacle_planar_editor_plugin.h"

#include "editor/editor_undo_redo_manager.h"

Entity2D *NavigationObstacle2DEditor::_get_node() const {
	return node;
}

void NavigationObstacle2DEditor::_set_node(Entity *p_polygon) {
	node = Object::cast_to<NavigationObstacle2D>(p_polygon);
}

Variant NavigationObstacle2DEditor::_get_polygon(int p_idx) const {
	return node->get_vertices();
}

void NavigationObstacle2DEditor::_set_polygon(int p_idx, const Variant &p_polygon) const {
	node->set_vertices(p_polygon);
}

void NavigationObstacle2DEditor::_action_add_polygon(const Variant &p_polygon) {
	EditorUndoRedoManager *undo_redo = EditorUndoRedoManager::get_singleton();
	undo_redo->add_do_method(node, "set_vertices", p_polygon);
	undo_redo->add_undo_method(node, "set_vertices", node->get_vertices());
}

void NavigationObstacle2DEditor::_action_remove_polygon(int p_idx) {
	EditorUndoRedoManager *undo_redo = EditorUndoRedoManager::get_singleton();
	undo_redo->add_do_method(node, "set_vertices", Variant(Vector<Vector2>()));
	undo_redo->add_undo_method(node, "set_vertices", node->get_vertices());
}

void NavigationObstacle2DEditor::_action_set_polygon(int p_idx, const Variant &p_previous, const Variant &p_polygon) {
	EditorUndoRedoManager *undo_redo = EditorUndoRedoManager::get_singleton();
	undo_redo->add_do_method(node, "set_vertices", p_polygon);
	undo_redo->add_undo_method(node, "set_vertices", node->get_vertices());
}

NavigationObstacle2DEditorPlugin::NavigationObstacle2DEditorPlugin() :
		AbstractPolygon2DEditorPlugin(memnew(NavigationObstacle2DEditor), "NavigationObstacle2D") {
}
