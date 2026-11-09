/**************************************************************************/
/*  physics_point_query_parameters_spatial.cpp                                 */

#include "physics_point_query_parameters_spatial.h"

#include "core/object/class_registry.h"
#include "core/variant/typed_array.h"

void PhysicsPointQueryParameters3D::set_exclude(const TypedArray<RID> &p_exclude) {
	parameters.exclude.clear();
	for (int i = 0; i < p_exclude.size(); i++) {
		parameters.exclude.insert(p_exclude[i]);
	}
}

TypedArray<RID> PhysicsPointQueryParameters3D::get_exclude() const {
	TypedArray<RID> ret;
	ret.resize(parameters.exclude.size());
	int idx = 0;
	for (const RID &E : parameters.exclude) {
		ret[idx++] = E;
	}
	return ret;
}

void PhysicsPointQueryParameters3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_position", "position"), &PhysicsPointQueryParameters3D::set_position);
	ClassRegistry::bind_method(D_METHOD("get_position"), &PhysicsPointQueryParameters3D::get_position);

	ClassRegistry::bind_method(D_METHOD("set_collision_mask", "collision_mask"), &PhysicsPointQueryParameters3D::set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("get_collision_mask"), &PhysicsPointQueryParameters3D::get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("set_exclude", "exclude"), &PhysicsPointQueryParameters3D::set_exclude);
	ClassRegistry::bind_method(D_METHOD("get_exclude"), &PhysicsPointQueryParameters3D::get_exclude);

	ClassRegistry::bind_method(D_METHOD("set_collide_with_bodies", "enable"), &PhysicsPointQueryParameters3D::set_collide_with_bodies);
	ClassRegistry::bind_method(D_METHOD("is_collide_with_bodies_enabled"), &PhysicsPointQueryParameters3D::is_collide_with_bodies_enabled);

	ClassRegistry::bind_method(D_METHOD("set_collide_with_areas", "enable"), &PhysicsPointQueryParameters3D::set_collide_with_areas);
	ClassRegistry::bind_method(D_METHOD("is_collide_with_areas_enabled"), &PhysicsPointQueryParameters3D::is_collide_with_areas_enabled);

	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "position"), "set_position", "get_position");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collision_mask", PROPERTY_HINT_LAYERS_3D_PHYSICS), "set_collision_mask", "get_collision_mask");
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "exclude", PROPERTY_HINT_ARRAY_TYPE, "RID"), "set_exclude", "get_exclude");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_with_bodies"), "set_collide_with_bodies", "is_collide_with_bodies_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_with_areas"), "set_collide_with_areas", "is_collide_with_areas_enabled");
}
