/**************************************************************************/
/*  physics_ray_query_parameters_planar.cpp                                   */

#include "physics_ray_query_parameters_planar.h"

#include "core/object/class_registry.h"
#include "core/variant/typed_array.h"

Ref<PhysicsRayQueryParameters2D> PhysicsRayQueryParameters2D::create(Vector2 p_from, Vector2 p_to, uint32_t p_mask, const TypedArray<RID> &p_exclude) {
	Ref<PhysicsRayQueryParameters2D> params;
	params.instantiate();
	params->set_from(p_from);
	params->set_to(p_to);
	params->set_collision_mask(p_mask);
	params->set_exclude(p_exclude);
	return params;
}

void PhysicsRayQueryParameters2D::set_exclude(const TypedArray<RID> &p_exclude) {
	parameters.exclude.clear();
	for (int i = 0; i < p_exclude.size(); i++) {
		parameters.exclude.insert(p_exclude[i]);
	}
}

TypedArray<RID> PhysicsRayQueryParameters2D::get_exclude() const {
	TypedArray<RID> ret;
	ret.resize(parameters.exclude.size());
	int idx = 0;
	for (const RID &E : parameters.exclude) {
		ret[idx++] = E;
	}
	return ret;
}

void PhysicsRayQueryParameters2D::register_members() {
	ClassRegistry::bind_static_method("PhysicsRayQueryParameters2D", D_METHOD("create", "from", "to", "collision_mask", "exclude"), &PhysicsRayQueryParameters2D::create, DEFVAL(UINT32_MAX), DEFVAL(TypedArray<RID>()));

	ClassRegistry::bind_method(D_METHOD("set_from", "from"), &PhysicsRayQueryParameters2D::set_from);
	ClassRegistry::bind_method(D_METHOD("get_from"), &PhysicsRayQueryParameters2D::get_from);

	ClassRegistry::bind_method(D_METHOD("set_to", "to"), &PhysicsRayQueryParameters2D::set_to);
	ClassRegistry::bind_method(D_METHOD("get_to"), &PhysicsRayQueryParameters2D::get_to);

	ClassRegistry::bind_method(D_METHOD("set_collision_mask", "collision_mask"), &PhysicsRayQueryParameters2D::set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("get_collision_mask"), &PhysicsRayQueryParameters2D::get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("set_exclude", "exclude"), &PhysicsRayQueryParameters2D::set_exclude);
	ClassRegistry::bind_method(D_METHOD("get_exclude"), &PhysicsRayQueryParameters2D::get_exclude);

	ClassRegistry::bind_method(D_METHOD("set_collide_with_bodies", "enable"), &PhysicsRayQueryParameters2D::set_collide_with_bodies);
	ClassRegistry::bind_method(D_METHOD("is_collide_with_bodies_enabled"), &PhysicsRayQueryParameters2D::is_collide_with_bodies_enabled);

	ClassRegistry::bind_method(D_METHOD("set_collide_with_areas", "enable"), &PhysicsRayQueryParameters2D::set_collide_with_areas);
	ClassRegistry::bind_method(D_METHOD("is_collide_with_areas_enabled"), &PhysicsRayQueryParameters2D::is_collide_with_areas_enabled);

	ClassRegistry::bind_method(D_METHOD("set_hit_from_inside", "enable"), &PhysicsRayQueryParameters2D::set_hit_from_inside);
	ClassRegistry::bind_method(D_METHOD("is_hit_from_inside_enabled"), &PhysicsRayQueryParameters2D::is_hit_from_inside_enabled);

	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "from"), "set_from", "get_from");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "to"), "set_to", "get_to");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collision_mask", PROPERTY_HINT_LAYERS_2D_PHYSICS), "set_collision_mask", "get_collision_mask");
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "exclude", PROPERTY_HINT_ARRAY_TYPE, "RID"), "set_exclude", "get_exclude");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_with_bodies"), "set_collide_with_bodies", "is_collide_with_bodies_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_with_areas"), "set_collide_with_areas", "is_collide_with_areas_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "hit_from_inside"), "set_hit_from_inside", "is_hit_from_inside_enabled");
}
