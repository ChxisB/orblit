/**************************************************************************/
/*  physics_body_spatial.h                                                     */

#pragma once

#include "scene/spatial/physics/collision_object_spatial.h"
#include "scene/spatial/physics/kinematic_collision_spatial.h"
#include "systems/physics_spatial/physics_system_spatial.h"

class PhysicsBody3D : public CollisionObject3D {
	ORBLIT_CLASS(PhysicsBody3D, CollisionObject3D);

protected:
	static void register_members();
	PhysicsBody3D(PS3DE::BodyMode p_mode);

	Ref<KinematicCollision3D> motion_cache;

	uint16_t locked_axis = 0;

	Ref<KinematicCollision3D> _move(const Vector3 &p_motion, bool p_test_only = false, real_t p_margin = 0.001, bool p_recovery_as_collision = false, int p_max_collisions = 1);

public:
	static constexpr AncestralClass static_ancestral_class = AncestralClass::PHYSICS_BODY_3D;

	PackedStringArray get_configuration_warnings() const override;

	bool move_and_collide(const PS3DT::MotionParameters &p_parameters, PS3DT::MotionResult &r_result, bool p_test_only = false, bool p_cancel_sliding = true);
	bool test_move(const Transform3D &p_from, const Vector3 &p_motion, const Ref<KinematicCollision3D> &r_collision = Ref<KinematicCollision3D>(), real_t p_margin = 0.001, bool p_recovery_as_collision = false, int p_max_collisions = 1);
	Vector3 get_gravity() const;

	void set_axis_lock(PS3DE::BodyAxis p_axis, bool p_lock);
	bool get_axis_lock(PS3DE::BodyAxis p_axis) const;

	virtual Vector3 get_linear_velocity() const;
	virtual Vector3 get_angular_velocity() const;
	virtual real_t get_inverse_mass() const;

	TypedArray<PhysicsBody3D> get_collision_exceptions();
	void add_collision_exception_with(RequiredParam<Entity> p_node); //must be physicsbody
	void remove_collision_exception_with(RequiredParam<Entity> p_node);
};
