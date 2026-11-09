/**************************************************************************/
/*  test_generic_6dof_joint_spatial.cpp                                        */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_generic_6dof_joint_spatial)

#ifndef PHYSICS_SPATIAL_DISABLED

#include "scene/spatial/physics/joints/generic_6dof_joint_spatial.h"
#include "systems/physics_spatial/physics_system_spatial.h"

#include <cfloat>

namespace TestGeneric6DOFJoint3D {

TEST_CASE("[Stage][Generic6DOFJoint3D] Quaternion angular target is normalized and stored") {
	Generic6DOFJoint3D *joint = memnew(Generic6DOFJoint3D);

	const Quaternion target_rotation(Vector3(0, 1, 0), 0.5);
	joint->set_angular_target_rotation(target_rotation * 2.0);

	CHECK(joint->get_angular_target_rotation().is_normalized());
	CHECK(joint->get_angular_target_rotation().is_equal_approx(target_rotation));
	CHECK(joint->has_target_rotation());

	memdelete(joint);
}

TEST_CASE("[Stage][Generic6DOFJoint3D] Euler equilibrium setter clears stored quaternion target") {
	Generic6DOFJoint3D *joint = memnew(Generic6DOFJoint3D);

	joint->set_angular_target_rotation(Quaternion(Vector3(0, 0, 1), 0.25));
	CHECK(joint->has_target_rotation());

	joint->set_param_x(Generic6DOFJoint3D::PARAM_ANGULAR_SPRING_EQUILIBRIUM_POINT, 0.1);
	CHECK_FALSE(joint->has_target_rotation());

	memdelete(joint);
}

TEST_CASE("[Stage][Generic6DOFJoint3D] clear_angular_target_rotation resets state") {
	Generic6DOFJoint3D *joint = memnew(Generic6DOFJoint3D);

	joint->set_angular_target_rotation(Quaternion(Vector3(1, 0, 0), 0.5));
	CHECK(joint->has_target_rotation());

	joint->clear_angular_target_rotation();
	CHECK_FALSE(joint->has_target_rotation());
	ERR_PRINT_OFF;
	CHECK(joint->get_angular_target_rotation().is_equal_approx(Quaternion()));
	ERR_PRINT_ON;

	memdelete(joint);
}

TEST_CASE("[Stage][Generic6DOFJoint3D] Getter returns identity before configuration when no explicit target is set") {
	Generic6DOFJoint3D *joint = memnew(Generic6DOFJoint3D);

	joint->set_param_x(Generic6DOFJoint3D::PARAM_ANGULAR_SPRING_EQUILIBRIUM_POINT, 0.3);
	joint->set_param_y(Generic6DOFJoint3D::PARAM_ANGULAR_SPRING_EQUILIBRIUM_POINT, -0.2);
	joint->set_param_z(Generic6DOFJoint3D::PARAM_ANGULAR_SPRING_EQUILIBRIUM_POINT, 0.4);

	CHECK_FALSE(joint->has_target_rotation());
	ERR_PRINT_OFF;
	CHECK(joint->get_angular_target_rotation().is_equal_approx(Quaternion()));
	ERR_PRINT_ON;

	memdelete(joint);
}

TEST_CASE("[Stage][Generic6DOFJoint3D] Explicit quaternion target shadows Euler equilibrium derivation") {
	Generic6DOFJoint3D *joint = memnew(Generic6DOFJoint3D);

	joint->set_param_x(Generic6DOFJoint3D::PARAM_ANGULAR_SPRING_EQUILIBRIUM_POINT, 0.3);
	const Quaternion explicit_target(Vector3(0, 1, 0), 0.7);
	joint->set_angular_target_rotation(explicit_target);

	CHECK(joint->has_target_rotation());
	CHECK(joint->get_angular_target_rotation().is_equal_approx(explicit_target));

	memdelete(joint);
}

static RID make_configured_generic_6dof_joint(PhysicsSystem3D *p_server, RID &r_body_a, RID &r_body_b) {
	r_body_a = p_server->body_create();
	r_body_b = p_server->body_create();

	const RID joint = p_server->joint_create();
	p_server->joint_make_generic_6dof(joint, r_body_a, Transform3D(), r_body_b, Transform3D());
	return joint;
}

TEST_CASE("[Stage][Generic6DOFJoint3D] Drive force and torque limits params round-trip") {
	Generic6DOFJoint3D *joint = memnew(Generic6DOFJoint3D);

	joint->set_param_x(Generic6DOFJoint3D::PARAM_LINEAR_DRIVE_FORCE_LIMIT, 123.0);
	joint->set_param_x(Generic6DOFJoint3D::PARAM_ANGULAR_DRIVE_TORQUE_LIMIT, 45.0);

	CHECK(joint->get_param_x(Generic6DOFJoint3D::PARAM_LINEAR_DRIVE_FORCE_LIMIT) == 123.0);
	CHECK(joint->get_param_x(Generic6DOFJoint3D::PARAM_ANGULAR_DRIVE_TORQUE_LIMIT) == 45.0);

	memdelete(joint);
}

TEST_CASE("[Stage][Generic6DOFJoint3D] Drive force and torque limits defaults are unlimited") {
	Generic6DOFJoint3D *joint = memnew(Generic6DOFJoint3D);

	CHECK(joint->get_param_x(Generic6DOFJoint3D::PARAM_LINEAR_DRIVE_FORCE_LIMIT) == FLT_MAX);
	CHECK(joint->get_param_y(Generic6DOFJoint3D::PARAM_LINEAR_DRIVE_FORCE_LIMIT) == FLT_MAX);
	CHECK(joint->get_param_z(Generic6DOFJoint3D::PARAM_LINEAR_DRIVE_FORCE_LIMIT) == FLT_MAX);

	CHECK(joint->get_param_x(Generic6DOFJoint3D::PARAM_ANGULAR_DRIVE_TORQUE_LIMIT) == FLT_MAX);
	CHECK(joint->get_param_y(Generic6DOFJoint3D::PARAM_ANGULAR_DRIVE_TORQUE_LIMIT) == FLT_MAX);
	CHECK(joint->get_param_z(Generic6DOFJoint3D::PARAM_ANGULAR_DRIVE_TORQUE_LIMIT) == FLT_MAX);

	memdelete(joint);
}

TEST_CASE("[Stage][PhysicsSystem3D][Generic6DOFJoint3D] Drive force and torque limits params round-trip per axis") {
	PhysicsSystem3D *physics_system = PhysicsSystem3D::get_singleton();
	REQUIRE(physics_system != nullptr);

	RID body_a;
	RID body_b;
	RID joint = make_configured_generic_6dof_joint(physics_system, body_a, body_b);

	constexpr Vector3::Axis axes[] = { Vector3::AXIS_X, Vector3::AXIS_Y, Vector3::AXIS_Z };
	constexpr real_t linear_values[] = { 12.0, 34.0, 56.0 };
	constexpr real_t angular_values[] = { 7.0, 8.0, 9.0 };

	for (int i = 0; i < 3; i++) {
		physics_system->generic_6dof_joint_set_param(joint, axes[i], PS3DE::G6DOF_JOINT_LINEAR_DRIVE_FORCE_LIMIT, linear_values[i]);
		physics_system->generic_6dof_joint_set_param(joint, axes[i], PS3DE::G6DOF_JOINT_ANGULAR_DRIVE_TORQUE_LIMIT, angular_values[i]);
	}

	for (int i = 0; i < 3; i++) {
		CHECK_EQ(physics_system->generic_6dof_joint_get_param(joint, axes[i], PS3DE::G6DOF_JOINT_LINEAR_DRIVE_FORCE_LIMIT), doctest::Approx(linear_values[i]));
		CHECK_EQ(physics_system->generic_6dof_joint_get_param(joint, axes[i], PS3DE::G6DOF_JOINT_ANGULAR_DRIVE_TORQUE_LIMIT), doctest::Approx(angular_values[i]));
	}

	physics_system->free_rid(joint);
	physics_system->free_rid(body_a);
	physics_system->free_rid(body_b);
}

TEST_CASE("[Stage][PhysicsSystem3D][Generic6DOFJoint3D] Drive force and torque limits defaults are unlimited") {
	PhysicsSystem3D *physics_system = PhysicsSystem3D::get_singleton();
	REQUIRE(physics_system != nullptr);

	RID body_a;
	RID body_b;
	RID joint = make_configured_generic_6dof_joint(physics_system, body_a, body_b);

	CHECK_EQ(physics_system->generic_6dof_joint_get_param(joint, Vector3::AXIS_X, PS3DE::G6DOF_JOINT_LINEAR_DRIVE_FORCE_LIMIT), FLT_MAX);
	CHECK_EQ(physics_system->generic_6dof_joint_get_param(joint, Vector3::AXIS_Y, PS3DE::G6DOF_JOINT_LINEAR_DRIVE_FORCE_LIMIT), FLT_MAX);
	CHECK_EQ(physics_system->generic_6dof_joint_get_param(joint, Vector3::AXIS_Z, PS3DE::G6DOF_JOINT_LINEAR_DRIVE_FORCE_LIMIT), FLT_MAX);

	CHECK_EQ(physics_system->generic_6dof_joint_get_param(joint, Vector3::AXIS_X, PS3DE::G6DOF_JOINT_ANGULAR_DRIVE_TORQUE_LIMIT), FLT_MAX);
	CHECK_EQ(physics_system->generic_6dof_joint_get_param(joint, Vector3::AXIS_Y, PS3DE::G6DOF_JOINT_ANGULAR_DRIVE_TORQUE_LIMIT), FLT_MAX);
	CHECK_EQ(physics_system->generic_6dof_joint_get_param(joint, Vector3::AXIS_Z, PS3DE::G6DOF_JOINT_ANGULAR_DRIVE_TORQUE_LIMIT), FLT_MAX);

	physics_system->free_rid(joint);
	physics_system->free_rid(body_a);
	physics_system->free_rid(body_b);
}

} // namespace TestGeneric6DOFJoint3D

#endif // PHYSICS_SPATIAL_DISABLED
