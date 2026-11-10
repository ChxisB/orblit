/**************************************************************************/
/*  jolt_shape_instance_spatial.h                                              */

#pragma once

#include "core/math/transform_spatial.h"

#include <Jolt/Jolt.h>

#include <Jolt/Physics/Collision/Shape/Shape.h>

class JoltShapedObject3D;
class JoltShape3D;

class JoltShapeInstance3D {
	// This RAII helper exists solely to avoid needing to maintain move construction/assignment in `JoltShapeInstance3D`.
	struct ShapeReference {
		JoltShapedObject3D *parent = nullptr;
		JoltShape3D *shape = nullptr;

		ShapeReference() = default;
		ShapeReference(JoltShapedObject3D *p_parent, JoltShape3D *p_shape);
		ShapeReference(const ShapeReference &p_other) = delete;
		ShapeReference(ShapeReference &&p_other);
		~ShapeReference();

		ShapeReference &operator=(const ShapeReference &p_other) = delete;
		ShapeReference &operator=(ShapeReference &&p_other);

		JoltShape3D *operator*() const { return shape; }
		JoltShape3D *operator->() const { return shape; }
		operator JoltShape3D *() const { return shape; }
	};

	inline static uint32_t next_id = 1;

	Transform3D transform;
	Vector3 scale;
	ShapeReference shape;
	JPH::ShapeRefC jolt_ref;
	uint32_t id = next_id++;
	bool disabled = false;

public:
	JoltShapeInstance3D() = default;
	JoltShapeInstance3D(JoltShapedObject3D *p_parent, JoltShape3D *p_shape, const Transform3D &p_transform = Transform3D(), const Vector3 &p_scale = Vector3(1.0f, 1.0f, 1.0f), bool p_disabled = false);

	uint32_t get_id() const { return id; }

	JoltShape3D *get_shape() const { return shape; }

	const JPH::Shape *get_jolt_ref() const { return jolt_ref; }

	const Transform3D &get_transform_unscaled() const { return transform; }
	Transform3D get_transform_scaled() const { return transform.scaled_local(scale); }
	void set_transform(const Transform3D &p_transform) { transform = p_transform; }

	const Vector3 &get_scale() const { return scale; }
	void set_scale(const Vector3 &p_scale) { scale = p_scale; }

	AABB get_aabb() const;

	bool is_built() const { return jolt_ref != nullptr; }

	bool is_enabled() const { return !disabled; }
	bool is_disabled() const { return disabled; }

	void enable() { disabled = false; }
	void disable() { disabled = true; }

	bool try_build(bool p_object_has_back_face_collision);
};
