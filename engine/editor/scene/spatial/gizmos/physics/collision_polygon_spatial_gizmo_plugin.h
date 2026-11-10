/**************************************************************************/
/*  collision_polygon_spatial_gizmo_plugin.h                                   */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class CollisionPolygon3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(CollisionPolygon3DGizmoPlugin, EditorEntity3DGizmoPlugin);

	void create_collision_material(const String &p_name, float p_alpha);

public:
	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;
	void redraw(EditorEntity3DGizmo *p_gizmo) override;
	CollisionPolygon3DGizmoPlugin();
};
