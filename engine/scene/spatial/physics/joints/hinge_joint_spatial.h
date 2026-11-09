/**************************************************************************/
/*  hinge_joint_spatial.h                                                      */

#pragma once

#include "scene/spatial/physics/joints/joint_spatial.h"

class HingeJoint3D : public Joint3D {
	ORBLIT_CLASS(HingeJoint3D, Joint3D);

public:
	enum Param {
		PARAM_BIAS = PS3DE::HINGE_JOINT_BIAS,
		PARAM_LIMIT_UPPER = PS3DE::HINGE_JOINT_LIMIT_UPPER,
		PARAM_LIMIT_LOWER = PS3DE::HINGE_JOINT_LIMIT_LOWER,
		PARAM_LIMIT_BIAS = PS3DE::HINGE_JOINT_LIMIT_BIAS,
		PARAM_LIMIT_RELAXATION = PS3DE::HINGE_JOINT_LIMIT_RELAXATION,
		PARAM_MOTOR_TARGET_VELOCITY = PS3DE::HINGE_JOINT_MOTOR_TARGET_VELOCITY,
		PARAM_MOTOR_MAX_IMPULSE = PS3DE::HINGE_JOINT_MOTOR_MAX_IMPULSE,
		PARAM_MAX = PS3DE::HINGE_JOINT_MAX
	};

	enum Flag {
		FLAG_USE_LIMIT = PS3DE::HINGE_JOINT_FLAG_USE_LIMIT,
		FLAG_ENABLE_MOTOR = PS3DE::HINGE_JOINT_FLAG_ENABLE_MOTOR,
		FLAG_MAX = PS3DE::HINGE_JOINT_FLAG_MAX
	};

protected:
	real_t params[PARAM_MAX];
	bool flags[FLAG_MAX];
	virtual void _configure_joint(RID p_joint, PhysicsBody3D *body_a, PhysicsBody3D *body_b) override;
	static void register_members();

public:
	void set_param(Param p_param, real_t p_value);
	real_t get_param(Param p_param) const;

	void set_flag(Flag p_flag, bool p_value);
	bool get_flag(Flag p_flag) const;

	HingeJoint3D();
};

VARIANT_ENUM_CAST(HingeJoint3D::Param);
VARIANT_ENUM_CAST(HingeJoint3D::Flag);
