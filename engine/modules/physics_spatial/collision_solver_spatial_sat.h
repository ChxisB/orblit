/**************************************************************************/
/*  collision_solver_spatial_sat.h                                             */

#pragma once

#include "collision_solver_spatial.h"

bool sat_calculate_penetration(const OrblitShape3D *p_shape_A, const Transform3D &p_transform_A, const OrblitShape3D *p_shape_B, const Transform3D &p_transform_B, OrblitCollisionSolver3D::CallbackResult p_result_callback, void *p_userdata, bool p_swap = false, Vector3 *r_prev_axis = nullptr, real_t p_margin_a = 0, real_t p_margin_b = 0);
