/**************************************************************************/
/*  test_rigid_body_parent_planar.cpp                                         */

#include "tests/test_macros.h"

TEST_FORCE_LINK(test_rigid_body_parent_planar)

#ifndef PHYSICS_PLANAR_DISABLED

#include "scene/planar/entity_planar.h"
#include "scene/planar/physics/collision_shape_planar.h"
#include "scene/planar/physics/rigid_body_planar.h"
#include "scene/resources/planar/circle_shape_planar.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "systems/physics_planar/physics_system_planar.h"

namespace TestRigidBodyParent2D {

void step_frame() {
	PhysicsSystem2D *physics = PhysicsSystem2D::get_singleton();
	Stage::get_singleton()->flush_transform_notifications();
	physics->end_sync();
	physics->step(1.0 / 60.0);
	physics->sync();
	physics->flush_queries();
}

TEST_CASE("[Stage][RigidBody2D] A body is carried by a parent that moves after a physics sync") {
	Entity2D *parent = memnew(Entity2D);
	RigidBody2D *body = memnew(RigidBody2D);
	CollisionShape2D *shape = memnew(CollisionShape2D);
	Ref<CircleShape2D> circle = memnew(CircleShape2D);
	shape->set_shape(circle);
	body->add_child(shape);
	body->set_gravity_scale(0);
	body->set_linear_velocity(Vector2(10, 0));
	parent->add_child(body);
	Stage::get_singleton()->get_root()->add_child(parent);

	// Nothing may read the global transform here, because a read refreshes the cache.
	step_frame();
	step_frame();

	parent->set_position(Vector2(100, 0));
	step_frame();
	step_frame();
	CHECK(body->get_global_position().x > 95.0);

	memdelete(parent);
}

} // namespace TestRigidBodyParent2D

#endif // PHYSICS_PLANAR_DISABLED
