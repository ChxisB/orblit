/**************************************************************************/
/*  jolt_pin_joint_spatial.h                                                   */

#pragma once

#include "jolt_joint_spatial.h"

#include <Jolt/Jolt.h>

#include <Jolt/Physics/Constraints/SliderConstraint.h>

class JoltPinJoint3D final : public JoltJoint3D {
	static JPH::Constraint *_build_pin(JPH::Body *p_jolt_body_a, JPH::Body *p_jolt_body_b, const Transform3D &p_shifted_ref_a, const Transform3D &p_shifted_ref_b);

	void _points_changed();

public:
	JoltPinJoint3D(const JoltJoint3D &p_old_joint, JoltBody3D *p_body_a, JoltBody3D *p_body_b, const Vector3 &p_local_a, const Vector3 &p_local_b);

	virtual PS3DE::JointType get_type() const override { return PS3DE::JOINT_TYPE_PIN; }

	Vector3 get_local_a() const { return local_ref_a.origin; }
	void set_local_a(const Vector3 &p_local_a);

	Vector3 get_local_b() const { return local_ref_b.origin; }
	void set_local_b(const Vector3 &p_local_b);

	double get_param(PS3DE::PinJointParam p_param) const;
	void set_param(PS3DE::PinJointParam p_param, double p_value);

	float get_applied_force() const;

	virtual void rebuild() override;
};
