/**************************************************************************/
/*  cone_twist_joint_spatial.h                                                 */

#pragma once

#include "scene/spatial/physics/joints/joint_spatial.h"

class ConeTwistJoint3D : public Joint3D {
	ORBLIT_CLASS(ConeTwistJoint3D, Joint3D);

public:
	enum Param {
		PARAM_SWING_SPAN,
		PARAM_TWIST_SPAN,
		PARAM_BIAS,
		PARAM_SOFTNESS,
		PARAM_RELAXATION,
		PARAM_MAX
	};

protected:
	real_t params[PARAM_MAX];
	virtual void _configure_joint(RID p_joint, PhysicsBody3D *body_a, PhysicsBody3D *body_b) override;
	static void register_members();

public:
	void set_param(Param p_param, real_t p_value);
	real_t get_param(Param p_param) const;

	ConeTwistJoint3D();
};

VARIANT_ENUM_CAST(ConeTwistJoint3D::Param);
