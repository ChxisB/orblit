/**************************************************************************/
/*  physics_point_query_parameters_planar.cpp                                 */

#include "physics_point_query_parameters_planar.h"

#include "core/object/class_registry.h"
#include "core/variant/typed_array.h"

void PhysicsPointQueryParameters2D::set_exclude(const TypedArray<RID> &p_exclude) {
	parameters.exclude.clear();
	for (int i = 0; i < p_exclude.size(); i++) {
		parameters.exclude.insert(p_exclude[i]);
	}
}

TypedArray<RID> PhysicsPointQueryParameters2D::get_exclude() const {
	TypedArray<RID> ret;
	ret.resize(parameters.exclude.size());
	int idx = 0;
	for (const RID &E : parameters.exclude) {
		ret[idx++] = E;
	}
	return ret;
}

void PhysicsPointQueryParameters2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_position", "position"), &PhysicsPointQueryParameters2D::set_position);
	ClassRegistry::bind_method(D_METHOD("get_position"), &PhysicsPointQueryParameters2D::get_position);

	ClassRegistry::bind_method(D_METHOD("set_canvas_instance_id", "canvas_instance_id"), &PhysicsPointQueryParameters2D::set_canvas_instance_id);
	ClassRegistry::bind_method(D_METHOD("get_canvas_instance_id"), &PhysicsPointQueryParameters2D::get_canvas_instance_id);

	ClassRegistry::bind_method(D_METHOD("set_collision_mask", "collision_mask"), &PhysicsPointQueryParameters2D::set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("get_collision_mask"), &PhysicsPointQueryParameters2D::get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("set_exclude", "exclude"), &PhysicsPointQueryParameters2D::set_exclude);
	ClassRegistry::bind_method(D_METHOD("get_exclude"), &PhysicsPointQueryParameters2D::get_exclude);

	ClassRegistry::bind_method(D_METHOD("set_collide_with_bodies", "enable"), &PhysicsPointQueryParameters2D::set_collide_with_bodies);
	ClassRegistry::bind_method(D_METHOD("is_collide_with_bodies_enabled"), &PhysicsPointQueryParameters2D::is_collide_with_bodies_enabled);

	ClassRegistry::bind_method(D_METHOD("set_collide_with_areas", "enable"), &PhysicsPointQueryParameters2D::set_collide_with_areas);
	ClassRegistry::bind_method(D_METHOD("is_collide_with_areas_enabled"), &PhysicsPointQueryParameters2D::is_collide_with_areas_enabled);

	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "position"), "set_position", "get_position");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "canvas_instance_id", PROPERTY_HINT_OBJECT_ID), "set_canvas_instance_id", "get_canvas_instance_id");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collision_mask", PROPERTY_HINT_LAYERS_2D_PHYSICS), "set_collision_mask", "get_collision_mask");
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "exclude", PROPERTY_HINT_ARRAY_TYPE, "RID"), "set_exclude", "get_exclude");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_with_bodies"), "set_collide_with_bodies", "is_collide_with_bodies_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_with_areas"), "set_collide_with_areas", "is_collide_with_areas_enabled");
}
