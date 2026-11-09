/**************************************************************************/
/*  collision_solver_spatial.h                                                 */

#pragma once

#include "shape_spatial.h"

class OrblitCollisionSolver3D {
public:
	typedef void (*CallbackResult)(const Vector3 &p_point_A, int p_index_A, const Vector3 &p_point_B, int p_index_B, const Vector3 &normal, void *p_userdata);

private:
	static bool soft_body_query_callback(uint32_t p_node_index, void *p_userdata);
	static void soft_body_contact_callback(const Vector3 &p_point_A, int p_index_A, const Vector3 &p_point_B, int p_index_B, const Vector3 &normal, void *p_userdata);
	static bool soft_body_concave_callback(void *p_userdata, OrblitShape3D *p_convex);
	static bool concave_callback(void *p_userdata, OrblitShape3D *p_convex);
	static bool solve_static_world_boundary(const OrblitShape3D *p_shape_A, const Transform3D &p_transform_A, const OrblitShape3D *p_shape_B, const Transform3D &p_transform_B, CallbackResult p_result_callback, void *p_userdata, bool p_swap_result, real_t p_margin = 0);
	static bool solve_separation_ray(const OrblitShape3D *p_shape_A, const Transform3D &p_transform_A, const OrblitShape3D *p_shape_B, const Transform3D &p_transform_B, CallbackResult p_result_callback, void *p_userdata, bool p_swap_result, real_t p_margin = 0);
	static bool solve_soft_body(const OrblitShape3D *p_shape_A, const Transform3D &p_transform_A, const OrblitShape3D *p_shape_B, const Transform3D &p_transform_B, CallbackResult p_result_callback, void *p_userdata, bool p_swap_result);
	static bool solve_concave(const OrblitShape3D *p_shape_A, const Transform3D &p_transform_A, const OrblitShape3D *p_shape_B, const Transform3D &p_transform_B, CallbackResult p_result_callback, void *p_userdata, bool p_swap_result, real_t p_margin_A = 0, real_t p_margin_B = 0);
	static bool concave_distance_callback(void *p_userdata, OrblitShape3D *p_convex);
	static bool solve_distance_world_boundary(const OrblitShape3D *p_shape_A, const Transform3D &p_transform_A, const OrblitShape3D *p_shape_B, const Transform3D &p_transform_B, Vector3 &r_point_A, Vector3 &r_point_B);

public:
	static bool solve_static(const OrblitShape3D *p_shape_A, const Transform3D &p_transform_A, const OrblitShape3D *p_shape_B, const Transform3D &p_transform_B, CallbackResult p_result_callback, void *p_userdata, Vector3 *r_sep_axis = nullptr, real_t p_margin_A = 0, real_t p_margin_B = 0);
	static bool solve_distance(const OrblitShape3D *p_shape_A, const Transform3D &p_transform_A, const OrblitShape3D *p_shape_B, const Transform3D &p_transform_B, Vector3 &r_point_A, Vector3 &r_point_B, const AABB &p_concave_hint, Vector3 *r_sep_axis = nullptr);
};
