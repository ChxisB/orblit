/**************************************************************************/
/*  geometry_instance_spatial_gizmo_plugin.h                                   */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class GeometryInstance3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(GeometryInstance3DGizmoPlugin, EditorEntity3DGizmoPlugin);

public:
	virtual bool has_gizmo(Entity3D *p_spatial) override;
	virtual String get_gizmo_name() const override;
	virtual int get_priority() const override;

	virtual void redraw(EditorEntity3DGizmo *p_gizmo) override;
};
