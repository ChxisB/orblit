/**************************************************************************/
/*  jolt_shape_instance_spatial.cpp                                            */

#include "jolt_shape_instance_spatial.h"

#include "jolt_shape_spatial.h"

#include <Jolt/Physics/Collision/Shape/DecoratedShape.h>

JoltShapeInstance3D::ShapeReference::ShapeReference(JoltShapedObject3D *p_parent, JoltShape3D *p_shape) :
		parent(p_parent),
		shape(p_shape) {
	if (shape != nullptr) {
		shape->add_owner(parent);
	}
}

JoltShapeInstance3D::ShapeReference::ShapeReference(ShapeReference &&p_other) :
		parent(p_other.parent),
		shape(p_other.shape) {
	p_other.parent = nullptr;
	p_other.shape = nullptr;
}

JoltShapeInstance3D::ShapeReference::~ShapeReference() {
	if (shape != nullptr) {
		shape->remove_owner(parent);
	}
}

JoltShapeInstance3D::ShapeReference &JoltShapeInstance3D::ShapeReference::operator=(ShapeReference &&p_other) {
	if (this != &p_other) {
		SWAP(parent, p_other.parent);
		SWAP(shape, p_other.shape);
	}

	return *this;
}

JoltShapeInstance3D::JoltShapeInstance3D(JoltShapedObject3D *p_parent, JoltShape3D *p_shape, const Transform3D &p_transform, const Vector3 &p_scale, bool p_disabled) :
		transform(p_transform),
		scale(p_scale),
		shape(p_parent, p_shape),
		disabled(p_disabled) {
}

AABB JoltShapeInstance3D::get_aabb() const {
	return get_transform_scaled().xform(shape->get_aabb());
}

bool JoltShapeInstance3D::try_build(bool p_object_has_back_face_collision) {
	ERR_FAIL_COND_V(is_disabled(), false);

	const JPH::ShapeRefC maybe_new_shape = shape->try_build();

	if (maybe_new_shape == nullptr) {
		jolt_ref = nullptr;
		return false;
	}

	if (jolt_ref != nullptr) {
		const JPH::DecoratedShape *outer_shape = static_cast<const JPH::DecoratedShape *>(jolt_ref.GetPtr());

		if (outer_shape->GetInnerShape() == maybe_new_shape) {
			return true;
		}
	}

	jolt_ref = JoltShape3D::with_instance_overrides(maybe_new_shape, (uint64_t)id, p_object_has_back_face_collision, shape->has_back_face_collision());

	return true;
}
