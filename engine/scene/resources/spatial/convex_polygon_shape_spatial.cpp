/**************************************************************************/
/*  convex_polygon_shape_spatial.cpp                                           */

#include "convex_polygon_shape_spatial.h"

#include "core/math/convex_hull.h"
#include "core/object/class_registry.h"
#include "scene/resources/mesh.h"
#include "systems/physics_spatial/physics_system_spatial.h"

Vector<Vector3> ConvexPolygonShape3D::get_debug_mesh_lines() const {
	Vector<Vector3> poly_points = get_points();

	if (poly_points.size() > 1) { // Need at least 2 points for a line.
		Vector<Vector3> varr = Variant(poly_points);
		Geometry3D::MeshData md;
		Error err = ConvexHullComputer::convex_hull(varr, md);
		if (err == OK) {
			Vector<Vector3> lines;
			lines.resize(md.edges.size() * 2);
			for (uint32_t i = 0; i < md.edges.size(); i++) {
				lines.write[i * 2 + 0] = md.vertices[md.edges[i].vertex_a];
				lines.write[i * 2 + 1] = md.vertices[md.edges[i].vertex_b];
			}
			return lines;
		}
	}

	return Vector<Vector3>();
}

Ref<ArrayMesh> ConvexPolygonShape3D::get_debug_arraymesh_faces(const Color &p_modulate) const {
	const Vector<Vector3> hull_points = get_points();

	Vector<Vector3> verts;
	Vector<Color> colors;
	Vector<int> indices;

	if (hull_points.size() >= 3) {
		Geometry3D::MeshData md;
		Error err = ConvexHullComputer::convex_hull(hull_points, md);
		if (err == OK) {
			verts = Vector<Vector3>(md.vertices);
			for (int i = 0; i < verts.size(); i++) {
				colors.push_back(p_modulate);
			}
			for (const Geometry3D::MeshData::Face &face : md.faces) {
				const int first_point = face.indices[0];
				const int indices_count = face.indices.size();
				for (int i = 1; i < indices_count - 1; i++) {
					indices.push_back(first_point);
					indices.push_back(face.indices[i]);
					indices.push_back(face.indices[i + 1]);
				}
			}
		}
	}

	Ref<ArrayMesh> mesh = memnew(ArrayMesh);
	Array a;
	a.resize(Mesh::ARRAY_MAX);
	a[RSE::ARRAY_VERTEX] = verts;
	a[RSE::ARRAY_COLOR] = colors;
	a[RSE::ARRAY_INDEX] = indices;
	mesh->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLES, a);

	return mesh;
}

real_t ConvexPolygonShape3D::get_enclosing_radius() const {
	Vector<Vector3> data = get_points();
	const Vector3 *read = data.ptr();
	real_t r = 0.0;
	for (int i(0); i < data.size(); i++) {
		r = MAX(read[i].length_squared(), r);
	}
	return Math::sqrt(r);
}

void ConvexPolygonShape3D::_update_shape() {
	PhysicsSystem3D::get_singleton()->shape_set_data(get_shape(), points);
	Shape3D::_update_shape();
}

void ConvexPolygonShape3D::set_points(const Vector<Vector3> &p_points) {
	points = p_points;
	_update_shape();
	emit_changed();
}

Vector<Vector3> ConvexPolygonShape3D::get_points() const {
	return points;
}

void ConvexPolygonShape3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_points", "points"), &ConvexPolygonShape3D::set_points);
	ClassRegistry::bind_method(D_METHOD("get_points"), &ConvexPolygonShape3D::get_points);

	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "points"), "set_points", "get_points");
}

ConvexPolygonShape3D::ConvexPolygonShape3D() :
		Shape3D(PhysicsSystem3D::get_singleton()->shape_create(PS3DE::SHAPE_CONVEX_POLYGON)) {
}
