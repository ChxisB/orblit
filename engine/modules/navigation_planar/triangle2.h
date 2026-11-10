/**************************************************************************/
/*  triangle2.h                                                           */

#pragma once

#include "core/math/vector2.h"

struct Triangle2 {
	Vector2 vertex[3];

	real_t get_area() const {
		return Math::abs((vertex[0] - vertex[1]).cross(vertex[0] - vertex[2])) * 0.5f;
	}

	Vector2 get_random_point_inside() const;

	Vector2 get_closest_point_to(const Vector2 &p_point) const;

	Triangle2() {}
	Triangle2(const Vector2 &p_v1, const Vector2 &p_v2, const Vector2 &p_v3) {
		vertex[0] = p_v1;
		vertex[1] = p_v2;
		vertex[2] = p_v3;
	}
};
