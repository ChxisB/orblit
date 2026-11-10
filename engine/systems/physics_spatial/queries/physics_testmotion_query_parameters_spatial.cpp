/**************************************************************************/
/*  physics_testmotion_query_parameters_spatial.cpp                            */

#include "physics_testmotion_query_parameters_spatial.h"

#include "core/object/class_registry.h"
#include "core/variant/typed_array.h"

TypedArray<RID> PhysicsTestMotionParameters3D::get_exclude_bodies() const {
	TypedArray<RID> exclude;
	exclude.resize(parameters.exclude_bodies.size());

	int body_index = 0;
	for (const RID &body : parameters.exclude_bodies) {
		exclude[body_index++] = body;
	}

	return exclude;
}

void PhysicsTestMotionParameters3D::set_exclude_bodies(const TypedArray<RID> &p_exclude) {
	parameters.exclude_bodies.clear();
	for (int i = 0; i < p_exclude.size(); i++) {
		parameters.exclude_bodies.insert(p_exclude[i]);
	}
}

TypedArray<uint64_t> PhysicsTestMotionParameters3D::get_exclude_objects() const {
	TypedArray<uint64_t> exclude;
	exclude.resize(parameters.exclude_objects.size());

	int object_index = 0;
	for (const ObjectID &object_id : parameters.exclude_objects) {
		exclude[object_index++] = object_id;
	}

	return exclude;
}

void PhysicsTestMotionParameters3D::set_exclude_objects(const TypedArray<uint64_t> &p_exclude) {
	parameters.exclude_objects.clear();
	for (int i = 0; i < p_exclude.size(); ++i) {
		ObjectID object_id = p_exclude[i];
		ERR_CONTINUE(object_id.is_null());
		parameters.exclude_objects.insert(object_id);
	}
}

void PhysicsTestMotionParameters3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_from"), &PhysicsTestMotionParameters3D::get_from);
	ClassRegistry::bind_method(D_METHOD("set_from", "from"), &PhysicsTestMotionParameters3D::set_from);

	ClassRegistry::bind_method(D_METHOD("get_motion"), &PhysicsTestMotionParameters3D::get_motion);
	ClassRegistry::bind_method(D_METHOD("set_motion", "motion"), &PhysicsTestMotionParameters3D::set_motion);

	ClassRegistry::bind_method(D_METHOD("get_margin"), &PhysicsTestMotionParameters3D::get_margin);
	ClassRegistry::bind_method(D_METHOD("set_margin", "margin"), &PhysicsTestMotionParameters3D::set_margin);

	ClassRegistry::bind_method(D_METHOD("get_max_collisions"), &PhysicsTestMotionParameters3D::get_max_collisions);
	ClassRegistry::bind_method(D_METHOD("set_max_collisions", "max_collisions"), &PhysicsTestMotionParameters3D::set_max_collisions);

	ClassRegistry::bind_method(D_METHOD("is_collide_separation_ray_enabled"), &PhysicsTestMotionParameters3D::is_collide_separation_ray_enabled);
	ClassRegistry::bind_method(D_METHOD("set_collide_separation_ray_enabled", "enabled"), &PhysicsTestMotionParameters3D::set_collide_separation_ray_enabled);

	ClassRegistry::bind_method(D_METHOD("get_exclude_bodies"), &PhysicsTestMotionParameters3D::get_exclude_bodies);
	ClassRegistry::bind_method(D_METHOD("set_exclude_bodies", "exclude_list"), &PhysicsTestMotionParameters3D::set_exclude_bodies);

	ClassRegistry::bind_method(D_METHOD("get_exclude_objects"), &PhysicsTestMotionParameters3D::get_exclude_objects);
	ClassRegistry::bind_method(D_METHOD("set_exclude_objects", "exclude_list"), &PhysicsTestMotionParameters3D::set_exclude_objects);

	ClassRegistry::bind_method(D_METHOD("is_recovery_as_collision_enabled"), &PhysicsTestMotionParameters3D::is_recovery_as_collision_enabled);
	ClassRegistry::bind_method(D_METHOD("set_recovery_as_collision_enabled", "enabled"), &PhysicsTestMotionParameters3D::set_recovery_as_collision_enabled);

	ADD_PROPERTY(PropertyInfo(Variant::TRANSFORM3D, "from"), "set_from", "get_from");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "motion"), "set_motion", "get_motion");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "margin"), "set_margin", "get_margin");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "max_collisions"), "set_max_collisions", "get_max_collisions");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_separation_ray"), "set_collide_separation_ray_enabled", "is_collide_separation_ray_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "exclude_bodies", PROPERTY_HINT_ARRAY_TYPE, "RID"), "set_exclude_bodies", "get_exclude_bodies");
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "exclude_objects"), "set_exclude_objects", "get_exclude_objects");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "recovery_as_collision"), "set_recovery_as_collision_enabled", "is_recovery_as_collision_enabled");
}
