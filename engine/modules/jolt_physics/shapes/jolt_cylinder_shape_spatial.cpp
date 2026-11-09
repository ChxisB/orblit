/**************************************************************************/
/*  jolt_cylinder_shape_spatial.cpp                                            */

#include "jolt_cylinder_shape_spatial.h"

#include "../jolt_project_settings.h"
#include "../misc/jolt_type_conversions.h"

#include <Jolt/Physics/Collision/Shape/CylinderShape.h>

JPH::ShapeRefC JoltCylinderShape3D::_build() const {
	const float half_height = height / 2.0f;
	const float min_half_extent = MIN(half_height, radius);
	const float actual_margin = MIN(margin, min_half_extent * JoltProjectSettings::collision_margin_fraction);

	const JPH::CylinderShapeSettings shape_settings(half_height, radius, actual_margin);
	const JPH::ShapeSettings::ShapeResult shape_result = shape_settings.Create();
	ERR_FAIL_COND_V_MSG(shape_result.HasError(), nullptr, vformat("Failed to build Jolt Physics cylinder shape with %s. It returned the following error: '%s'. This shape belongs to %s.", to_string(), to_orblit(shape_result.GetError()), _owners_to_string()));

	return shape_result.Get();
}

Variant JoltCylinderShape3D::get_data() const {
	Dictionary data;
	data["height"] = height;
	data["radius"] = radius;
	return data;
}

void JoltCylinderShape3D::set_data(const Variant &p_data) {
	ERR_FAIL_COND(p_data.get_type() != Variant::DICTIONARY);

	const Dictionary data = p_data;

	const Variant maybe_height = data.get("height", Variant());
	ERR_FAIL_COND(maybe_height.get_type() != Variant::FLOAT);

	const Variant maybe_radius = data.get("radius", Variant());
	ERR_FAIL_COND(maybe_radius.get_type() != Variant::FLOAT);

	const float new_height = maybe_height;
	const float new_radius = maybe_radius;

	if (unlikely(new_height == height && new_radius == radius)) {
		return;
	}

	height = new_height;
	radius = new_radius;

	destroy();
}

void JoltCylinderShape3D::set_margin(float p_margin) {
	if (unlikely(margin == p_margin)) {
		return;
	}

	margin = p_margin;

	destroy();
}

AABB JoltCylinderShape3D::get_aabb() const {
	const Vector3 half_extents(radius, height / 2.0f, radius);
	return AABB(-half_extents, half_extents * 2.0f);
}

String JoltCylinderShape3D::to_string() const {
	return vformat("{height=%f radius=%f margin=%f}", height, radius, margin);
}
