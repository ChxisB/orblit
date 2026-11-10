/**************************************************************************/
/*  collision_polygon_planar_editor_plugin.cpp                                */

#include "collision_polygon_planar_editor_plugin.h"

Entity2D *CollisionPolygon2DEditor::_get_node() const {
	return node;
}

void CollisionPolygon2DEditor::_set_node(Entity *p_polygon) {
	node = Object::cast_to<CollisionPolygon2D>(p_polygon);
}

CollisionPolygon2DEditor::CollisionPolygon2DEditor() {
	set_edit_origin_and_center(true);
}

CollisionPolygon2DEditorPlugin::CollisionPolygon2DEditorPlugin() :
		AbstractPolygon2DEditorPlugin(memnew(CollisionPolygon2DEditor), "CollisionPolygon2D") {
}
