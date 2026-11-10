/**************************************************************************/
/*  pin_joint_planar.h                                                        */

#pragma once

#include "scene/planar/physics/joints/joint_planar.h"

class PhysicsBody2D;

class PinJoint2D : public Joint2D {
	ORBLIT_CLASS(PinJoint2D, Joint2D);

	real_t softness = 0.0;
	real_t angular_limit_lower = 0.0;
	real_t angular_limit_upper = 0.0;
	real_t motor_target_velocity = 0.0;
	bool motor_enabled = false;
	bool angular_limit_enabled = false;

protected:
	void _notification(int p_what);
	virtual void _configure_joint(RID p_joint, PhysicsBody2D *body_a, PhysicsBody2D *body_b) override;
	static void register_members();

public:
	void set_softness(real_t p_softness);
	real_t get_softness() const;
	void set_angular_limit_lower(real_t p_angular_limit_lower);
	real_t get_angular_limit_lower() const;
	void set_angular_limit_upper(real_t p_angular_limit_upper);
	real_t get_angular_limit_upper() const;
	void set_motor_target_velocity(real_t p_motor_target_velocity);
	real_t get_motor_target_velocity() const;

	void set_motor_enabled(bool p_motor_enabled);
	bool is_motor_enabled() const;
	void set_angular_limit_enabled(bool p_angular_limit_enabled);
	bool is_angular_limit_enabled() const;

	PinJoint2D();
};
