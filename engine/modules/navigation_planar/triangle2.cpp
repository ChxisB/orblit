/**************************************************************************/
/*  triangle2.cpp                                                         */

#include "triangle2.h"

Vector2 Triangle2::get_random_point_inside() const {
	real_t a = Math::random(0.0, 1.0);
	real_t b = Math::random(0.0, 1.0);
	if (a > b) {
		SWAP(a, b);
	}

	return vertex[0] * a + vertex[1] * (b - a) + vertex[2] * (1.0f - b);
}

Vector2 Triangle2::get_closest_point_to(const Vector2 &p_point) const {
	Vector2 edge0 = vertex[1] - vertex[0];
	Vector2 edge1 = vertex[2] - vertex[0];
	Vector2 v0 = vertex[0] - p_point;

	real_t a = edge0.dot(edge0);
	real_t b = edge0.dot(edge1);
	real_t c = edge1.dot(edge1);
	real_t d = edge0.dot(v0);
	real_t e = edge1.dot(v0);

	real_t det = a * c - b * b;
	real_t s = b * e - c * d;
	real_t t = b * d - a * e;

	if (s + t < det) {
		if (s < 0.f) {
			if (t < 0.f) {
				if (d < 0.f) {
					s = CLAMP(-d / a, 0.f, 1.f);
					t = 0.f;
				} else {
					s = 0.f;
					t = CLAMP(-e / c, 0.f, 1.f);
				}
			} else {
				s = 0.f;
				t = CLAMP(-e / c, 0.f, 1.f);
			}
		} else if (t < 0.f) {
			s = CLAMP(-d / a, 0.f, 1.f);
			t = 0.f;
		} else {
			real_t inv_det = 1.f / det;
			s *= inv_det;
			t *= inv_det;
		}
	} else {
		if (s < 0.f) {
			real_t tmp0 = b + d;
			real_t tmp1 = c + e;
			if (tmp1 > tmp0) {
				real_t numer = tmp1 - tmp0;
				real_t denom = a - 2 * b + c;
				s = CLAMP(numer / denom, 0.f, 1.f);
				t = 1 - s;
			} else {
				t = CLAMP(-e / c, 0.f, 1.f);
				s = 0.f;
			}
		} else if (t < 0.f) {
			if (a + d > b + e) {
				real_t numer = c + e - b - d;
				real_t denom = a - 2 * b + c;
				s = CLAMP(numer / denom, 0.f, 1.f);
				t = 1 - s;
			} else {
				s = CLAMP(-d / a, 0.f, 1.f);
				t = 0.f;
			}
		} else {
			real_t numer = c + e - b - d;
			real_t denom = a - 2 * b + c;
			s = CLAMP(numer / denom, 0.f, 1.f);
			t = 1.f - s;
		}
	}

	return vertex[0] + s * edge0 + t * edge1;
}
