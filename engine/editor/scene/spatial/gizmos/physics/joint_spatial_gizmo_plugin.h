/**************************************************************************/
/*  joint_spatial_gizmo_plugin.h                                               */

#pragma once

#include "editor/scene/spatial/entity_spatial_editor_gizmos.h"

class Timer;

class Joint3DGizmoPlugin : public EditorEntity3DGizmoPlugin {
	ORBLIT_CLASS(Joint3DGizmoPlugin, EditorEntity3DGizmoPlugin);

	Timer *update_timer = nullptr;
	EditorEntity3DGizmo *last_drawn = nullptr;

	void incremental_update_gizmos();

public:
	bool has_gizmo(Entity3D *p_spatial) override;
	String get_gizmo_name() const override;
	int get_priority() const override;
	void redraw(EditorEntity3DGizmo *p_gizmo) override;

	static void CreatePinJointGizmo(const Transform3D &p_offset, Vector<Vector3> &r_cursor_points);
	static void CreateHingeJointGizmo(const Transform3D &p_offset, const Transform3D &p_trs_joint, const Transform3D &p_trs_body_a, const Transform3D &p_trs_body_b, real_t p_limit_lower, real_t p_limit_upper, bool p_use_limit, Vector<Vector3> &r_common_points, Vector<Vector3> *r_body_a_points, Vector<Vector3> *r_body_b_points);
	static void CreateSliderJointGizmo(const Transform3D &p_offset, const Transform3D &p_trs_joint, const Transform3D &p_trs_body_a, const Transform3D &p_trs_body_b, real_t p_angular_limit_lower, real_t p_angular_limit_upper, real_t p_linear_limit_lower, real_t p_linear_limit_upper, Vector<Vector3> &r_points, Vector<Vector3> *r_body_a_points, Vector<Vector3> *r_body_b_points);
	static void CreateConeTwistJointGizmo(const Transform3D &p_offset, const Transform3D &p_trs_joint, const Transform3D &p_trs_body_a, const Transform3D &p_trs_body_b, real_t p_swing, real_t p_twist, Vector<Vector3> *r_body_a_points, Vector<Vector3> *r_body_b_points);
	static void CreateGeneric6DOFJointGizmo(
			const Transform3D &p_offset,
			const Transform3D &p_trs_joint,
			const Transform3D &p_trs_body_a,
			const Transform3D &p_trs_body_b,
			real_t p_angular_limit_lower_x,
			real_t p_angular_limit_upper_x,
			real_t p_linear_limit_lower_x,
			real_t p_linear_limit_upper_x,
			bool p_enable_angular_limit_x,
			bool p_enable_linear_limit_x,
			real_t p_angular_limit_lower_y,
			real_t p_angular_limit_upper_y,
			real_t p_linear_limit_lower_y,
			real_t p_linear_limit_upper_y,
			bool p_enable_angular_limit_y,
			bool p_enable_linear_limit_y,
			real_t p_angular_limit_lower_z,
			real_t p_angular_limit_upper_z,
			real_t p_linear_limit_lower_z,
			real_t p_linear_limit_upper_z,
			bool p_enable_angular_limit_z,
			bool p_enable_linear_limit_z,
			Vector<Vector3> &r_points,
			Vector<Vector3> *r_body_a_points,
			Vector<Vector3> *r_body_b_points);

	Joint3DGizmoPlugin();
};

class JointGizmosDrawer {
public:
	static Basis look_body(const Transform3D &p_joint_transform, const Transform3D &p_body_transform);
	static Basis look_body_toward(Vector3::Axis p_axis, const Transform3D &joint_transform, const Transform3D &body_transform);
	static Basis look_body_toward_x(const Transform3D &p_joint_transform, const Transform3D &p_body_transform);
	static Basis look_body_toward_y(const Transform3D &p_joint_transform, const Transform3D &p_body_transform);
	/// Special function just used for physics joints, it returns a basis constrained toward Joint Z axis
	/// with axis X and Y that are looking toward the body and oriented toward up
	static Basis look_body_toward_z(const Transform3D &p_joint_transform, const Transform3D &p_body_transform);

	// Draw circle around p_axis
	static void draw_circle(Vector3::Axis p_axis, real_t p_radius, const Transform3D &p_offset, const Basis &p_base, real_t p_limit_lower, real_t p_limit_upper, Vector<Vector3> &r_points, bool p_inverse = false);
	static void draw_cone(const Transform3D &p_offset, const Basis &p_base, real_t p_swing, real_t p_twist, Vector<Vector3> &r_points);
};
