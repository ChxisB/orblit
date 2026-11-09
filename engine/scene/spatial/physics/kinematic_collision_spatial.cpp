/**************************************************************************/
/*  kinematic_collision_spatial.cpp                                            */

#include "kinematic_collision_spatial.h"

#include "core/object/class_registry.h"
#include "scene/spatial/physics/physics_body_spatial.h"

Vector3 KinematicCollision3D::get_travel() const {
	return result.travel;
}

Vector3 KinematicCollision3D::get_remainder() const {
	return result.remainder;
}

int KinematicCollision3D::get_collision_count() const {
	return result.collision_count;
}

real_t KinematicCollision3D::get_depth() const {
	return result.collision_depth;
}

Vector3 KinematicCollision3D::get_position(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, Vector3());
	return result.collisions[p_collision_index].position;
}

Vector3 KinematicCollision3D::get_normal(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, Vector3());
	return result.collisions[p_collision_index].normal;
}

real_t KinematicCollision3D::get_angle(int p_collision_index, const Vector3 &p_up_direction) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, 0.0);
	ERR_FAIL_COND_V(p_up_direction == Vector3(), 0);
	return result.collisions[p_collision_index].get_angle(p_up_direction);
}

Object *KinematicCollision3D::get_local_shape(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, nullptr);
	PhysicsBody3D *owner = ObjectRegistry::get_instance<PhysicsBody3D>(owner_id);
	if (!owner) {
		return nullptr;
	}
	uint32_t ownerid = owner->shape_find_owner(result.collisions[p_collision_index].local_shape);
	return owner->shape_owner_get_owner(ownerid);
}

Object *KinematicCollision3D::get_collider(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, nullptr);
	if (result.collisions[p_collision_index].collider_id.is_valid()) {
		return ObjectRegistry::get_instance(result.collisions[p_collision_index].collider_id);
	}

	return nullptr;
}

ObjectID KinematicCollision3D::get_collider_id(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, ObjectID());
	return result.collisions[p_collision_index].collider_id;
}

RID KinematicCollision3D::get_collider_rid(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, RID());
	return result.collisions[p_collision_index].collider;
}

Object *KinematicCollision3D::get_collider_shape(int p_collision_index) const {
	Object *collider = get_collider(p_collision_index);
	if (collider) {
		CollisionObject3D *obj2d = Object::cast_to<CollisionObject3D>(collider);
		if (obj2d) {
			uint32_t ownerid = obj2d->shape_find_owner(result.collisions[p_collision_index].collider_shape);
			return obj2d->shape_owner_get_owner(ownerid);
		}
	}

	return nullptr;
}

int KinematicCollision3D::get_collider_shape_index(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, 0);
	return result.collisions[p_collision_index].collider_shape;
}

Vector3 KinematicCollision3D::get_collider_velocity(int p_collision_index) const {
	ERR_FAIL_INDEX_V(p_collision_index, result.collision_count, Vector3());
	return result.collisions[p_collision_index].collider_velocity;
}

void KinematicCollision3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_travel"), &KinematicCollision3D::get_travel);
	ClassRegistry::bind_method(D_METHOD("get_remainder"), &KinematicCollision3D::get_remainder);
	ClassRegistry::bind_method(D_METHOD("get_depth"), &KinematicCollision3D::get_depth);
	ClassRegistry::bind_method(D_METHOD("get_collision_count"), &KinematicCollision3D::get_collision_count);
	ClassRegistry::bind_method(D_METHOD("get_position", "collision_index"), &KinematicCollision3D::get_position, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_normal", "collision_index"), &KinematicCollision3D::get_normal, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_angle", "collision_index", "up_direction"), &KinematicCollision3D::get_angle, DEFVAL(0), DEFVAL(Vector3(0.0, 1.0, 0.0)));
	ClassRegistry::bind_method(D_METHOD("get_local_shape", "collision_index"), &KinematicCollision3D::get_local_shape, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collider", "collision_index"), &KinematicCollision3D::get_collider, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collider_id", "collision_index"), &KinematicCollision3D::get_collider_id, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collider_rid", "collision_index"), &KinematicCollision3D::get_collider_rid, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collider_shape", "collision_index"), &KinematicCollision3D::get_collider_shape, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collider_shape_index", "collision_index"), &KinematicCollision3D::get_collider_shape_index, DEFVAL(0));
	ClassRegistry::bind_method(D_METHOD("get_collider_velocity", "collision_index"), &KinematicCollision3D::get_collider_velocity, DEFVAL(0));
}
