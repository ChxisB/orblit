/**************************************************************************/
/*  light_spatial_gizmo_plugin.h                                               */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class Light3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(Light3DGizmoPlugin, EditorEntity3DGizmoPlugin);

private:
	static float _find_closest_angle_to_half_pi_arc(const Vector3 &p_from, const Vector3 &p_to, float p_arc_radius, const Transform3D &p_arc_xform);

public:
	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;

	String get_handle_name(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const override;
	Variant get_handle_value(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary) const override;
	void set_handle(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary, Camera3D *p_camera, const Point2 &p_point) override;
	void commit_handle(const EditorEntity3DGizmo *p_gizmo, int p_id, bool p_secondary, const Variant &p_restore, bool p_cancel = false) override;
	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	Light3DGizmoPlugin();
};
