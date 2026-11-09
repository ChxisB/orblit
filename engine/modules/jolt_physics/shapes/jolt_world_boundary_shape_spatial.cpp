/**************************************************************************/
/*  jolt_world_boundary_shape_spatial.cpp                                      */

#include "jolt_world_boundary_shape_spatial.h"

#include "../jolt_project_settings.h"
#include "../misc/jolt_type_conversions.h"

#include <Jolt/Physics/Collision/Shape/PlaneShape.h>

JPH::ShapeRefC JoltWorldBoundaryShape3D::_build() const {
	const Plane normalized_plane = plane.normalized();
	ERR_FAIL_COND_V_MSG(normalized_plane == Plane(), nullptr, vformat("Failed to build Jolt Physics world boundary shape with %s. The plane's normal must not be zero. This shape belongs to %s.", to_string(), _owners_to_string()));

	const float half_size = JoltProjectSettings::world_boundary_shape_size / 2.0f;
	const JPH::PlaneShapeSettings shape_settings(to_jolt(normalized_plane), nullptr, half_size);
	const JPH::ShapeSettings::ShapeResult shape_result = shape_settings.Create();
	ERR_FAIL_COND_V_MSG(shape_result.HasError(), nullptr, vformat("Failed to build Jolt Physics world boundary shape with %s. It returned the following error: '%s'. This shape belongs to %s.", to_string(), to_orblit(shape_result.GetError()), _owners_to_string()));

	return shape_result.Get();
}

Variant JoltWorldBoundaryShape3D::get_data() const {
	return plane;
}

void JoltWorldBoundaryShape3D::set_data(const Variant &p_data) {
	ERR_FAIL_COND(p_data.get_type() != Variant::PLANE);

	const Plane new_plane = p_data;
	if (unlikely(new_plane == plane)) {
		return;
	}

	plane = p_data;

	destroy();
}

AABB JoltWorldBoundaryShape3D::get_aabb() const {
	const float size = JoltProjectSettings::world_boundary_shape_size;
	const float half_size = size / 2.0f;
	return AABB(Vector3(-half_size, -half_size, -half_size), Vector3(size, half_size, size));
}

String JoltWorldBoundaryShape3D::to_string() const {
	return vformat("{plane=%s}", plane);
}
