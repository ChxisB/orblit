/**************************************************************************/
/*  generic_6dof_joint_spatial.h                                               */

#pragma once

#include "scene/spatial/physics/joints/joint_spatial.h"

class Generic6DOFJoint3D : public Joint3D {
	ORBLIT_CLASS(Generic6DOFJoint3D, Joint3D);

public:
	enum Param {
		PARAM_LINEAR_LOWER_LIMIT = PS3DE::G6DOF_JOINT_LINEAR_LOWER_LIMIT,
		PARAM_LINEAR_UPPER_LIMIT = PS3DE::G6DOF_JOINT_LINEAR_UPPER_LIMIT,
		PARAM_LINEAR_LIMIT_SOFTNESS = PS3DE::G6DOF_JOINT_LINEAR_LIMIT_SOFTNESS,
		PARAM_LINEAR_RESTITUTION = PS3DE::G6DOF_JOINT_LINEAR_RESTITUTION,
		PARAM_LINEAR_DAMPING = PS3DE::G6DOF_JOINT_LINEAR_DAMPING,
		PARAM_LINEAR_MOTOR_TARGET_VELOCITY = PS3DE::G6DOF_JOINT_LINEAR_MOTOR_TARGET_VELOCITY,
		PARAM_LINEAR_SPRING_STIFFNESS = PS3DE::G6DOF_JOINT_LINEAR_SPRING_STIFFNESS,
		PARAM_LINEAR_SPRING_DAMPING = PS3DE::G6DOF_JOINT_LINEAR_SPRING_DAMPING,
		PARAM_LINEAR_SPRING_EQUILIBRIUM_POINT = PS3DE::G6DOF_JOINT_LINEAR_SPRING_EQUILIBRIUM_POINT,
		PARAM_ANGULAR_LOWER_LIMIT = PS3DE::G6DOF_JOINT_ANGULAR_LOWER_LIMIT,
		PARAM_ANGULAR_UPPER_LIMIT = PS3DE::G6DOF_JOINT_ANGULAR_UPPER_LIMIT,
		PARAM_ANGULAR_LIMIT_SOFTNESS = PS3DE::G6DOF_JOINT_ANGULAR_LIMIT_SOFTNESS,
		PARAM_ANGULAR_DAMPING = PS3DE::G6DOF_JOINT_ANGULAR_DAMPING,
		PARAM_ANGULAR_RESTITUTION = PS3DE::G6DOF_JOINT_ANGULAR_RESTITUTION,
		PARAM_ANGULAR_FORCE_LIMIT = PS3DE::G6DOF_JOINT_ANGULAR_FORCE_LIMIT,
		PARAM_ANGULAR_ERP = PS3DE::G6DOF_JOINT_ANGULAR_ERP,
		PARAM_ANGULAR_MOTOR_TARGET_VELOCITY = PS3DE::G6DOF_JOINT_ANGULAR_MOTOR_TARGET_VELOCITY,
		PARAM_ANGULAR_SPRING_STIFFNESS = PS3DE::G6DOF_JOINT_ANGULAR_SPRING_STIFFNESS,
		PARAM_ANGULAR_SPRING_DAMPING = PS3DE::G6DOF_JOINT_ANGULAR_SPRING_DAMPING,
		PARAM_ANGULAR_SPRING_EQUILIBRIUM_POINT = PS3DE::G6DOF_JOINT_ANGULAR_SPRING_EQUILIBRIUM_POINT,
		PARAM_LINEAR_DRIVE_FORCE_LIMIT = PS3DE::G6DOF_JOINT_LINEAR_DRIVE_FORCE_LIMIT,
		PARAM_ANGULAR_DRIVE_TORQUE_LIMIT = PS3DE::G6DOF_JOINT_ANGULAR_DRIVE_TORQUE_LIMIT,
		PARAM_MAX = PS3DE::G6DOF_JOINT_MAX,
	};

	enum Flag {
		FLAG_ENABLE_LINEAR_LIMIT = PS3DE::G6DOF_JOINT_FLAG_ENABLE_LINEAR_LIMIT,
		FLAG_ENABLE_ANGULAR_LIMIT = PS3DE::G6DOF_JOINT_FLAG_ENABLE_ANGULAR_LIMIT,
		FLAG_ENABLE_LINEAR_SPRING = PS3DE::G6DOF_JOINT_FLAG_ENABLE_LINEAR_SPRING,
		FLAG_ENABLE_ANGULAR_SPRING = PS3DE::G6DOF_JOINT_FLAG_ENABLE_ANGULAR_SPRING,
		FLAG_ENABLE_ANGULAR_MOTOR = PS3DE::G6DOF_JOINT_FLAG_ENABLE_ANGULAR_MOTOR,
		FLAG_ENABLE_LINEAR_MOTOR = PS3DE::G6DOF_JOINT_FLAG_ENABLE_LINEAR_MOTOR,
		FLAG_MAX = PS3DE::G6DOF_JOINT_FLAG_MAX
	};

protected:
	real_t params_x[PARAM_MAX];
	bool flags_x[FLAG_MAX];
	real_t params_y[PARAM_MAX];
	bool flags_y[FLAG_MAX];
	real_t params_z[PARAM_MAX];
	bool flags_z[FLAG_MAX];
	Quaternion angular_target_rotation;
	bool has_angular_target_rotation = false;

	virtual void _configure_joint(RID p_joint, PhysicsBody3D *body_a, PhysicsBody3D *body_b) override;
	static void register_members();

	static bool _is_valid_angular_target_rotation(const Quaternion &p_target_rotation);

public:
	void set_param_x(Param p_param, real_t p_value);
	real_t get_param_x(Param p_param) const;

	void set_param_y(Param p_param, real_t p_value);
	real_t get_param_y(Param p_param) const;

	void set_param_z(Param p_param, real_t p_value);
	real_t get_param_z(Param p_param) const;

	void set_flag_x(Flag p_flag, bool p_enabled);
	bool get_flag_x(Flag p_flag) const;

	void set_flag_y(Flag p_flag, bool p_enabled);
	bool get_flag_y(Flag p_flag) const;

	void set_flag_z(Flag p_flag, bool p_enabled);
	bool get_flag_z(Flag p_flag) const;

	void set_angular_target_rotation(const Quaternion &p_target_rotation);
	Quaternion get_angular_target_rotation() const;
	bool has_target_rotation() const;
	void clear_angular_target_rotation();

	Generic6DOFJoint3D();
};

VARIANT_ENUM_CAST(Generic6DOFJoint3D::Param);
VARIANT_ENUM_CAST(Generic6DOFJoint3D::Flag);
