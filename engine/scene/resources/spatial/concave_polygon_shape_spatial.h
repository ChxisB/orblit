/**************************************************************************/
/*  concave_polygon_shape_spatial.h                                            */

#pragma once

#include "scene/resources/spatial/shape_spatial.h"

class ArrayMesh;

class ConcavePolygonShape3D : public Shape3D {
	ORBLIT_CLASS(ConcavePolygonShape3D, Shape3D);

	Vector<Vector3> faces;
	bool backface_collision = false;

	struct DrawEdge {
		Vector3 a;
		Vector3 b;
		static uint32_t hash(const DrawEdge &p_edge) {
			uint32_t h = hash_murmur3_one_32(HashMapHasherDefault::hash(p_edge.a));
			return hash_murmur3_one_32(HashMapHasherDefault::hash(p_edge.b), h);
		}
		bool operator==(const DrawEdge &p_edge) const {
			return (a == p_edge.a && b == p_edge.b);
		}

		DrawEdge(const Vector3 &p_a = Vector3(), const Vector3 &p_b = Vector3()) {
			a = p_a;
			b = p_b;
			if (a < b) {
				SWAP(a, b);
			}
		}
	};

protected:
	static void register_members();

	virtual void _update_shape() override;

public:
	void set_faces(const Vector<Vector3> &p_faces);
	Vector<Vector3> get_faces() const;

	void set_backface_collision_enabled(bool p_enabled);
	bool is_backface_collision_enabled() const;

	virtual Vector<Vector3> get_debug_mesh_lines() const override;
	virtual Ref<ArrayMesh> get_debug_arraymesh_faces(const Color &p_modulate) const override;
	virtual real_t get_enclosing_radius() const override;

	ConcavePolygonShape3D();
};
