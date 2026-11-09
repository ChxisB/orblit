/**************************************************************************/
/*  spring_bone_spatial_gizmo_plugin.h                                         */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class SpringBoneCollision3D;
class SpringBoneSimulator3D;
class SurfaceTool;

class SpringBoneSimulator3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(SpringBoneSimulator3DGizmoPlugin, EditorEntity3DGizmoPlugin);

	struct SelectionMaterials {
		Ref<StandardMaterial3D> unselected_mat;
		Ref<ShaderMaterial> selected_mat;
	};
	static SelectionMaterials selection_materials;

public:
	static Ref<ArrayMesh> get_joints_mesh(Skeleton3D *p_skeleton, SpringBoneSimulator3D *p_simulator, bool p_is_selected);
	static void draw_sphere(Ref<SurfaceTool> &p_surface_tool, const Basis &p_basis, const Vector3 &p_center, float p_radius, const Color &p_color);
	static void draw_line(Ref<SurfaceTool> &p_surface_tool, const Vector3 &p_begin_pos, const Vector3 &p_end_pos, const Color &p_color);

	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;

	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	SpringBoneSimulator3DGizmoPlugin();
	~SpringBoneSimulator3DGizmoPlugin();
};

class SpringBoneCollision3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(SpringBoneCollision3DGizmoPlugin, EditorEntity3DGizmoPlugin);

	struct SelectionMaterials {
		Ref<StandardMaterial3D> unselected_mat;
		Ref<ShaderMaterial> selected_mat;
	};
	static SelectionMaterials selection_materials;

public:
	static Ref<ArrayMesh> get_collision_mesh(SpringBoneCollision3D *p_collision, bool p_is_selected);
	static void draw_sphere(Ref<SurfaceTool> &p_surface_tool, float p_radius, const Color &p_color);
	static void draw_capsule(Ref<SurfaceTool> &p_surface_tool, float p_radius, float p_height, const Color &p_color);
	static void draw_plane(Ref<SurfaceTool> &p_surface_tool, const Color &p_color);

	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;

	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	SpringBoneCollision3DGizmoPlugin();
	~SpringBoneCollision3DGizmoPlugin();
};
