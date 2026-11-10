/**************************************************************************/
/*  particles_spatial_emission_shape_gizmo_plugin.h                            */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"
#include "editor/scene/spatial/gizmos/gizmo_spatial_helper.h"

class Particles3DEmissionShapeGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(Particles3DEmissionShapeGizmoPlugin, EditorEntity3DGizmoPlugin);

	Ref<Gizmo3DHelper> helper;

public:
	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;
	bool is_selectable_when_hidden() const override;
	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	String get_handle_name(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const override;
	Variant get_handle_value(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const override;
	void set_handle(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary, Camera3D *p_camera, const Point2 &p_point) override;
	void commit_handle(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary, const Variant &p_restore, bool p_cancel = false) override;

	Particles3DEmissionShapeGizmoPlugin();
};
