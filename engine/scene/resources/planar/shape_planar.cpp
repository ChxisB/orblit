/**************************************************************************/
/*  shape_planar.cpp                                                          */

#include "shape_planar.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"
#include "systems/physics_planar/physics_system_planar.h"

RID Shape2D::get_rid() const {
	return shape;
}

void Shape2D::set_custom_solver_bias(real_t p_bias) {
	custom_bias = p_bias;
	PhysicsSystem2D::get_singleton()->shape_set_custom_solver_bias(shape, custom_bias);
}

real_t Shape2D::get_custom_solver_bias() const {
	return custom_bias;
}

bool Shape2D::collide_with_motion(const Transform2D &p_local_xform, const Vector2 &p_local_motion, RequiredParam<Shape2D> p_shape, const Transform2D &p_shape_xform, const Vector2 &p_shape_motion) {
	EXTRACT_PARAM_OR_FAIL_V(check_shape, p_shape, false);
	int r;
	return PhysicsSystem2D::get_singleton()->shape_collide(get_rid(), p_local_xform, p_local_motion, check_shape->get_rid(), p_shape_xform, p_shape_motion, nullptr, 0, r);
}

bool Shape2D::collide(const Transform2D &p_local_xform, RequiredParam<Shape2D> p_shape, const Transform2D &p_shape_xform) {
	EXTRACT_PARAM_OR_FAIL_V(check_shape, p_shape, false);
	int r;
	return PhysicsSystem2D::get_singleton()->shape_collide(get_rid(), p_local_xform, Vector2(), check_shape->get_rid(), p_shape_xform, Vector2(), nullptr, 0, r);
}

PackedVector2Array Shape2D::collide_with_motion_and_get_contacts(const Transform2D &p_local_xform, const Vector2 &p_local_motion, RequiredParam<Shape2D> p_shape, const Transform2D &p_shape_xform, const Vector2 &p_shape_motion) {
	EXTRACT_PARAM_OR_FAIL_V(check_shape, p_shape, PackedVector2Array());
	const int max_contacts = 16;
	Vector2 result[max_contacts * 2];
	int contacts = 0;

	if (!PhysicsSystem2D::get_singleton()->shape_collide(get_rid(), p_local_xform, p_local_motion, check_shape->get_rid(), p_shape_xform, p_shape_motion, result, max_contacts, contacts)) {
		return PackedVector2Array();
	}

	PackedVector2Array results;
	results.resize(contacts * 2);
	for (int i = 0; i < contacts * 2; i++) {
		results.write[i] = result[i];
	}

	return results;
}

PackedVector2Array Shape2D::collide_and_get_contacts(const Transform2D &p_local_xform, RequiredParam<Shape2D> p_shape, const Transform2D &p_shape_xform) {
	EXTRACT_PARAM_OR_FAIL_V(check_shape, p_shape, PackedVector2Array());
	const int max_contacts = 16;
	Vector2 result[max_contacts * 2];
	int contacts = 0;

	if (!PhysicsSystem2D::get_singleton()->shape_collide(get_rid(), p_local_xform, Vector2(), check_shape->get_rid(), p_shape_xform, Vector2(), result, max_contacts, contacts)) {
		return PackedVector2Array();
	}

	PackedVector2Array results;
	results.resize(contacts * 2);
	for (int i = 0; i < contacts * 2; i++) {
		results.write[i] = result[i];
	}

	return results;
}

void Shape2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_custom_solver_bias", "bias"), &Shape2D::set_custom_solver_bias);
	ClassRegistry::bind_method(D_METHOD("get_custom_solver_bias"), &Shape2D::get_custom_solver_bias);
	ClassRegistry::bind_method(D_METHOD("collide", "local_xform", "with_shape", "shape_xform"), &Shape2D::collide);
	ClassRegistry::bind_method(D_METHOD("collide_with_motion", "local_xform", "local_motion", "with_shape", "shape_xform", "shape_motion"), &Shape2D::collide_with_motion);
	ClassRegistry::bind_method(D_METHOD("collide_and_get_contacts", "local_xform", "with_shape", "shape_xform"), &Shape2D::collide_and_get_contacts);
	ClassRegistry::bind_method(D_METHOD("collide_with_motion_and_get_contacts", "local_xform", "local_motion", "with_shape", "shape_xform", "shape_motion"), &Shape2D::collide_with_motion_and_get_contacts);
	ClassRegistry::bind_method(D_METHOD("draw", "canvas_item", "color"), &Shape2D::draw);
	ClassRegistry::bind_method(D_METHOD("get_rect"), &Shape2D::get_rect);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "custom_solver_bias", PROPERTY_HINT_RANGE, "0,1,0.001"), "set_custom_solver_bias", "get_custom_solver_bias");
}

bool Shape2D::is_collision_outline_enabled() {
	return GLOBAL_GET_CACHED(bool, "debug/shapes/collision/draw_2d_outlines");
}

Shape2D::Shape2D(const RID &p_rid) {
	shape = p_rid;
}

Shape2D::~Shape2D() {
	ERR_FAIL_NULL(PhysicsSystem2D::get_singleton());
	PhysicsSystem2D::get_singleton()->free_rid(shape);
}
