/**************************************************************************/
/*  marker_spatial_gizmo_plugin.h                                              */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class Marker3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(Marker3DGizmoPlugin, EditorEntity3DGizmoPlugin);

	Ref<ArrayMesh> pos3d_mesh;

public:
	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;
	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	Marker3DGizmoPlugin();
};
