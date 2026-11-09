/**************************************************************************/
/*  physics_intersect_ray_result_planar.cpp                                   */

#include "physics_intersect_ray_result_planar.h"

#include "core/object/class_registry.h"

Vector2 PhysicsIntersectRayResult2D::get_position() const {
	return result.position;
}

Vector2 PhysicsIntersectRayResult2D::get_normal() const {
	return result.normal;
}

RID PhysicsIntersectRayResult2D::get_collider_rid() const {
	return result.rid;
}

ObjectID PhysicsIntersectRayResult2D::get_collider_id() const {
	return result.collider_id;
}

Object *PhysicsIntersectRayResult2D::get_collider() const {
	return result.get_collider();
}

int PhysicsIntersectRayResult2D::get_collider_shape() const {
	return result.shape;
}

void PhysicsIntersectRayResult2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_position"), &PhysicsIntersectRayResult2D::get_position);
	ClassRegistry::bind_method(D_METHOD("get_normal"), &PhysicsIntersectRayResult2D::get_normal);
	ClassRegistry::bind_method(D_METHOD("get_collider_rid"), &PhysicsIntersectRayResult2D::get_collider_rid);
	ClassRegistry::bind_method(D_METHOD("get_collider_id"), &PhysicsIntersectRayResult2D::get_collider_id);
	ClassRegistry::bind_method(D_METHOD("get_collider"), &PhysicsIntersectRayResult2D::get_collider);
	ClassRegistry::bind_method(D_METHOD("get_collider_shape"), &PhysicsIntersectRayResult2D::get_collider_shape);

	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "position"), "", "get_position");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "normal"), "", "get_normal");
	ADD_PROPERTY(PropertyInfo(Variant::RID, "collider_rid"), "", "get_collider_rid");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collider_id"), "", "get_collider_id");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "collider"), "", "get_collider");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collider_shape"), "", "get_collider_shape");
}
