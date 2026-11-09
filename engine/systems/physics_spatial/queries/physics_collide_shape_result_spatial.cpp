/**************************************************************************/
/*  physics_collide_shape_result_spatial.cpp                                   */

#include "physics_collide_shape_result_spatial.h"

#include "core/object/class_registry.h"

PhysicsCollideShapeResult3D::PhysicsCollideShapeResult3D(int p_max_collisions) {
	ERR_FAIL_COND(p_max_collisions < 0);
	result.resize(2 * p_max_collisions);
}

int PhysicsCollideShapeResult3D::get_max_collisions() const {
	return result.size() / 2;
}

void PhysicsCollideShapeResult3D::set_max_collisions(int p_max_collisions) {
	ERR_FAIL_COND(p_max_collisions < 0);
	result.resize(2 * p_max_collisions);
	collision_count = MIN(collision_count, static_cast<int>(result.size() / 2));
}

int PhysicsCollideShapeResult3D::get_collision_count() const {
	return collision_count;
}

Vector3 PhysicsCollideShapeResult3D::get_point_on_queried_shape(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, collision_count, Vector3());
	return result[2 * p_collision_index];
}

Vector3 PhysicsCollideShapeResult3D::get_point_on_colliding_shape(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, collision_count, Vector3());
	return result[2 * p_collision_index + 1];
}

void PhysicsCollideShapeResult3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_max_collisions"), &PhysicsCollideShapeResult3D::get_max_collisions);
	ClassRegistry::bind_method(D_METHOD("set_max_collisions", "max_collisions"), &PhysicsCollideShapeResult3D::set_max_collisions);
	ClassRegistry::bind_method(D_METHOD("get_collision_count"), &PhysicsCollideShapeResult3D::get_collision_count);
	ClassRegistry::bind_method(D_METHOD("get_point_on_queried_shape", "collision_index"), &PhysicsCollideShapeResult3D::get_point_on_queried_shape);
	ClassRegistry::bind_method(D_METHOD("get_point_on_colliding_shape", "collision_index"), &PhysicsCollideShapeResult3D::get_point_on_colliding_shape);

	ADD_PROPERTY(PropertyInfo(Variant::INT, "max_collisions"), "set_max_collisions", "get_max_collisions");
}
