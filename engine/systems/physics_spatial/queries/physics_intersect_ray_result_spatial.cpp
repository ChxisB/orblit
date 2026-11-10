/**************************************************************************/
/*  physics_intersect_ray_result_spatial.cpp                                   */

#include "physics_intersect_ray_result_spatial.h"

#include "core/object/class_registry.h"

Vector3 PhysicsIntersectRayResult3D::get_position() const {
	return result.position;
}

Vector3 PhysicsIntersectRayResult3D::get_normal() const {
	return result.normal;
}

RID PhysicsIntersectRayResult3D::get_collider_rid() const {
	return result.rid;
}

ObjectID PhysicsIntersectRayResult3D::get_collider_id() const {
	return result.collider_id;
}

Object *PhysicsIntersectRayResult3D::get_collider() const {
	return result.get_collider();
}

int PhysicsIntersectRayResult3D::get_collider_shape() const {
	return result.shape;
}

int PhysicsIntersectRayResult3D::get_collider_face_index() const {
	return result.face_index;
}

void PhysicsIntersectRayResult3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_position"), &PhysicsIntersectRayResult3D::get_position);
	ClassRegistry::bind_method(D_METHOD("get_normal"), &PhysicsIntersectRayResult3D::get_normal);
	ClassRegistry::bind_method(D_METHOD("get_collider_rid"), &PhysicsIntersectRayResult3D::get_collider_rid);
	ClassRegistry::bind_method(D_METHOD("get_collider_id"), &PhysicsIntersectRayResult3D::get_collider_id);
	ClassRegistry::bind_method(D_METHOD("get_collider"), &PhysicsIntersectRayResult3D::get_collider);
	ClassRegistry::bind_method(D_METHOD("get_collider_shape"), &PhysicsIntersectRayResult3D::get_collider_shape);
	ClassRegistry::bind_method(D_METHOD("get_collider_face_index"), &PhysicsIntersectRayResult3D::get_collider_face_index);

	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "position"), "", "get_position");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "normal"), "", "get_normal");
	ADD_PROPERTY(PropertyInfo(Variant::RID, "collider_rid"), "", "get_collider_rid");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collider_id"), "", "get_collider_id");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "collider"), "", "get_collider");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collider_shape"), "", "get_collider_shape");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collider_face_index"), "", "get_collider_face_index");
}
