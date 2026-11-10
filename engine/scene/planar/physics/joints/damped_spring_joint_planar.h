/**************************************************************************/
/*  damped_spring_joint_planar.h                                              */

#pragma once

#include "scene/planar/physics/joints/joint_planar.h"

class PhysicsBody2D;

class DampedSpringJoint2D : public Joint2D {
	ORBLIT_CLASS(DampedSpringJoint2D, Joint2D);

	real_t stiffness = 20.0;
	real_t damping = 1.0;
	real_t rest_length = 0.0;
	real_t length = 50.0;

protected:
	void _notification(int p_what);
	virtual void _configure_joint(RID p_joint, PhysicsBody2D *body_a, PhysicsBody2D *body_b) override;
	static void register_members();

public:
	void set_length(real_t p_length);
	real_t get_length() const;

	void set_rest_length(real_t p_rest_length);
	real_t get_rest_length() const;

	void set_damping(real_t p_damping);
	real_t get_damping() const;

	void set_stiffness(real_t p_stiffness);
	real_t get_stiffness() const;

	DampedSpringJoint2D();
};
