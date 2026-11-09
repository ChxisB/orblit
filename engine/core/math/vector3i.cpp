/**************************************************************************/
/*  vector3i.cpp                                                          */

#include "vector3i.h"

#include "core/math/vector3.h"
#include "core/string/ustring.h"

Vector3i::Axis Vector3i::min_axis_index() const {
	return x < y ? (x < z ? Vector3i::AXIS_X : Vector3i::AXIS_Z) : (y < z ? Vector3i::AXIS_Y : Vector3i::AXIS_Z);
}

Vector3i::Axis Vector3i::max_axis_index() const {
	return x < y ? (y < z ? Vector3i::AXIS_Z : Vector3i::AXIS_Y) : (x < z ? Vector3i::AXIS_Z : Vector3i::AXIS_X);
}

Vector3i Vector3i::clamp(const Vector3i &p_min, const Vector3i &p_max) const {
	return Vector3i(
			CLAMP(x, p_min.x, p_max.x),
			CLAMP(y, p_min.y, p_max.y),
			CLAMP(z, p_min.z, p_max.z));
}

Vector3i Vector3i::clampi(int32_t p_min, int32_t p_max) const {
	return Vector3i(
			CLAMP(x, p_min, p_max),
			CLAMP(y, p_min, p_max),
			CLAMP(z, p_min, p_max));
}

Vector3i Vector3i::snapped(const Vector3i &p_step) const {
	return Vector3i(
			Math::snapped(x, p_step.x),
			Math::snapped(y, p_step.y),
			Math::snapped(z, p_step.z));
}

Vector3i Vector3i::snappedi(int32_t p_step) const {
	return Vector3i(
			Math::snapped(x, p_step),
			Math::snapped(y, p_step),
			Math::snapped(z, p_step));
}

Vector3i::operator String() const {
	return "(" + itos(x) + ", " + itos(y) + ", " + itos(z) + ")";
}

Vector3i::operator Vector3() const {
	return Vector3(x, y, z);
}
