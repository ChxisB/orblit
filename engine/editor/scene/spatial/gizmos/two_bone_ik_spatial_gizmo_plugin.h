/**************************************************************************/
/*  two_bone_ik_spatial_gizmo_plugin.h                                         */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class TwoBoneIK3D;
class SurfaceTool;

class TwoBoneIK3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(TwoBoneIK3DGizmoPlugin, EditorEntity3DGizmoPlugin);

	struct SelectionMaterials {
		Ref<StandardMaterial3D> unselected_mat;
		Ref<ShaderMaterial> selected_mat;
	};
	static SelectionMaterials selection_materials;

public:
	static Ref<ArrayMesh> get_joints_mesh(Skeleton3D *p_skeleton, TwoBoneIK3D *p_ik, bool p_is_selected);
	static void draw_line(Ref<SurfaceTool> &p_surface_tool, const Vector3 &p_begin_pos, const Vector3 &p_end_pos, const Color &p_color);
	static void draw_arrow(Ref<SurfaceTool> &p_surface_tool, const Vector3 &p_origin, const Vector3 &p_direction, real_t p_length, const Color &p_color);

	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;

	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	TwoBoneIK3DGizmoPlugin();
	~TwoBoneIK3DGizmoPlugin();
};
