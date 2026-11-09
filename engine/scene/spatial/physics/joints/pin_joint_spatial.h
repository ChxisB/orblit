/**************************************************************************/
/*  pin_joint_spatial.h                                                        */

#pragma once

#include "scene/spatial/physics/joints/joint_spatial.h"

class PinJoint3D : public Joint3D {
	ORBLIT_CLASS(PinJoint3D, Joint3D);

public:
	enum Param {
		PARAM_BIAS = PS3DE::PIN_JOINT_BIAS,
		PARAM_DAMPING = PS3DE::PIN_JOINT_DAMPING,
		PARAM_IMPULSE_CLAMP = PS3DE::PIN_JOINT_IMPULSE_CLAMP
	};

protected:
	real_t params[3];
	virtual void _configure_joint(RID p_joint, PhysicsBody3D *body_a, PhysicsBody3D *body_b) override;
	static void register_members();

public:
	void set_param(Param p_param, real_t p_value);
	real_t get_param(Param p_param) const;

	PinJoint3D();
};

VARIANT_ENUM_CAST(PinJoint3D::Param);
