/**************************************************************************/
/*  jolt_slider_joint_spatial.h                                                */

#pragma once

#include "../jolt_physics_system_spatial.h"
#include "jolt_joint_spatial.h"

#include <Jolt/Jolt.h>

#include <Jolt/Physics/Constraints/SliderConstraint.h>

class JoltSliderJoint3D final : public JoltJoint3D {
	typedef PS3DE::SliderJointParam Parameter;
	typedef JoltPhysicsSystem3D::SliderJointParamJolt JoltParameter;
	typedef JoltPhysicsSystem3D::SliderJointFlagJolt JoltFlag;

	double limit_upper = 0.0;
	double limit_lower = 0.0;

	double limit_spring_frequency = 0.0;
	double limit_spring_damping = 0.0;

	double motor_target_speed = 0.0f;
	double motor_max_force = FLT_MAX;

	bool limits_enabled = true;
	bool limit_spring_enabled = false;

	bool motor_enabled = false;

	JPH::Constraint *_build_slider(JPH::Body *p_jolt_body_a, JPH::Body *p_jolt_body_b, const Transform3D &p_shifted_ref_a, const Transform3D &p_shifted_ref_b, float p_limit) const;
	JPH::Constraint *_build_fixed(JPH::Body *p_jolt_body_a, JPH::Body *p_jolt_body_b, const Transform3D &p_shifted_ref_a, const Transform3D &p_shifted_ref_b) const;

	bool _is_sprung() const { return limit_spring_enabled && limit_spring_frequency > 0.0; }
	bool _is_fixed() const { return limits_enabled && limit_lower == limit_upper && !_is_sprung(); }

	void _update_motor_state();
	void _update_motor_velocity();
	void _update_motor_limit();

	void _limits_changed();
	void _limit_spring_changed();
	void _motor_state_changed();
	void _motor_speed_changed();
	void _motor_limit_changed();

public:
	JoltSliderJoint3D(const JoltJoint3D &p_old_joint, JoltBody3D *p_body_a, JoltBody3D *p_body_b, const Transform3D &p_local_ref_a, const Transform3D &p_local_ref_b);

	virtual PS3DE::JointType get_type() const override { return PS3DE::JOINT_TYPE_SLIDER; }

	double get_param(PS3DE::SliderJointParam p_param) const;
	void set_param(PS3DE::SliderJointParam p_param, double p_value);

	double get_jolt_param(JoltParameter p_param) const;
	void set_jolt_param(JoltParameter p_param, double p_value);

	bool get_jolt_flag(JoltFlag p_flag) const;
	void set_jolt_flag(JoltFlag p_flag, bool p_enabled);

	float get_applied_force() const;
	float get_applied_torque() const;

	virtual void rebuild() override;
};
