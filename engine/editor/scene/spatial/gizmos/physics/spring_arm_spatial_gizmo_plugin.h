/**************************************************************************/
/*  spring_arm_spatial_gizmo_plugin.h                                          */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class SpringArm3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(SpringArm3DGizmoPlugin, EditorEntity3DGizmoPlugin);

public:
	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;
	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	SpringArm3DGizmoPlugin();
};
