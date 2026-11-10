/**************************************************************************/
/*  cone_twist_joint_spatial.cpp                                               */

#include "cone_twist_joint_spatial.h"

#include "core/object/class_registry.h"

void ConeTwistJoint3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_param", "param", "value"), &ConeTwistJoint3D::set_param);
	ClassRegistry::bind_method(D_METHOD("get_param", "param"), &ConeTwistJoint3D::get_param);

	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "swing_span", PROPERTY_HINT_RANGE, "-180,180,0.1,radians_as_degrees"), "set_param", "get_param", PARAM_SWING_SPAN);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "twist_span", PROPERTY_HINT_RANGE, "-40000,40000,0.1,radians_as_degrees"), "set_param", "get_param", PARAM_TWIST_SPAN);

	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "bias", PROPERTY_HINT_RANGE, "0.01,16.0,0.01"), "set_param", "get_param", PARAM_BIAS);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "softness", PROPERTY_HINT_RANGE, "0.01,16.0,0.01"), "set_param", "get_param", PARAM_SOFTNESS);
	ADD_PROPERTYI(PropertyInfo(Variant::FLOAT, "relaxation", PROPERTY_HINT_RANGE, "0.01,16.0,0.01"), "set_param", "get_param", PARAM_RELAXATION);

	BIND_ENUM_CONSTANT(PARAM_SWING_SPAN);
	BIND_ENUM_CONSTANT(PARAM_TWIST_SPAN);
	BIND_ENUM_CONSTANT(PARAM_BIAS);
	BIND_ENUM_CONSTANT(PARAM_SOFTNESS);
	BIND_ENUM_CONSTANT(PARAM_RELAXATION);
	BIND_ENUM_CONSTANT(PARAM_MAX);
}

void ConeTwistJoint3D::set_param(Param p_param, real_t p_value) {
	ERR_FAIL_INDEX(p_param, PARAM_MAX);
	params[p_param] = p_value;
	if (is_configured()) {
		PhysicsSystem3D::get_singleton()->cone_twist_joint_set_param(get_rid(), PS3DE::ConeTwistJointParam(p_param), p_value);
	}

	update_gizmos();
}

real_t ConeTwistJoint3D::get_param(Param p_param) const {
	ERR_FAIL_INDEX_V(p_param, PARAM_MAX, 0);
	return params[p_param];
}

void ConeTwistJoint3D::_configure_joint(RID p_joint, PhysicsBody3D *body_a, PhysicsBody3D *body_b) {
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

	PhysicsSystem3D::get_singleton()->joint_make_cone_twist(p_joint, body_a->get_rid(), local_a, body_b ? body_b->get_rid() : RID(), local_b);
	for (int i = 0; i < PARAM_MAX; i++) {
		PhysicsSystem3D::get_singleton()->cone_twist_joint_set_param(p_joint, PS3DE::ConeTwistJointParam(i), params[i]);
	}
}

ConeTwistJoint3D::ConeTwistJoint3D() {
	params[PARAM_SWING_SPAN] = Math::PI * 0.25;
	params[PARAM_TWIST_SPAN] = Math::PI;
	params[PARAM_BIAS] = 0.3;
	params[PARAM_SOFTNESS] = 0.8;
	params[PARAM_RELAXATION] = 1.0;
}
