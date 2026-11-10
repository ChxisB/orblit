/**************************************************************************/
/*  vector2i.cpp                                                          */

#include "vector2i.h"

#include "core/math/vector2.h"
#include "core/string/ustring.h"

Vector2i Vector2i::clamp(const Vector2i &p_min, const Vector2i &p_max) const {
	return Vector2i(
			CLAMP(x, p_min.x, p_max.x),
			CLAMP(y, p_min.y, p_max.y));
}

Vector2i Vector2i::clampi(int32_t p_min, int32_t p_max) const {
	return Vector2i(
			CLAMP(x, p_min, p_max),
			CLAMP(y, p_min, p_max));
}

Vector2i Vector2i::snapped(const Vector2i &p_step) const {
	return Vector2i(
			Math::snapped(x, p_step.x),
			Math::snapped(y, p_step.y));
}

Vector2i Vector2i::snappedi(int32_t p_step) const {
	return Vector2i(
			Math::snapped(x, p_step),
			Math::snapped(y, p_step));
}

int64_t Vector2i::length_squared() const {
	return x * (int64_t)x + y * (int64_t)y;
}

double Vector2i::length() const {
	return Math::sqrt((double)length_squared());
}

Vector2i::operator String() const {
	return "(" + itos(x) + ", " + itos(y) + ")";
}

Vector2i::operator Vector2() const {
	return Vector2((int32_t)x, (int32_t)y);
}
