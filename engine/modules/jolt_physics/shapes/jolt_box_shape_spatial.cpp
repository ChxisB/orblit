/**************************************************************************/
/*  jolt_box_shape_spatial.cpp                                                 */

#include "jolt_box_shape_spatial.h"

#include "../jolt_project_settings.h"
#include "../misc/jolt_type_conversions.h"

#include <Jolt/Physics/Collision/Shape/BoxShape.h>

JPH::ShapeRefC JoltBoxShape3D::_build() const {
	const float min_half_extent = (float)half_extents[half_extents.min_axis_index()];
	const float actual_margin = MIN(margin, min_half_extent * JoltProjectSettings::collision_margin_fraction);

	const JPH::BoxShapeSettings shape_settings(to_jolt(half_extents), actual_margin);
	const JPH::ShapeSettings::ShapeResult shape_result = shape_settings.Create();

	ERR_FAIL_COND_V_MSG(shape_result.HasError(), nullptr, vformat("Failed to build Jolt Physics box shape with %s. It returned the following error: '%s'. This shape belongs to %s.", to_string(), to_orblit(shape_result.GetError()), _owners_to_string()));

	return shape_result.Get();
}

Variant JoltBoxShape3D::get_data() const {
	return half_extents;
}

void JoltBoxShape3D::set_data(const Variant &p_data) {
	ERR_FAIL_COND(p_data.get_type() != Variant::VECTOR3);

	const Vector3 new_half_extents = p_data;
	if (unlikely(new_half_extents == half_extents)) {
		return;
	}

	half_extents = new_half_extents;

	destroy();
}

void JoltBoxShape3D::set_margin(float p_margin) {
	if (unlikely(margin == p_margin)) {
		return;
	}

	margin = p_margin;

	destroy();
}

String JoltBoxShape3D::to_string() const {
	return vformat("{half_extents=%v margin=%f}", half_extents, margin);
}

AABB JoltBoxShape3D::get_aabb() const {
	return AABB(-half_extents, half_extents * 2.0f);
}
