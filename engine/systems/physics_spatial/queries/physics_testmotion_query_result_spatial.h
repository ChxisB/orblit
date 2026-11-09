/**************************************************************************/
/*  physics_testmotion_query_result_spatial.h                                  */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_spatial/physics_system_spatial_types.h"

class PhysicsTestMotionResult3D : public RefCounted {
	ORBLIT_CLASS(PhysicsTestMotionResult3D, RefCounted);

	PS3DT::MotionResult result;

protected:
	static void register_members();

public:
	PS3DT::MotionResult *get_result_ptr() { return &result; }

	Vector3 get_travel() const;
	Vector3 get_remainder() const;
	real_t get_collision_safe_fraction() const;
	real_t get_collision_unsafe_fraction() const;

	int get_collision_count() const;

	Vector3 get_collision_point(int p_collision_index = 0) const;
	Vector3 get_collision_normal(int p_collision_index = 0) const;
	Vector3 get_collider_velocity(int p_collision_index = 0) const;
	ObjectID get_collider_id(int p_collision_index = 0) const;
	RID get_collider_rid(int p_collision_index = 0) const;
	Object *get_collider(int p_collision_index = 0) const;
	int get_collider_shape(int p_collision_index = 0) const;
	int get_collision_local_shape(int p_collision_index = 0) const;
	real_t get_collision_depth(int p_collision_index = 0) const;
};
