/**************************************************************************/
/*  jolt_joint_spatial.h                                                       */

#pragma once

#include "core/math/transform_spatial.h"
#include "core/containers/rid.h"
#include "systems/physics_spatial/physics_system_spatial_enums.h"

#include <Jolt/Jolt.h>

#include <Jolt/Physics/Constraints/Constraint.h>

class JoltBody3D;
class JoltSpace3D;

class JoltJoint3D {
protected:
	bool enabled = true;
	bool collision_disabled = false;

	int velocity_iterations = 0;
	int position_iterations = 0;
	int solver_priority = 1;

	JPH::Ref<JPH::Constraint> jolt_ref;

	JoltBody3D *body_a = nullptr;
	JoltBody3D *body_b = nullptr;

	RID rid;

	Transform3D local_ref_a;
	Transform3D local_ref_b;

	void _shift_reference_frames(const Vector3 &p_linear_shift, const Vector3 &p_angular_shift, Transform3D &r_shifted_ref_a, Transform3D &r_shifted_ref_b);

	void _wake_up_bodies();

	void _update_enabled();
	void _update_iterations();
	void _update_priority();

	void _update_joint();

	void _enabled_changed();
	void _iterations_changed();

	String _bodies_to_string() const;

public:
	JoltJoint3D() = default;
	JoltJoint3D(const JoltJoint3D &p_old_joint, JoltBody3D *p_body_a, JoltBody3D *p_body_b, const Transform3D &p_local_ref_a, const Transform3D &p_local_ref_b);
	virtual ~JoltJoint3D();

	virtual PS3DE::JointType get_type() const { return PS3DE::JOINT_TYPE_MAX; }

	RID get_rid() const { return rid; }
	void set_rid(const RID &p_rid) { rid = p_rid; }

	JoltSpace3D *get_space() const;

	JPH::Constraint *get_jolt_ref() const { return jolt_ref; }

	bool is_enabled() const { return enabled; }
	void set_enabled(bool p_enabled);

	int get_solver_priority() const { return solver_priority; }
	void set_solver_priority(int p_priority);

	int get_solver_velocity_iterations() const { return velocity_iterations; }
	void set_solver_velocity_iterations(int p_iterations);

	int get_solver_position_iterations() const { return position_iterations; }
	void set_solver_position_iterations(int p_iterations);

	bool is_collision_disabled() const { return collision_disabled; }
	void set_collision_disabled(bool p_disabled);

	void destroy();

	virtual void rebuild() {}
};
