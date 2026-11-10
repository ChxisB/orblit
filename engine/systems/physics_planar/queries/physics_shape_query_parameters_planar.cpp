/**************************************************************************/
/*  physics_shape_query_parameters_planar.cpp                                 */

#include "physics_shape_query_parameters_planar.h"

#include "core/io/resource.h"
#include "core/object/class_registry.h"
#include "core/variant/typed_array.h"

Ref<Resource> PhysicsShapeQueryParameters2D::get_shape() const {
	return shape_ref;
}

void PhysicsShapeQueryParameters2D::set_shape(const Ref<Resource> &p_shape_ref) {
	ERR_FAIL_COND(p_shape_ref.is_null());
	shape_ref = p_shape_ref;
	parameters.shape_rid = p_shape_ref->get_rid();
}

void PhysicsShapeQueryParameters2D::set_shape_rid(const RID &p_shape) {
	if (parameters.shape_rid != p_shape) {
		shape_ref = Ref<Resource>();
		parameters.shape_rid = p_shape;
	}
}

void PhysicsShapeQueryParameters2D::set_exclude(const TypedArray<RID> &p_exclude) {
	parameters.exclude.clear();
	for (int i = 0; i < p_exclude.size(); i++) {
		parameters.exclude.insert(p_exclude[i]);
	}
}

TypedArray<RID> PhysicsShapeQueryParameters2D::get_exclude() const {
	TypedArray<RID> ret;
	ret.resize(parameters.exclude.size());
	int idx = 0;
	for (const RID &E : parameters.exclude) {
		ret[idx++] = E;
	}
	return ret;
}

void PhysicsShapeQueryParameters2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_shape", "shape"), &PhysicsShapeQueryParameters2D::set_shape);
	ClassRegistry::bind_method(D_METHOD("get_shape"), &PhysicsShapeQueryParameters2D::get_shape);

	ClassRegistry::bind_method(D_METHOD("set_shape_rid", "shape"), &PhysicsShapeQueryParameters2D::set_shape_rid);
	ClassRegistry::bind_method(D_METHOD("get_shape_rid"), &PhysicsShapeQueryParameters2D::get_shape_rid);

	ClassRegistry::bind_method(D_METHOD("set_transform", "transform"), &PhysicsShapeQueryParameters2D::set_transform);
	ClassRegistry::bind_method(D_METHOD("get_transform"), &PhysicsShapeQueryParameters2D::get_transform);

	ClassRegistry::bind_method(D_METHOD("set_motion", "motion"), &PhysicsShapeQueryParameters2D::set_motion);
	ClassRegistry::bind_method(D_METHOD("get_motion"), &PhysicsShapeQueryParameters2D::get_motion);

	ClassRegistry::bind_method(D_METHOD("set_margin", "margin"), &PhysicsShapeQueryParameters2D::set_margin);
	ClassRegistry::bind_method(D_METHOD("get_margin"), &PhysicsShapeQueryParameters2D::get_margin);

	ClassRegistry::bind_method(D_METHOD("set_collision_mask", "collision_mask"), &PhysicsShapeQueryParameters2D::set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("get_collision_mask"), &PhysicsShapeQueryParameters2D::get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("set_exclude", "exclude"), &PhysicsShapeQueryParameters2D::set_exclude);
	ClassRegistry::bind_method(D_METHOD("get_exclude"), &PhysicsShapeQueryParameters2D::get_exclude);

	ClassRegistry::bind_method(D_METHOD("set_collide_with_bodies", "enable"), &PhysicsShapeQueryParameters2D::set_collide_with_bodies);
	ClassRegistry::bind_method(D_METHOD("is_collide_with_bodies_enabled"), &PhysicsShapeQueryParameters2D::is_collide_with_bodies_enabled);

	ClassRegistry::bind_method(D_METHOD("set_collide_with_areas", "enable"), &PhysicsShapeQueryParameters2D::set_collide_with_areas);
	ClassRegistry::bind_method(D_METHOD("is_collide_with_areas_enabled"), &PhysicsShapeQueryParameters2D::is_collide_with_areas_enabled);

	ADD_PROPERTY(PropertyInfo(Variant::INT, "collision_mask", PROPERTY_HINT_LAYERS_2D_PHYSICS), "set_collision_mask", "get_collision_mask");
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "exclude", PROPERTY_HINT_ARRAY_TYPE, "RID"), "set_exclude", "get_exclude");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "margin", PROPERTY_HINT_RANGE, "0,100,0.01"), "set_margin", "get_margin");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "motion"), "set_motion", "get_motion");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "shape", PROPERTY_HINT_RESOURCE_TYPE, "Shape2D"), "set_shape", "get_shape");
	ADD_PROPERTY(PropertyInfo(Variant::RID, "shape_rid"), "set_shape_rid", "get_shape_rid");
	ADD_PROPERTY(PropertyInfo(Variant::TRANSFORM2D, "transform"), "set_transform", "get_transform");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_with_bodies"), "set_collide_with_bodies", "is_collide_with_bodies_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_with_areas"), "set_collide_with_areas", "is_collide_with_areas_enabled");
}
