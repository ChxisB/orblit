/**************************************************************************/
/*  collision_polygon_planar_editor_plugin.h                                  */

#pragma once

#include "editor/scene/planar/abstract_polygon_planar_editor.h"
#include "scene/planar/physics/collision_polygon_planar.h"

class CollisionPolygon2DEditor : public AbstractPolygon2DEditor {
	ORBLIT_CLASS(CollisionPolygon2DEditor, AbstractPolygon2DEditor);

	CollisionPolygon2D *node = nullptr;

protected:
	virtual Entity2D *_get_node() const override;
	virtual void _set_node(Entity *p_polygon) override;

public:
	CollisionPolygon2DEditor();
};

class CollisionPolygon2DEditorPlugin : public AbstractPolygon2DEditorPlugin {
	ORBLIT_CLASS(CollisionPolygon2DEditorPlugin, AbstractPolygon2DEditorPlugin);

public:
	CollisionPolygon2DEditorPlugin();
};
