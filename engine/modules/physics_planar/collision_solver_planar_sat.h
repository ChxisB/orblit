/**************************************************************************/
/*  collision_solver_planar_sat.h                                             */

#pragma once

#include "collision_solver_planar.h"

bool sat_2d_calculate_penetration(const OrblitShape2D *p_shape_A, const Transform2D &p_transform_A, const Vector2 &p_motion_A, const OrblitShape2D *p_shape_B, const Transform2D &p_transform_B, const Vector2 &p_motion_B, OrblitCollisionSolver2D::CallbackResult p_result_callback, void *p_userdata, bool p_swap = false, Vector2 *sep_axis = nullptr, real_t p_margin_A = 0, real_t p_margin_B = 0);
