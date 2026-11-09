/**************************************************************************/
/*  chain_ik_spatial_gizmo_plugin.h                                            */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class ChainIK3D;
class SurfaceTool;

class ChainIK3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(ChainIK3DGizmoPlugin, EditorEntity3DGizmoPlugin);

	struct SelectionMaterials {
		Ref<StandardMaterial3D> unselected_mat;
		Ref<ShaderMaterial> selected_mat;
	};
	static SelectionMaterials selection_materials;

public:
	static void get_joints_mesh(Skeleton3D *p_skeleton, ChainIK3D *p_ik, bool p_is_selected, Ref<ArrayMesh> &r_skinned_mesh, Ref<ArrayMesh> &r_mesh);
	static void draw_line(Ref<SurfaceTool> &p_surface_tool, const Vector3 &p_begin_pos, const Vector3 &p_end_pos, const Color &p_color);

	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;

	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	ChainIK3DGizmoPlugin();
	~ChainIK3DGizmoPlugin();
};
