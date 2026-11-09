/**************************************************************************/
/*  physics_system_planar_types.h                                             */

#pragma once

#include "core/math/vector2.h"
#include "core/object/object.h"
#include "core/containers/hash_set.h"
#include "core/containers/rid.h"

namespace PhysicsSystem2DTypes {

struct RayParameters {
	Vector2 from;
	Vector2 to;
	HashSet<RID> exclude;
	uint32_t collision_mask = UINT32_MAX;

	bool collide_with_bodies = true;
	bool collide_with_areas = false;

	bool hit_from_inside = false;
};

struct RayResult {
	Vector2 position;
	Vector2 normal;
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
	Vector2 position;
	ObjectID canvas_instance_id;
	HashSet<RID> exclude;
	uint32_t collision_mask = UINT32_MAX;

	bool collide_with_bodies = true;
	bool collide_with_areas = false;

	bool pick_point = false;
};

struct ShapeParameters {
	RID shape_rid;
	Transform2D transform;
	Vector2 motion;
	real_t margin = 0.0;
	HashSet<RID> exclude;
	uint32_t collision_mask = UINT32_MAX;

	bool collide_with_bodies = true;
	bool collide_with_areas = false;
};

struct ShapeRestInfo {
	Vector2 point;
	Vector2 normal;
	RID rid;
	ObjectID collider_id;
	int shape = 0;
	Vector2 linear_velocity; // Velocity at contact point.
};

struct MotionParameters {
	Transform2D from;
	Vector2 motion;
	real_t margin = 0.08;
	bool collide_separation_ray = false;
	HashSet<RID> exclude_bodies;
	HashSet<ObjectID> exclude_objects;
	bool recovery_as_collision = false;

	MotionParameters() {}

	MotionParameters(const Transform2D &p_from, const Vector2 &p_motion, real_t p_margin = 0.08) :
			from(p_from),
			motion(p_motion),
			margin(p_margin) {}
};

struct MotionResult {
	Vector2 travel;
	Vector2 remainder;

	Vector2 collision_point;
	Vector2 collision_normal;
	Vector2 collider_velocity;
	real_t collision_depth = 0.0;
	real_t collision_safe_fraction = 0.0;
	real_t collision_unsafe_fraction = 0.0;
	int collision_local_shape = 0;
	ObjectID collider_id;
	RID collider;
	int collider_shape = 0;

	real_t get_angle(Vector2 p_up_direction) const {
		return Math::acos(collision_normal.dot(p_up_direction));
	}
};
} // namespace PhysicsSystem2DTypes

// Alias to make it easier to use.
#define PS2DT PhysicsSystem2DTypes
