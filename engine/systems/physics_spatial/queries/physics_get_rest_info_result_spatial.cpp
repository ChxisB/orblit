/**************************************************************************/
/*  physics_get_rest_info_result_spatial.cpp                                   */

#include "physics_get_rest_info_result_spatial.h"

#include "core/object/class_registry.h"

Vector3 PhysicsGetRestInfoResult3D::get_point() const {
	return result.point;
}

Vector3 PhysicsGetRestInfoResult3D::get_normal() const {
	return result.normal;
}

RID PhysicsGetRestInfoResult3D::get_collider_rid() const {
	return result.rid;
}

ObjectID PhysicsGetRestInfoResult3D::get_collider_id() const {
	return result.collider_id;
}

int PhysicsGetRestInfoResult3D::get_collider_shape() const {
	return result.shape;
}

Vector3 PhysicsGetRestInfoResult3D::get_collider_velocity() const {
	return result.linear_velocity;
}

void PhysicsGetRestInfoResult3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_point"), &PhysicsGetRestInfoResult3D::get_point);
	ClassRegistry::bind_method(D_METHOD("get_normal"), &PhysicsGetRestInfoResult3D::get_normal);
	ClassRegistry::bind_method(D_METHOD("get_collider_rid"), &PhysicsGetRestInfoResult3D::get_collider_rid);
	ClassRegistry::bind_method(D_METHOD("get_collider_id"), &PhysicsGetRestInfoResult3D::get_collider_id);
	ClassRegistry::bind_method(D_METHOD("get_collider_shape"), &PhysicsGetRestInfoResult3D::get_collider_shape);
	ClassRegistry::bind_method(D_METHOD("get_collider_velocity"), &PhysicsGetRestInfoResult3D::get_collider_velocity);

	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "point"), "", "get_point");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "normal"), "", "get_normal");
	ADD_PROPERTY(PropertyInfo(Variant::RID, "collider_rid"), "", "get_collider_rid");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collider_id"), "", "get_collider_id");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collider_shape"), "", "get_collider_shape");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "collider_velocity"), "", "get_collider_velocity");
}
