/**************************************************************************/
/*  physics_testmotion_query_result_planar.cpp                                */

#include "physics_testmotion_query_result_planar.h"

#include "core/object/class_registry.h"

Vector2 PhysicsTestMotionResult2D::get_travel() const {
	return result.travel;
}

Vector2 PhysicsTestMotionResult2D::get_remainder() const {
	return result.remainder;
}

Vector2 PhysicsTestMotionResult2D::get_collision_point() const {
	return result.collision_point;
}

Vector2 PhysicsTestMotionResult2D::get_collision_normal() const {
	return result.collision_normal;
}

Vector2 PhysicsTestMotionResult2D::get_collider_velocity() const {
	return result.collider_velocity;
}

ObjectID PhysicsTestMotionResult2D::get_collider_id() const {
	return result.collider_id;
}

RID PhysicsTestMotionResult2D::get_collider_rid() const {
	return result.collider;
}

Object *PhysicsTestMotionResult2D::get_collider() const {
	return ObjectRegistry::get_instance(result.collider_id);
}

int PhysicsTestMotionResult2D::get_collider_shape() const {
	return result.collider_shape;
}

int PhysicsTestMotionResult2D::get_collision_local_shape() const {
	return result.collision_local_shape;
}

real_t PhysicsTestMotionResult2D::get_collision_depth() const {
	return result.collision_depth;
}

real_t PhysicsTestMotionResult2D::get_collision_safe_fraction() const {
	return result.collision_safe_fraction;
}

real_t PhysicsTestMotionResult2D::get_collision_unsafe_fraction() const {
	return result.collision_unsafe_fraction;
}

void PhysicsTestMotionResult2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_travel"), &PhysicsTestMotionResult2D::get_travel);
	ClassRegistry::bind_method(D_METHOD("get_remainder"), &PhysicsTestMotionResult2D::get_remainder);
	ClassRegistry::bind_method(D_METHOD("get_collision_point"), &PhysicsTestMotionResult2D::get_collision_point);
	ClassRegistry::bind_method(D_METHOD("get_collision_normal"), &PhysicsTestMotionResult2D::get_collision_normal);
	ClassRegistry::bind_method(D_METHOD("get_collider_velocity"), &PhysicsTestMotionResult2D::get_collider_velocity);
	ClassRegistry::bind_method(D_METHOD("get_collider_id"), &PhysicsTestMotionResult2D::get_collider_id);
	ClassRegistry::bind_method(D_METHOD("get_collider_rid"), &PhysicsTestMotionResult2D::get_collider_rid);
	ClassRegistry::bind_method(D_METHOD("get_collider"), &PhysicsTestMotionResult2D::get_collider);
	ClassRegistry::bind_method(D_METHOD("get_collider_shape"), &PhysicsTestMotionResult2D::get_collider_shape);
	ClassRegistry::bind_method(D_METHOD("get_collision_local_shape"), &PhysicsTestMotionResult2D::get_collision_local_shape);
	ClassRegistry::bind_method(D_METHOD("get_collision_depth"), &PhysicsTestMotionResult2D::get_collision_depth);
	ClassRegistry::bind_method(D_METHOD("get_collision_safe_fraction"), &PhysicsTestMotionResult2D::get_collision_safe_fraction);
	ClassRegistry::bind_method(D_METHOD("get_collision_unsafe_fraction"), &PhysicsTestMotionResult2D::get_collision_unsafe_fraction);
}
