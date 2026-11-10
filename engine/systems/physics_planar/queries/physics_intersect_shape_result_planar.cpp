/**************************************************************************/
/*  physics_intersect_shape_result_planar.cpp                                 */

#include "physics_intersect_shape_result_planar.h"

#include "core/object/class_registry.h"

PhysicsIntersectShapeResult2D::PhysicsIntersectShapeResult2D(int p_max_intersections) {
	ERR_FAIL_COND(p_max_intersections < 0);
	result.resize(p_max_intersections);
}

int PhysicsIntersectShapeResult2D::get_max_intersections() const {
	return result.size();
}

void PhysicsIntersectShapeResult2D::set_max_intersections(int p_max_intersections) {
	ERR_FAIL_COND(p_max_intersections < 0);
	result.resize(p_max_intersections);
	intersection_count = MIN(intersection_count, static_cast<int>(result.size()));
}

int PhysicsIntersectShapeResult2D::get_intersection_count() const {
	return intersection_count;
}

RID PhysicsIntersectShapeResult2D::get_collider_rid(int p_intersection_index) const {
	ERR_FAIL_INDEX_V(p_intersection_index, intersection_count, RID());
	return result[p_intersection_index].rid;
}

ObjectID PhysicsIntersectShapeResult2D::get_collider_id(int p_intersection_index) const {
	ERR_FAIL_INDEX_V(p_intersection_index, intersection_count, ObjectID());
	return result[p_intersection_index].collider_id;
}

Object *PhysicsIntersectShapeResult2D::get_collider(int p_intersection_index) const {
	ERR_FAIL_INDEX_V(p_intersection_index, intersection_count, nullptr);
	return result[p_intersection_index].get_collider();
}

int PhysicsIntersectShapeResult2D::get_collider_shape(int p_intersection_index) const {
	ERR_FAIL_INDEX_V(p_intersection_index, intersection_count, 0);
	return result[p_intersection_index].shape;
}

void PhysicsIntersectShapeResult2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_max_intersections"), &PhysicsIntersectShapeResult2D::get_max_intersections);
	ClassRegistry::bind_method(D_METHOD("set_max_intersections", "max_intersections"), &PhysicsIntersectShapeResult2D::set_max_intersections);
	ClassRegistry::bind_method(D_METHOD("get_intersection_count"), &PhysicsIntersectShapeResult2D::get_intersection_count);
	ClassRegistry::bind_method(D_METHOD("get_collider_id", "intersection_index"), &PhysicsIntersectShapeResult2D::get_collider_id);
	ClassRegistry::bind_method(D_METHOD("get_collider_rid", "intersection_index"), &PhysicsIntersectShapeResult2D::get_collider_rid);
	ClassRegistry::bind_method(D_METHOD("get_collider", "intersection_index"), &PhysicsIntersectShapeResult2D::get_collider);
	ClassRegistry::bind_method(D_METHOD("get_collider_shape", "intersection_index"), &PhysicsIntersectShapeResult2D::get_collider_shape);

	ADD_PROPERTY(PropertyInfo(Variant::INT, "max_intersections"), "set_max_intersections", "get_max_intersections");
}
