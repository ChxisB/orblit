/**************************************************************************/
/*  concave_polygon_shape_spatial.cpp                                          */

#include "concave_polygon_shape_spatial.h"

#include "core/object/class_registry.h"
#include "scene/resources/mesh.h"
#include "systems/physics_spatial/physics_system_spatial.h"

Vector<Vector3> ConcavePolygonShape3D::get_debug_mesh_lines() const {
	HashSet<DrawEdge, DrawEdge> edges;

	int index_count = faces.size();
	ERR_FAIL_COND_V((index_count % 3) != 0, Vector<Vector3>());

	const Vector3 *r = faces.ptr();

	for (int i = 0; i < index_count; i += 3) {
		for (int j = 0; j < 3; j++) {
			DrawEdge de(r[i + j], r[i + ((j + 1) % 3)]);
			edges.insert(de);
		}
	}

	Vector<Vector3> points;
	points.resize(edges.size() * 2);
	int idx = 0;
	for (const DrawEdge &E : edges) {
		points.write[idx + 0] = E.a;
		points.write[idx + 1] = E.b;
		idx += 2;
	}

	return points;
}

Ref<ArrayMesh> ConcavePolygonShape3D::get_debug_arraymesh_faces(const Color &p_modulate) const {
	Vector<Color> colors;

	for (int i = 0; i < faces.size(); i++) {
		colors.push_back(p_modulate);
	}

	Ref<ArrayMesh> mesh = memnew(ArrayMesh);
	Array a;
	a.resize(Mesh::ARRAY_MAX);
	a[RSE::ARRAY_VERTEX] = faces;
	a[RSE::ARRAY_COLOR] = colors;
	mesh->add_surface_from_arrays(Mesh::PRIMITIVE_TRIANGLES, a);

	return mesh;
}

real_t ConcavePolygonShape3D::get_enclosing_radius() const {
	Vector<Vector3> data = get_faces();
	const Vector3 *read = data.ptr();
	real_t r = 0.0;
	for (int i(0); i < data.size(); i++) {
		r = MAX(read[i].length_squared(), r);
	}
	return Math::sqrt(r);
}

void ConcavePolygonShape3D::_update_shape() {
	Dictionary d;
	d["faces"] = faces;
	d["backface_collision"] = backface_collision;
	PhysicsSystem3D::get_singleton()->shape_set_data(get_shape(), d);

	Shape3D::_update_shape();
}

void ConcavePolygonShape3D::set_faces(const Vector<Vector3> &p_faces) {
	faces = p_faces;
	_update_shape();
	emit_changed();
}

Vector<Vector3> ConcavePolygonShape3D::get_faces() const {
	return faces;
}

void ConcavePolygonShape3D::set_backface_collision_enabled(bool p_enabled) {
	backface_collision = p_enabled;

	if (!faces.is_empty()) {
		_update_shape();
		emit_changed();
	}
}

bool ConcavePolygonShape3D::is_backface_collision_enabled() const {
	return backface_collision;
}

void ConcavePolygonShape3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_faces", "faces"), &ConcavePolygonShape3D::set_faces);
	ClassRegistry::bind_method(D_METHOD("get_faces"), &ConcavePolygonShape3D::get_faces);

	ClassRegistry::bind_method(D_METHOD("set_backface_collision_enabled", "enabled"), &ConcavePolygonShape3D::set_backface_collision_enabled);
	ClassRegistry::bind_method(D_METHOD("is_backface_collision_enabled"), &ConcavePolygonShape3D::is_backface_collision_enabled);

	ADD_PROPERTY(PropertyInfo(Variant::PACKED_VECTOR3_ARRAY, "data", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR | PROPERTY_USAGE_INTERNAL), "set_faces", "get_faces");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "backface_collision"), "set_backface_collision_enabled", "is_backface_collision_enabled");
}

ConcavePolygonShape3D::ConcavePolygonShape3D() :
		Shape3D(PhysicsSystem3D::get_singleton()->shape_create(PS3DE::SHAPE_CONCAVE_POLYGON)) {
	//set_planes(Vector3(1,1,1));
}
