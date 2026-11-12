/**************************************************************************/
/*  test_rigid_body_sleep.cpp                                                 */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_rigid_body_sleep)

#include "scene/stage/stage.h"
#include "scene/stage/window.h"

#ifndef PHYSICS_PLANAR_DISABLED
#include "scene/planar/physics/collision_shape_planar.h"
#include "scene/planar/physics/rigid_body_planar.h"
#include "scene/resources/planar/circle_shape_planar.h"
#include "systems/physics_planar/physics_system_planar.h"
#endif // PHYSICS_PLANAR_DISABLED

#ifndef PHYSICS_SPATIAL_DISABLED
#include "scene/resources/spatial/sphere_shape_spatial.h"
#include "scene/spatial/physics/collision_shape_spatial.h"
#include "scene/spatial/physics/rigid_body_spatial.h"
#include "systems/physics_spatial/physics_system_spatial.h"
#endif // PHYSICS_SPATIAL_DISABLED

namespace TestRigidBodySleep {

#ifndef PHYSICS_PLANAR_DISABLED
void step_frame_2d() {
	PhysicsSystem2D *physics = PhysicsSystem2D::get_singleton();
	Stage::get_singleton()->flush_transform_notifications();
	physics->end_sync();
	physics->step(1.0 / 60.0);
	physics->sync();
	physics->flush_queries();
}

TEST_CASE("[Stage][RigidBody2D] A body set to sleep before it enters the stage stays asleep") {
	RigidBody2D *body = memnew(RigidBody2D);
	CollisionShape2D *shape = memnew(CollisionShape2D);
	Ref<CircleShape2D> circle = memnew(CircleShape2D);
	shape->set_shape(circle);
	body->add_child(shape);
	body->set_position(Vector2(50, 40));
	body->set_sleeping(true);
	Stage::get_singleton()->get_root()->add_child(body);

	for (int i = 0; i < 5; i++) {
		step_frame_2d();
	}
	CHECK(PhysicsSystem2D::get_singleton()->body_get_state(body->get_rid(), PS2DE::BODY_STATE_SLEEPING));
	CHECK(body->get_global_position().y == doctest::Approx(40.0));

	memdelete(body);
}

TEST_CASE("[Stage][RigidBody2D] A sleeping body wakes when an impulse is applied") {
	RigidBody2D *body = memnew(RigidBody2D);
	CollisionShape2D *shape = memnew(CollisionShape2D);
	Ref<CircleShape2D> circle = memnew(CircleShape2D);
	shape->set_shape(circle);
	body->add_child(shape);
	body->set_sleeping(true);
	Stage::get_singleton()->get_root()->add_child(body);
	step_frame_2d();

	body->apply_central_impulse(Vector2(100, 0));
	step_frame_2d();
	CHECK_FALSE(PhysicsSystem2D::get_singleton()->body_get_state(body->get_rid(), PS2DE::BODY_STATE_SLEEPING));

	memdelete(body);
}
#endif // PHYSICS_PLANAR_DISABLED

#ifndef PHYSICS_SPATIAL_DISABLED
void step_frame_3d() {
	PhysicsSystem3D *physics = PhysicsSystem3D::get_singleton();
	Stage::get_singleton()->flush_transform_notifications();
	physics->end_sync();
	physics->step(1.0 / 60.0);
	physics->sync();
	physics->flush_queries();
}

TEST_CASE("[Stage][RigidBody3D] A body set to sleep before it enters the stage stays asleep") {
	RigidBody3D *body = memnew(RigidBody3D);
	CollisionShape3D *shape = memnew(CollisionShape3D);
	Ref<SphereShape3D> sphere = memnew(SphereShape3D);
	shape->set_shape(sphere);
	body->add_child(shape);
	body->set_position(Vector3(50, 40, 0));
	body->set_sleeping(true);
	Stage::get_singleton()->get_root()->add_child(body);

	for (int i = 0; i < 5; i++) {
		step_frame_3d();
	}
	CHECK(PhysicsSystem3D::get_singleton()->body_get_state(body->get_rid(), PS3DE::BODY_STATE_SLEEPING));
	CHECK(body->get_global_position().y == doctest::Approx(40.0));

	memdelete(body);
}
#endif // PHYSICS_SPATIAL_DISABLED

} // namespace TestRigidBodySleep
