/**************************************************************************/
/*  physics_testmotion_query_result_planar.h                                  */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_planar/physics_system_planar_types.h"

class PhysicsTestMotionResult2D : public RefCounted {
	ORBLIT_CLASS(PhysicsTestMotionResult2D, RefCounted);

	PS2DT::MotionResult result;

protected:
	static void register_members();

public:
	PS2DT::MotionResult *get_result_ptr() { return &result; }

	Vector2 get_travel() const;
	Vector2 get_remainder() const;

	Vector2 get_collision_point() const;
	Vector2 get_collision_normal() const;
	Vector2 get_collider_velocity() const;
	ObjectID get_collider_id() const;
	RID get_collider_rid() const;
	Object *get_collider() const;
	int get_collider_shape() const;
	int get_collision_local_shape() const;
	real_t get_collision_depth() const;
	real_t get_collision_safe_fraction() const;
	real_t get_collision_unsafe_fraction() const;
};
