/**************************************************************************/
/*  separation_ray_shape_spatial.cpp                                           */

#include "separation_ray_shape_spatial.h"

#include "core/object/class_registry.h"
#include "scene/resources/mesh.h"
#include "systems/physics_spatial/physics_system_spatial.h"

Vector<Vector3> SeparationRayShape3D::get_debug_mesh_lines() const {
	Vector<Vector3> points = {
		Vector3(),
		Vector3(0, 0, get_length())
	};

	return points;
}

Ref<ArrayMesh> SeparationRayShape3D::get_debug_arraymesh_faces(const Color &p_modulate) const {
	return memnew(ArrayMesh);
}

real_t SeparationRayShape3D::get_enclosing_radius() const {
	return length;
}

void SeparationRayShape3D::_update_shape() {
	Dictionary d;
	d["length"] = length;
	d["slide_on_slope"] = slide_on_slope;
	PhysicsSystem3D::get_singleton()->shape_set_data(get_shape(), d);
	Shape3D::_update_shape();
}

void SeparationRayShape3D::set_length(float p_length) {
	length = p_length;
	_update_shape();
	emit_changed();
}

float SeparationRayShape3D::get_length() const {
	return length;
}

void SeparationRayShape3D::set_slide_on_slope(bool p_active) {
	slide_on_slope = p_active;
	_update_shape();
	emit_changed();
}

bool SeparationRayShape3D::get_slide_on_slope() const {
	return slide_on_slope;
}

void SeparationRayShape3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_length", "length"), &SeparationRayShape3D::set_length);
	ClassRegistry::bind_method(D_METHOD("get_length"), &SeparationRayShape3D::get_length);

	ClassRegistry::bind_method(D_METHOD("set_slide_on_slope", "active"), &SeparationRayShape3D::set_slide_on_slope);
	ClassRegistry::bind_method(D_METHOD("get_slide_on_slope"), &SeparationRayShape3D::get_slide_on_slope);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "length", PROPERTY_HINT_RANGE, "0.001,100,0.001,or_greater,suffix:m"), "set_length", "get_length");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "slide_on_slope"), "set_slide_on_slope", "get_slide_on_slope");
}

SeparationRayShape3D::SeparationRayShape3D() :
		Shape3D(PhysicsSystem3D::get_singleton()->shape_create(PS3DE::SHAPE_SEPARATION_RAY)) {
	/* Code copied from setters to prevent the use of uninitialized variables */
	_update_shape();
	emit_changed();
}
