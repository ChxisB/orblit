/**************************************************************************/
/*  kinematic_collision_planar.cpp                                            */

#include "kinematic_collision_planar.h"

#include "core/object/class_registry.h"
#include "scene/planar/physics/physics_body_planar.h"

Vector2 KinematicCollision2D::get_position() const {
	return result.collision_point;
}

Vector2 KinematicCollision2D::get_normal() const {
	return result.collision_normal;
}

Vector2 KinematicCollision2D::get_travel() const {
	return result.travel;
}

Vector2 KinematicCollision2D::get_remainder() const {
	return result.remainder;
}

real_t KinematicCollision2D::get_angle(const Vector2 &p_up_direction) const {
	ERR_FAIL_COND_V(p_up_direction == Vector2(), 0);
	return result.get_angle(p_up_direction);
}

real_t KinematicCollision2D::get_depth() const {
	return result.collision_depth;
}

Object *KinematicCollision2D::get_local_shape() const {
	PhysicsBody2D *owner = ObjectRegistry::get_instance<PhysicsBody2D>(owner_id);
	if (!owner) {
		return nullptr;
	}
	uint32_t ownerid = owner->shape_find_owner(result.collision_local_shape);
	return owner->shape_owner_get_owner(ownerid);
}

Object *KinematicCollision2D::get_collider() const {
	if (result.collider_id.is_valid()) {
		return ObjectRegistry::get_instance(result.collider_id);
	}

	return nullptr;
}

ObjectID KinematicCollision2D::get_collider_id() const {
	return result.collider_id;
}

RID KinematicCollision2D::get_collider_rid() const {
	return result.collider;
}

Object *KinematicCollision2D::get_collider_shape() const {
	Object *collider = get_collider();
	if (collider) {
		CollisionObject2D *obj2d = Object::cast_to<CollisionObject2D>(collider);
		if (obj2d) {
			uint32_t ownerid = obj2d->shape_find_owner(result.collider_shape);
			return obj2d->shape_owner_get_owner(ownerid);
		}
	}

	return nullptr;
}

int KinematicCollision2D::get_collider_shape_index() const {
	return result.collider_shape;
}

Vector2 KinematicCollision2D::get_collider_velocity() const {
	return result.collider_velocity;
}

void KinematicCollision2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_position"), &KinematicCollision2D::get_position);
	ClassRegistry::bind_method(D_METHOD("get_normal"), &KinematicCollision2D::get_normal);
	ClassRegistry::bind_method(D_METHOD("get_travel"), &KinematicCollision2D::get_travel);
	ClassRegistry::bind_method(D_METHOD("get_remainder"), &KinematicCollision2D::get_remainder);
	ClassRegistry::bind_method(D_METHOD("get_angle", "up_direction"), &KinematicCollision2D::get_angle, DEFVAL(Vector2(0.0, -1.0)));
	ClassRegistry::bind_method(D_METHOD("get_depth"), &KinematicCollision2D::get_depth);
	ClassRegistry::bind_method(D_METHOD("get_local_shape"), &KinematicCollision2D::get_local_shape);
	ClassRegistry::bind_method(D_METHOD("get_collider"), &KinematicCollision2D::get_collider);
	ClassRegistry::bind_method(D_METHOD("get_collider_id"), &KinematicCollision2D::get_collider_id);
	ClassRegistry::bind_method(D_METHOD("get_collider_rid"), &KinematicCollision2D::get_collider_rid);
	ClassRegistry::bind_method(D_METHOD("get_collider_shape"), &KinematicCollision2D::get_collider_shape);
	ClassRegistry::bind_method(D_METHOD("get_collider_shape_index"), &KinematicCollision2D::get_collider_shape_index);
	ClassRegistry::bind_method(D_METHOD("get_collider_velocity"), &KinematicCollision2D::get_collider_velocity);
}
