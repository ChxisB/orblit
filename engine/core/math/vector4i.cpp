/**************************************************************************/
/*  vector4i.cpp                                                          */

#include "vector4i.h"

#include "core/math/vector4.h"
#include "core/string/ustring.h"

Vector4i::Axis Vector4i::min_axis_index() const {
	uint32_t min_index = 0;
	int32_t min_value = x;
	for (uint32_t i = 1; i < 4; i++) {
		if (operator[](i) <= min_value) {
			min_index = i;
			min_value = operator[](i);
		}
	}
	return Vector4i::Axis(min_index);
}

Vector4i::Axis Vector4i::max_axis_index() const {
	uint32_t max_index = 0;
	int32_t max_value = x;
	for (uint32_t i = 1; i < 4; i++) {
		if (operator[](i) > max_value) {
			max_index = i;
			max_value = operator[](i);
		}
	}
	return Vector4i::Axis(max_index);
}

Vector4i Vector4i::clamp(const Vector4i &p_min, const Vector4i &p_max) const {
	return Vector4i(
			CLAMP(x, p_min.x, p_max.x),
			CLAMP(y, p_min.y, p_max.y),
			CLAMP(z, p_min.z, p_max.z),
			CLAMP(w, p_min.w, p_max.w));
}

Vector4i Vector4i::clampi(int32_t p_min, int32_t p_max) const {
	return Vector4i(
			CLAMP(x, p_min, p_max),
			CLAMP(y, p_min, p_max),
			CLAMP(z, p_min, p_max),
			CLAMP(w, p_min, p_max));
}

Vector4i Vector4i::snapped(const Vector4i &p_step) const {
	return Vector4i(
			Math::snapped(x, p_step.x),
			Math::snapped(y, p_step.y),
			Math::snapped(z, p_step.z),
			Math::snapped(w, p_step.w));
}

Vector4i Vector4i::snappedi(int32_t p_step) const {
	return Vector4i(
			Math::snapped(x, p_step),
			Math::snapped(y, p_step),
			Math::snapped(z, p_step),
			Math::snapped(w, p_step));
}

Vector4i::operator String() const {
	return "(" + itos(x) + ", " + itos(y) + ", " + itos(z) + ", " + itos(w) + ")";
}

Vector4i::operator Vector4() const {
	return Vector4(x, y, z, w);
}

static_assert(sizeof(Vector4i) == 4 * sizeof(int32_t));
