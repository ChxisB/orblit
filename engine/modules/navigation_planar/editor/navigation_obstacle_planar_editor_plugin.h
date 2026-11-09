/**************************************************************************/
/*  navigation_obstacle_planar_editor_plugin.h                                */

#pragma once

#include "editor/scene/planar/abstract_polygon_planar_editor.h"
#include "scene/planar/navigation/navigation_obstacle_planar.h"

class NavigationObstacle2DEditor : public AbstractPolygon2DEditor {
	ORBLIT_CLASS(NavigationObstacle2DEditor, AbstractPolygon2DEditor);

	NavigationObstacle2D *node = nullptr;

protected:
	virtual Entity2D *_get_node() const override;
	virtual void _set_node(Entity *p_polygon) override;

	virtual Variant _get_polygon(int p_idx) const override;
	virtual void _set_polygon(int p_idx, const Variant &p_polygon) const override;

	virtual void _action_add_polygon(const Variant &p_polygon) override;
	virtual void _action_remove_polygon(int p_idx) override;
	virtual void _action_set_polygon(int p_idx, const Variant &p_previous, const Variant &p_polygon) override;
};

class NavigationObstacle2DEditorPlugin : public AbstractPolygon2DEditorPlugin {
	ORBLIT_CLASS(NavigationObstacle2DEditorPlugin, AbstractPolygon2DEditorPlugin);

public:
	NavigationObstacle2DEditorPlugin();
};
