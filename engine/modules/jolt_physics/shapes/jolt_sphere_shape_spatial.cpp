/**************************************************************************/
/*  jolt_sphere_shape_spatial.cpp                                              */

#include "jolt_sphere_shape_spatial.h"

#include "../misc/jolt_type_conversions.h"

#include <Jolt/Physics/Collision/Shape/SphereShape.h>

JPH::ShapeRefC JoltSphereShape3D::_build() const {
	ERR_FAIL_COND_V_MSG(radius <= 0.0f, nullptr, vformat("Failed to build Jolt Physics sphere shape with %s. Its radius must be greater than 0. This shape belongs to %s.", to_string(), _owners_to_string()));

	const JPH::SphereShapeSettings shape_settings(radius);
	const JPH::ShapeSettings::ShapeResult shape_result = shape_settings.Create();
	ERR_FAIL_COND_V_MSG(shape_result.HasError(), nullptr, vformat("Failed to build Jolt Physics sphere shape with %s. It returned the following error: '%s'. This shape belongs to %s.", to_string(), to_orblit(shape_result.GetError()), _owners_to_string()));

	return shape_result.Get();
}

Variant JoltSphereShape3D::get_data() const {
	return radius;
}

void JoltSphereShape3D::set_data(const Variant &p_data) {
	ERR_FAIL_COND(p_data.get_type() != Variant::FLOAT);

	const float new_radius = p_data;
	if (unlikely(new_radius == radius)) {
		return;
	}

	radius = new_radius;

	destroy();
}

AABB JoltSphereShape3D::get_aabb() const {
	const Vector3 half_extents(radius, radius, radius);
	return AABB(-half_extents, half_extents * 2.0f);
}

String JoltSphereShape3D::to_string() const {
	return vformat("{radius=%f}", radius);
}
