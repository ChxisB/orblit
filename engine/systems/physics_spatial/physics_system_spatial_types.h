/**************************************************************************/
/*  physics_system_spatial_types.h                                             */

#pragma once

#include "core/containers/hash_set.h"
#include "core/containers/rid.h"
#include "core/math/vector3.h"
#include "core/object/object.h"

namespace PhysicsSystem3DTypes {

struct RayParameters {
	Vector3 from;
	Vector3 to;
	HashSet<RID> exclude;
	uint32_t collision_mask = UINT32_MAX;

	bool collide_with_bodies = true;
	bool collide_with_areas = false;

	bool hit_from_inside = false;
	bool hit_back_faces = true;

	bool pick_ray = false;
};

struct RayResult {
	Vector3 position;
	Vector3 normal;
	RID rid;
	ObjectID collider_id;
	int shape = 0;
	int face_index = -1;

	_FORCE_INLINE_ Object *get_collider() const {
		if (collider_id.is_valid()) {
			return ObjectRegistry::get_instance(collider_id);
		} else {
			return nullptr;
		}
	}
};

struct ShapeResult {
	RID rid;
	ObjectID collider_id;
	int shape = 0;

	_FORCE_INLINE_ Object *get_collider() const {
		if (collider_id.is_valid()) {
			return ObjectRegistry::get_instance(collider_id);
		} else {
			return nullptr;
		}
	}
};

struct PointParameters {
	Vector3 position;
	HashSet<RID> exclude;
	uint32_t collision_mask = UINT32_MAX;

	bool collide_with_bodies = true;
	bool collide_with_areas = false;
};

struct ShapeParameters {
	RID shape_rid;
	Transform3D transform;
	Vector3 motion;
	real_t margin = 0.0;
	HashSet<RID> exclude;
	uint32_t collision_mask = UINT32_MAX;

	bool collide_with_bodies = true;
	bool collide_with_areas = false;
};

struct ShapeRestInfo {
	Vector3 point;
	Vector3 normal;
	RID rid;
	ObjectID collider_id;
	int shape = 0;
	Vector3 linear_velocity; // Velocity at contact point.
};

struct MotionParameters {
	Transform3D from;
	Vector3 motion;
	real_t margin = 0.001;
	int max_collisions = 1;
	bool collide_separation_ray = false;
	HashSet<RID> exclude_bodies;
	HashSet<ObjectID> exclude_objects;
	bool recovery_as_collision = false;

	MotionParameters() {}

	MotionParameters(const Transform3D &p_from, const Vector3 &p_motion, real_t p_margin = 0.001) :
			from(p_from),
			motion(p_motion),
			margin(p_margin) {}
};

struct MotionCollision {
	Vector3 position;
	Vector3 normal;
	Vector3 collider_velocity;
	Vector3 collider_angular_velocity;
	real_t depth = 0.0;
	int local_shape = 0;
	ObjectID collider_id;
	RID collider;
	int collider_shape = 0;

	real_t get_angle(Vector3 p_up_direction) const {
		return Math::acos(normal.dot(p_up_direction));
	}
};

struct MotionResult {
	Vector3 travel;
	Vector3 remainder;
	real_t collision_depth = 0.0;
	real_t collision_safe_fraction = 0.0;
	real_t collision_unsafe_fraction = 0.0;

	static const int MAX_COLLISIONS = 32;
	MotionCollision collisions[MAX_COLLISIONS];
	int collision_count = 0;
};
} // namespace PhysicsSystem3DTypes

// Alias to make it easier to use.
#define PS3DT PhysicsSystem3DTypes
