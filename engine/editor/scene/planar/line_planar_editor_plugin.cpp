/**************************************************************************/
/*  line_planar_editor_plugin.cpp                                             */

#include "line_planar_editor_plugin.h"

#include "core/object/callable_mp.h"
#include "editor/editor_undo_redo_manager.h"
#include "editor/scene/canvas_item_editor_plugin.h"

Entity2D *Line2DEditor::_get_node() const {
	return node;
}

void Line2DEditor::_set_node(Entity *p_line) {
	CanvasItem *ci_editor_control = CanvasItemEditor::get_singleton()->get_viewport_control();

	if (node) {
		node->disconnect(SceneStringName(draw), callable_mp(ci_editor_control, &CanvasItem::queue_redraw));
	}

	node = Object::cast_to<Line2D>(p_line);
	if (node) {
		// Update the canvas overlay.
		node->connect(SceneStringName(draw), callable_mp(ci_editor_control, &CanvasItem::queue_redraw));
	}
}

bool Line2DEditor::_is_line() const {
	return true;
}

Variant Line2DEditor::_get_polygon(int p_idx) const {
	return _get_node()->get("points");
}

void Line2DEditor::_set_polygon(int p_idx, const Variant &p_polygon) const {
	_get_node()->set("points", p_polygon);
}

void Line2DEditor::_action_set_polygon(int p_idx, const Variant &p_previous, const Variant &p_polygon) {
	Entity2D *_node = _get_node();
	EditorUndoRedoManager *undo_redo = EditorUndoRedoManager::get_singleton();
	undo_redo->add_do_method(_node, "set_points", p_polygon);
	undo_redo->add_undo_method(_node, "set_points", p_previous);
}

Line2DEditorPlugin::Line2DEditorPlugin() :
		AbstractPolygon2DEditorPlugin(memnew(Line2DEditor), "Line2D") {
}
