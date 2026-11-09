/**************************************************************************/
/*  hinge_joint_spatial.cpp                                                    */

#include "hinge_joint_spatial.h"

#include "core/object/class_registry.h"

void HingeJoint3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_param", "param", "value"), &HingeJoint3D::set_param);
	ClassRegistry::bind_method(D_METHOD("get_param", "param"), &HingeJoint3D::get_param);

	ClassRegistry::bind_method(D_METHOD("set_flag", "flag", "enabled"), &HingeJoint3D::set_flag);
	ClassRegistry::bind_method(D_METHOD("get_flag", "flag"), &HingeJoint3D::get_flag);

	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "params/bias", PROPERTY_HINT_RANGE, "0.00,0.99,0.01"), "set_param", "get_param", PARAM_BIAS);

	ADD_PROPERTYI(PropertyInfo(Variant::BOOL, "angular_limit/enable"), "set_flag", "get_flag", FLAG_USE_LIMIT);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "angular_limit/upper", PROPERTY_HINT_RANGE, "-180,180,0.1,radians_as_degrees"), "set_param", "get_param", PARAM_LIMIT_UPPER);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "angular_limit/lower", PROPERTY_HINT_RANGE, "-180,180,0.1,radians_as_degrees"), "set_param", "get_param", PARAM_LIMIT_LOWER);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "angular_limit/bias", PROPERTY_HINT_RANGE, "0.01,0.99,0.01"), "set_param", "get_param", PARAM_LIMIT_BIAS);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "angular_limit/relaxation", PROPERTY_HINT_RANGE, "0.01,16,0.01"), "set_param", "get_param", PARAM_LIMIT_RELAXATION);

	ADD_PROPERTYI(PropertyInfo(Variant::BOOL, "motor/enable"), "set_flag", "get_flag", FLAG_ENABLE_MOTOR);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "motor/target_velocity", PROPERTY_HINT_RANGE, U"-200,200,0.01,or_greater,or_less,radians_as_degrees,suffix:\u00B0/s"), "set_param", "get_param", PARAM_MOTOR_TARGET_VELOCITY);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "motor/max_impulse", PROPERTY_HINT_RANGE, "0.01,1024,0.01"), "set_param", "get_param", PARAM_MOTOR_MAX_IMPULSE);

	BIND_ENUM_CONSTANT(PARAM_BIAS);
	BIND_ENUM_CONSTANT(PARAM_LIMIT_UPPER);
	BIND_ENUM_CONSTANT(PARAM_LIMIT_LOWER);
	BIND_ENUM_CONSTANT(PARAM_LIMIT_BIAS);
	BIND_ENUM_CONSTANT(PARAM_LIMIT_RELAXATION);
	BIND_ENUM_CONSTANT(PARAM_MOTOR_TARGET_VELOCITY);
	BIND_ENUM_CONSTANT(PARAM_MOTOR_MAX_IMPULSE);
	BIND_ENUM_CONSTANT(PARAM_MAX);

	BIND_ENUM_CONSTANT(FLAG_USE_LIMIT);
	BIND_ENUM_CONSTANT(FLAG_ENABLE_MOTOR);
	BIND_ENUM_CONSTANT(FLAG_MAX);
}

void HingeJoint3D::set_param(Param p_param, real_t p_value) {
	ERR_FAIL_INDEX(p_param, PARAM_MAX);
	params[p_param] = p_value;
	if (is_configured()) {
		PhysicsSystem3D::get_singleton()->hinge_joint_set_param(get_rid(), PS3DE::HingeJointParam(p_param), p_value);
	}

	update_gizmos();
}

real_t HingeJoint3D::get_param(Param p_param) const {
	ERR_FAIL_INDEX_V(p_param, PARAM_MAX, 0);
	return params[p_param];
}

void HingeJoint3D::set_flag(Flag p_flag, bool p_value) {
	ERR_FAIL_INDEX(p_flag, FLAG_MAX);
	flags[p_flag] = p_value;
	if (is_configured()) {
		PhysicsSystem3D::get_singleton()->hinge_joint_set_flag(get_rid(), PS3DE::HingeJointFlag(p_flag), p_value);
	}

	update_gizmos();
}

bool HingeJoint3D::get_flag(Flag p_flag) const {
	ERR_FAIL_INDEX_V(p_flag, FLAG_MAX, false);
	return flags[p_flag];
}

void HingeJoint3D::_configure_joint(RID p_joint, PhysicsBody3D *body_a, PhysicsBody3D *body_b) {
	Transform3D gt = get_global_transform();
	Transform3D ainv = body_a->get_global_transform().affine_inverse();

	Transform3D local_a = ainv * gt;
	local_a.orthonormalize();
	Transform3D local_b = gt;

	if (body_b) {
		Transform3D binv = body_b->get_global_transform().affine_inverse();
		local_b = binv * gt;
	}

	local_b.orthonormalize();

	PhysicsSystem3D::get_singleton()->joint_make_hinge(p_joint, body_a->get_rid(), local_a, body_b ? body_b->get_rid() : RID(), local_b);
	for (int i = 0; i < PARAM_MAX; i++) {
		PhysicsSystem3D::get_singleton()->hinge_joint_set_param(p_joint, PS3DE::HingeJointParam(i), params[i]);
	}
	for (int i = 0; i < FLAG_MAX; i++) {
		set_flag(Flag(i), flags[i]);
		PhysicsSystem3D::get_singleton()->hinge_joint_set_flag(p_joint, PS3DE::HingeJointFlag(i), flags[i]);
	}
}

HingeJoint3D::HingeJoint3D() {
	params[PARAM_BIAS] = 0.3;
	params[PARAM_LIMIT_UPPER] = Math::PI * 0.5;
	params[PARAM_LIMIT_LOWER] = -Math::PI * 0.5;
	params[PARAM_LIMIT_BIAS] = 0.3;
	params[PARAM_LIMIT_RELAXATION] = 1.0;
	params[PARAM_MOTOR_TARGET_VELOCITY] = 1;
	params[PARAM_MOTOR_MAX_IMPULSE] = 1;

	flags[FLAG_USE_LIMIT] = false;
	flags[FLAG_ENABLE_MOTOR] = false;
}
