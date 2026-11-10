/**************************************************************************/
/*  lightmap_probe_gizmo_plugin.h                                         */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class LightmapProbeGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(LightmapProbeGizmoPlugin, EditorEntity3DGizmoPlugin);

	float probe_size = 0.4f;

public:
	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;
	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	LightmapProbeGizmoPlugin();
};
