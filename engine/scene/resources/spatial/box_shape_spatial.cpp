/**************************************************************************/
/*  box_shape_spatial.cpp                                                      */

#include "box_shape_spatial.h"

#include "core/object/class_registry.h"
#include "scene/resources/spatial/primitive_meshes.h"
#include "systems/physics_spatial/physics_system_spatial.h"

Vector<Vector3> BoxShape3D::get_debug_mesh_lines() const {
	Vector<Vector3> lines;
	AABB aabb;
	aabb.position = -size / 2;
	aabb.size = size;

	for (int i = 0; i < 12; i++) {
		Vector3 a, b;
		aabb.get_edge(i, a, b);
		lines.push_back(a);
		lines.push_back(b);
	}

	return lines;
}

Ref<ArrayMesh> BoxShape3D::get_debug_arraymesh_faces(const Color &p_modulate) const {
	Array box_array;
	box_array.resize(RSE::ARRAY_MAX);
	BoxMesh::create_mesh_array(box_array, size);

	Vector<Color> colors;
	const PackedVector3Array &verts = box_array[RSE::ARRAY_VERTEX];
	const int32_t verts_size = verts.size();
	for (int i = 0; i < verts_size; i++) {
		colors.append(p_modulate);
	}

	Ref<ArrayMesh> box_mesh = memnew(ArrayMesh);
	box_array[RSE::ARRAY_COLOR] = colors;
	box_mesh->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLES, box_array);
	return box_mesh;
}

real_t BoxShape3D::get_enclosing_radius() const {
	return size.length() / 2;
}

void BoxShape3D::_update_shape() {
	PhysicsSystem3D::get_singleton()->shape_set_data(get_shape(), size / 2);
	Shape3D::_update_shape();
}

void BoxShape3D::set_size(const Vector3 &p_size) {
	ERR_FAIL_COND_MSG(p_size.x < 0 || p_size.y < 0 || p_size.z < 0, "BoxShape3D size cannot be negative.");
	size = p_size;
	_update_shape();
	emit_changed();
}

Vector3 BoxShape3D::get_size() const {
	return size;
}

void BoxShape3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_size", "size"), &BoxShape3D::set_size);
	ClassRegistry::bind_method(D_METHOD("get_size"), &BoxShape3D::get_size);

	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "size", PROPERTY_HINT_NONE, "suffix:m"), "set_size", "get_size");
}

BoxShape3D::BoxShape3D() :
		Shape3D(PhysicsSystem3D::get_singleton()->shape_create(PS3DE::SHAPE_BOX)) {
	set_size(Vector3(1, 1, 1));
}
