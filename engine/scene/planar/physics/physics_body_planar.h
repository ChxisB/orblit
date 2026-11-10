/**************************************************************************/
/*  physics_body_planar.h                                                     */

#pragma once

#include "scene/planar/physics/collision_object_planar.h"
#include "scene/planar/physics/kinematic_collision_planar.h"

class PhysicsBody2D : public CollisionObject2D {
	ORBLIT_CLASS(PhysicsBody2D, CollisionObject2D);

protected:
	static void register_members();
	PhysicsBody2D(PS2DE::BodyMode p_mode);

	Ref<KinematicCollision2D> motion_cache;

	Ref<KinematicCollision2D> _move(const Vector2 &p_motion, bool p_test_only = false, real_t p_margin = 0.08, bool p_recovery_as_collision = false);

public:
	PackedStringArray get_configuration_warnings() const override;

	bool move_and_collide(const PS2DT::MotionParameters &p_parameters, PS2DT::MotionResult &r_result, bool p_test_only = false, bool p_cancel_sliding = true);
	bool test_move(const Transform2D &p_from, const Vector2 &p_motion, const Ref<KinematicCollision2D> &r_collision = Ref<KinematicCollision2D>(), real_t p_margin = 0.08, bool p_recovery_as_collision = false);
	Vector2 get_gravity() const;

	TypedArray<PhysicsBody2D> get_collision_exceptions();
	void add_collision_exception_with(RequiredParam<Entity> p_node); //must be physicsbody
	void remove_collision_exception_with(RequiredParam<Entity> p_node);
};
