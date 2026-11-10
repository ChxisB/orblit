/**************************************************************************/
/*  gjk_epa.h                                                             */

#pragma once

#include "collision_solver_spatial.h"
#include "shape_spatial.h"

bool gjk_epa_calculate_penetration(const OrblitShape3D *p_shape_A, const Transform3D &p_transform_A, const OrblitShape3D *p_shape_B, const Transform3D &p_transform_B, OrblitCollisionSolver3D::CallbackResult p_result_callback, void *p_userdata, bool p_swap = false, real_t p_margin_A = 0.0, real_t p_margin_B = 0.0);
bool gjk_epa_calculate_distance(const OrblitShape3D *p_shape_A, const Transform3D &p_transform_A, const OrblitShape3D *p_shape_B, const Transform3D &p_transform_B, Vector3 &r_result_A, Vector3 &r_result_B);
