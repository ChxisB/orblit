/**************************************************************************/
/*  jolt_cone_twist_joint_spatial.h                                            */

#pragma once

#include "../jolt_physics_system_spatial.h"
#include "jolt_joint_spatial.h"

#include <Jolt/Jolt.h>

#include <Jolt/Physics/Body/Body.h>

class JoltConeTwistJoint3D final : public JoltJoint3D {
	typedef PS3DE::ConeTwistJointParam Parameter;
	typedef JoltPhysicsSystem3D::ConeTwistJointParamJolt JoltParameter;
	typedef JoltPhysicsSystem3D::ConeTwistJointFlagJolt JoltFlag;

	double swing_limit_span = 0.0;
	double twist_limit_span = 0.0;

	double swing_motor_target_speed_y = 0.0;
	double swing_motor_target_speed_z = 0.0;
	double twist_motor_target_speed = 0.0;

	double swing_motor_max_torque = FLT_MAX;
	double twist_motor_max_torque = FLT_MAX;

	bool swing_limit_enabled = true;
	bool twist_limit_enabled = true;

	bool swing_motor_enabled = false;
	bool twist_motor_enabled = false;

	JPH::Constraint *_build_swing_twist(JPH::Body *p_jolt_body_a, JPH::Body *p_jolt_body_b, const Transform3D &p_shifted_ref_a, const Transform3D &p_shifted_ref_b, float p_swing_limit_span, float p_twist_limit_span) const;

	void _update_swing_motor_state();
	void _update_twist_motor_state();
	void _update_motor_velocity();
	void _update_swing_motor_limit();
	void _update_twist_motor_limit();

	void _limits_changed();
	void _swing_motor_state_changed();
	void _twist_motor_state_changed();
	void _motor_velocity_changed();
	void _swing_motor_limit_changed();
	void _twist_motor_limit_changed();

public:
	JoltConeTwistJoint3D(const JoltJoint3D &p_old_joint, JoltBody3D *p_body_a, JoltBody3D *p_body_b, const Transform3D &p_local_ref_a, const Transform3D &p_local_ref_b);

	virtual PS3DE::JointType get_type() const override { return PS3DE::JOINT_TYPE_CONE_TWIST; }

	double get_param(PS3DE::ConeTwistJointParam p_param) const;
	void set_param(PS3DE::ConeTwistJointParam p_param, double p_value);

	double get_jolt_param(JoltParameter p_param) const;
	void set_jolt_param(JoltParameter p_param, double p_value);

	bool get_jolt_flag(JoltFlag p_flag) const;
	void set_jolt_flag(JoltFlag p_flag, bool p_enabled);

	float get_applied_force() const;
	float get_applied_torque() const;

	virtual void rebuild() override;
};
