/**************************************************************************/
/*  physics_testmotion_query_result_spatial.cpp                                */

#include "physics_testmotion_query_result_spatial.h"

#include "core/object/class_registry.h"

Vector3 PhysicsTestMotionResult3D::get_travel() const {
	return result.travel;
}

Vector3 PhysicsTestMotionResult3D::get_remainder() const {
	return result.remainder;
}

real_t PhysicsTestMotionResult3D::get_collision_safe_fraction() const {
	return result.collision_safe_fraction;
}

real_t PhysicsTestMotionResult3D::get_collision_unsafe_fraction() const {
	return result.collision_unsafe_fraction;
}

int PhysicsTestMotionResult3D::get_collision_count() const {
	return result.collision_count;
}

Vector3 PhysicsTestMotionResult3D::get_collision_point(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, Vector3());
	return result.collisions[p_collision_index].position;
}

Vector3 PhysicsTestMotionResult3D::get_collision_normal(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, Vector3());
	return result.collisions[p_collision_index].normal;
}

Vector3 PhysicsTestMotionResult3D::get_collider_velocity(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, Vector3());
	return result.collisions[p_collision_index].collider_velocity;
}

ObjectID PhysicsTestMotionResult3D::get_collider_id(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, ObjectID());
	return result.collisions[p_collision_index].collider_id;
}

RID PhysicsTestMotionResult3D::get_collider_rid(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, RID());
	return result.collisions[p_collision_index].collider;
}

Object *PhysicsTestMotionResult3D::get_collider(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, nullptr);
	return ObjectRegistry::get_instance(result.collisions[p_collision_index].collider_id);
}

int PhysicsTestMotionResult3D::get_collider_shape(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, 0);
	return result.collisions[p_collision_index].collider_shape;
}

int PhysicsTestMotionResult3D::get_collision_local_shape(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, 0);
	return result.collisions[p_collision_index].local_shape;
}

real_t PhysicsTestMotionResult3D::get_collision_depth(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, 0.0);
	return result.collisions[p_collision_index].depth;
}

void PhysicsTestMotionResult3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_travel"), &PhysicsTestMotionResult3D::get_travel);
	ClassRegistry::bind_method(D_METHOD("get_remainder"), &PhysicsTestMotionResult3D::get_remainder);
	ClassRegistry::bind_method(D_METHOD("get_collision_safe_fraction"), &PhysicsTestMotionResult3D::get_collision_safe_fraction);
	ClassRegistry::bind_method(D_METHOD("get_collision_unsafe_fraction"), &PhysicsTestMotionResult3D::get_collision_unsafe_fraction);
	ClassRegistry::bind_method(D_METHOD("get_collision_count"), &PhysicsTestMotionResult3D::get_collision_count);
	ClassRegistry::bind_method(D_METHOD("get_collision_point", "collision_index"), &PhysicsTestMotionResult3D::get_collision_point, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collision_normal", "collision_index"), &PhysicsTestMotionResult3D::get_collision_normal, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collider_velocity", "collision_index"), &PhysicsTestMotionResult3D::get_collider_velocity, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collider_id", "collision_index"), &PhysicsTestMotionResult3D::get_collider_id, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collider_rid", "collision_index"), &PhysicsTestMotionResult3D::get_collider_rid, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collider", "collision_index"), &PhysicsTestMotionResult3D::get_collider, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collider_shape", "collision_index"), &PhysicsTestMotionResult3D::get_collider_shape, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collision_local_shape", "collision_index"), &PhysicsTestMotionResult3D::get_collision_local_shape, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collision_depth", "collision_index"), &PhysicsTestMotionResult3D::get_collision_depth, DEFVAL(0));
}
