/**************************************************************************/
/*  interpolated_property.cpp                                             */

#include "interpolated_property.h"

#include "core/math/vector2.h"

namespace InterpolatedPropertyFuncs {

float lerp(float p_a, float p_b, float p_fraction) {
	return Math::lerp(p_a, p_b, p_fraction);
}

double lerp(double p_a, double p_b, float p_fraction) {
	return Math::lerp(p_a, p_b, (double)p_fraction);
}

Vector2 lerp(const Vector2 &p_a, const Vector2 &p_b, float p_fraction) {
	return p_a.lerp(p_b, p_fraction);
}

} //namespace InterpolatedPropertyFuncs
