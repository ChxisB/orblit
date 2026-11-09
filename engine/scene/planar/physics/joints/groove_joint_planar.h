/**************************************************************************/
/*  groove_joint_planar.h                                                     */

#pragma once

#include "scene/planar/physics/joints/joint_planar.h"

class PhysicsBody2D;

class GrooveJoint2D : public Joint2D {
	ORBLIT_CLASS(GrooveJoint2D, Joint2D);

	real_t length = 50.0;
	real_t initial_offset = 25.0;

protected:
	void _notification(int p_what);
	virtual void _configure_joint(RID p_joint, PhysicsBody2D *body_a, PhysicsBody2D *body_b) override;
	static void register_members();

public:
	void set_length(real_t p_length);
	real_t get_length() const;

	void set_initial_offset(real_t p_initial_offset);
	real_t get_initial_offset() const;

	GrooveJoint2D();
};
