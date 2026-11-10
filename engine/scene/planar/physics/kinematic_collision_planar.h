/**************************************************************************/
/*  kinematic_collision_planar.h                                              */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_planar/physics_system_planar_types.h"

class CharacterBody2D;
class PhysicsBody2D;

class KinematicCollision2D : public RefCounted {
	ORBLIT_CLASS(KinematicCollision2D, RefCounted);

	ObjectID owner_id;
	friend class PhysicsBody2D;
	friend class CharacterBody2D;
	PS2DT::MotionResult result;

protected:
	static void register_members();

public:
	Vector2 get_position() const;
	Vector2 get_normal() const;
	Vector2 get_travel() const;
	Vector2 get_remainder() const;
	real_t get_angle(const Vector2 &p_up_direction = Vector2(0.0, -1.0)) const;
	real_t get_depth() const;
	Object *get_local_shape() const;
	Object *get_collider() const;
	ObjectID get_collider_id() const;
	RID get_collider_rid() const;
	Object *get_collider_shape() const;
	int get_collider_shape_index() const;
	Vector2 get_collider_velocity() const;
};
