/**************************************************************************/
/*  collision_solver_planar.h                                                 */

#pragma once

#include "shape_planar.h"

class OrblitCollisionSolver2D {
public:
	typedef void (*CallbackResult)(const Vector2 &p_point_A, const Vector2 &p_point_B, void *p_userdata);

private:
	static bool solve_static_world_boundary(const OrblitShape2D *p_shape_A, const Transform2D &p_transform_A, const OrblitShape2D *p_shape_B, const Transform2D &p_transform_B, const Vector2 &p_motion_B, CallbackResult p_result_callback, void *p_userdata, bool p_swap_result, real_t p_margin = 0);
	static bool concave_callback(void *p_userdata, OrblitShape2D *p_convex);
	static bool solve_concave(const OrblitShape2D *p_shape_A, const Transform2D &p_transform_A, const Vector2 &p_motion_A, const OrblitShape2D *p_shape_B, const Transform2D &p_transform_B, const Vector2 &p_motion_B, CallbackResult p_result_callback, void *p_userdata, bool p_swap_result, Vector2 *r_sep_axis = nullptr, real_t p_margin_A = 0, real_t p_margin_B = 0);
	static bool solve_separation_ray(const OrblitShape2D *p_shape_A, const Vector2 &p_motion_A, const Transform2D &p_transform_A, const OrblitShape2D *p_shape_B, const Transform2D &p_transform_B, CallbackResult p_result_callback, void *p_userdata, bool p_swap_result, Vector2 *r_sep_axis = nullptr, real_t p_margin = 0);

public:
	static bool solve(const OrblitShape2D *p_shape_A, const Transform2D &p_transform_A, const Vector2 &p_motion_A, const OrblitShape2D *p_shape_B, const Transform2D &p_transform_B, const Vector2 &p_motion_B, CallbackResult p_result_callback, void *p_userdata, Vector2 *r_sep_axis = nullptr, real_t p_margin_A = 0, real_t p_margin_B = 0);
};
