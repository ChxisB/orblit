/**************************************************************************/
/*  pin_joint_spatial.cpp                                                      */

#include "pin_joint_spatial.h"

#include "core/object/class_registry.h"

void PinJoint3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_param", "param", "value"), &PinJoint3D::set_param);
	ClassRegistry::bind_method(D_METHOD("get_param", "param"), &PinJoint3D::get_param);

	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "params/bias", PROPERTY_HINT_RANGE, "0.01,0.99,0.01"), "set_param", "get_param", PARAM_BIAS);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "params/damping", PROPERTY_HINT_RANGE, "0.01,8.0,0.01"), "set_param", "get_param", PARAM_DAMPING);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "params/impulse_clamp", PROPERTY_HINT_RANGE, "0.0,64.0,0.01"), "set_param", "get_param", PARAM_IMPULSE_CLAMP);

	BIND_ENUM_CONSTANT(PARAM_BIAS);
	BIND_ENUM_CONSTANT(PARAM_DAMPING);
	BIND_ENUM_CONSTANT(PARAM_IMPULSE_CLAMP);
}

void PinJoint3D::set_param(Param p_param, real_t p_value) {
	ERR_FAIL_INDEX(p_param, 3);
	params[p_param] = p_value;
	if (is_configured()) {
		PhysicsSystem3D::get_singleton()->pin_joint_set_param(get_rid(), PS3DE::PinJointParam(p_param), p_value);
	}
}

real_t PinJoint3D::get_param(Param p_param) const {
	ERR_FAIL_INDEX_V(p_param, 3, 0);
	return params[p_param];
}

void PinJoint3D::_configure_joint(RID p_joint, PhysicsBody3D *body_a, PhysicsBody3D *body_b) {
	Vector3 pinpos = get_global_transform().origin;
	Vector3 local_a = body_a->to_local(pinpos);
	Vector3 local_b;

	if (body_b) {
		local_b = body_b->to_local(pinpos);
	} else {
		local_b = pinpos;
	}

	PhysicsSystem3D::get_singleton()->joint_make_pin(p_joint, body_a->get_rid(), local_a, body_b ? body_b->get_rid() : RID(), local_b);
	for (int i = 0; i < 3; i++) {
		PhysicsSystem3D::get_singleton()->pin_joint_set_param(p_joint, PS3DE::PinJointParam(i), params[i]);
	}
}

PinJoint3D::PinJoint3D() {
	params[PARAM_BIAS] = 0.3;
	params[PARAM_DAMPING] = 1;
	params[PARAM_IMPULSE_CLAMP] = 0;
}
