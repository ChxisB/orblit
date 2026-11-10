/**************************************************************************/
/*  world_boundary_shape_spatial.cpp                                           */

#include "world_boundary_shape_spatial.h"

#include "core/object/class_registry.h"
#include "scene/resources/mesh.h"
#include "systems/physics_spatial/physics_system_spatial.h"

Vector<Vector3> WorldBoundaryShape3D::get_debug_mesh_lines() const {
	Plane p = get_plane();

	Vector3 n1 = p.get_any_perpendicular_normal();
	Vector3 n2 = p.normal.cross(n1).normalized();

	Vector3 pface[4] = {
		p.normal * p.d + n1 * 10.0 + n2 * 10.0,
		p.normal * p.d + n1 * 10.0 + n2 * -10.0,
		p.normal * p.d + n1 * -10.0 + n2 * -10.0,
		p.normal * p.d + n1 * -10.0 + n2 * 10.0,
	};

	Vector<Vector3> points = {
		pface[0],
		pface[1],
		pface[1],
		pface[2],
		pface[2],
		pface[3],
		pface[3],
		pface[0],
		p.normal * p.d,
		p.normal * p.d + p.normal * 3
	};

	return points;
}

Ref<ArrayMesh> WorldBoundaryShape3D::get_debug_arraymesh_faces(const Color &p_modulate) const {
	Plane p = get_plane();

	Vector3 n1 = p.get_any_perpendicular_normal();
	Vector3 n2 = p.normal.cross(n1).normalized();

	Vector3 pface[4] = {
		p.normal * p.d + n1 * 10.0 + n2 * 10.0,
		p.normal * p.d + n1 * 10.0 + n2 * -10.0,
		p.normal * p.d + n1 * -10.0 + n2 * -10.0,
		p.normal * p.d + n1 * -10.0 + n2 * 10.0,
	};

	Vector<Vector3> points = {
		pface[0],
		pface[1],
		pface[2],
		pface[3],
	};

	Vector<Color> colors = {
		p_modulate,
		p_modulate,
		p_modulate,
		p_modulate,
	};

	Vector<int> indices = {
		0,
		1,
		2,
		0,
		2,
		3,
	};

	Ref<ArrayMesh> mesh = memnew(ArrayMesh);
	Array a;
	a.resize(Mesh::ARRAY_MAX);
	a[RSE::ARRAY_VERTEX] = points;
	a[RSE::ARRAY_COLOR] = colors;
	a[RSE::ARRAY_INDEX] = indices;
	mesh->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLES, a);

	return mesh;
}

void WorldBoundaryShape3D::_update_shape() {
	PhysicsSystem3D::get_singleton()->shape_set_data(get_shape(), plane);
	Shape3D::_update_shape();
}

void WorldBoundaryShape3D::set_plane(const Plane &p_plane) {
	plane = p_plane;
	_update_shape();
	emit_changed();
}

const Plane &WorldBoundaryShape3D::get_plane() const {
	return plane;
}

void WorldBoundaryShape3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_plane", "plane"), &WorldBoundaryShape3D::set_plane);
	ClassRegistry::bind_method(D_METHOD("get_plane"), &WorldBoundaryShape3D::get_plane);

	ADD_PROPERTY(PropertyInfo(Variant::PLANE, "plane", PROPERTY_HINT_NONE, "suffix:m"), "set_plane", "get_plane");
}

WorldBoundaryShape3D::WorldBoundaryShape3D() :
		Shape3D(PhysicsSystem3D::get_singleton()->shape_create(PS3DE::SHAPE_WORLD_BOUNDARY)) {
	set_plane(Plane(0, 1, 0, 0));
}
