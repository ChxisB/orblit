/**************************************************************************/
/*  triangle_mesh.h                                                       */

#pragma once

#include "core/math/face3.h"
#include "core/object/ref_counted.h"

class TriangleMesh : public RefCounted {
	ORBLIT_CLASS(TriangleMesh, RefCounted);

public:
	struct Triangle {
		Vector3 normal;
		int indices[3];
		int32_t surface_index = 0;
	};

	struct BVH {
		AABB aabb;
		Vector3 center; //used for sorting
		int left = -1;
		int right = -1;
		int32_t face_index = -1;
	};

protected:
	static void register_members();

private:
	Vector<Triangle> triangles;
	Vector<Vector3> vertices;

	struct BVHCmpX {
		bool operator()(const BVH *p_left, const BVH *p_right) const {
			return p_left->center.x < p_right->center.x;
		}
	};

	struct BVHCmpY {
		bool operator()(const BVH *p_left, const BVH *p_right) const {
			return p_left->center.y < p_right->center.y;
		}
	};
	struct BVHCmpZ {
		bool operator()(const BVH *p_left, const BVH *p_right) const {
			return p_left->center.z < p_right->center.z;
		}
	};

	int _create_bvh(BVH *p_bvh, BVH **p_bb, int p_from, int p_size, int p_depth, int &r_max_depth, int &r_max_alloc);

	Vector<BVH> bvh;
	int max_depth = 0;
	bool valid = false;

public:
	bool is_valid() const;
	bool intersect_segment(const Vector3 &p_begin, const Vector3 &p_end, Vector3 &r_point, Vector3 &r_normal, int32_t *r_surf_index = nullptr, int32_t *r_face_index = nullptr) const;
	bool intersect_ray(const Vector3 &p_begin, const Vector3 &p_dir, Vector3 &r_point, Vector3 &r_normal, int32_t *r_surf_index = nullptr, int32_t *r_face_index = nullptr) const;
	bool inside_convex_shape(const Plane *p_planes, int p_plane_count, const Vector3 *p_points, int p_point_count, Vector3 p_scale = Vector3(1, 1, 1)) const;
	Vector<Face3> get_faces() const;
	AABB get_aabb() const;

	const Vector<Triangle> &get_triangles() const { return triangles; }
	const Vector<Vector3> &get_vertices() const { return vertices; }
	const Vector<BVH> &get_bvh() const { return bvh; }
	void get_indices(Vector<int> *r_triangles_indices) const;

	void create(const Vector<Vector3> &p_faces, const Vector<int32_t> &p_surface_indices = Vector<int32_t>());

	// Wrapped functions for compatibility with method bindings
	// and user exposed script api that can't use more native types.
	bool create_from_faces(const Vector<Vector3> &p_faces);
	Dictionary intersect_segment_scriptwrap(const Vector3 &p_begin, const Vector3 &p_end) const;
	Dictionary intersect_ray_scriptwrap(const Vector3 &p_begin, const Vector3 &p_dir) const;
	Vector<Vector3> get_faces_scriptwrap() const;

	TriangleMesh();
};
