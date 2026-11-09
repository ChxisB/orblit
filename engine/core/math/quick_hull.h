/**************************************************************************/
/*  quick_hull.h                                                          */

#pragma once

#include "core/containers/list.h"
#include "core/math/geometry_spatial.h"

class QuickHull {
public:
	struct Edge {
		union {
			uint32_t vertices[2];
			uint64_t id = 0;
		};

		static uint32_t hash(const Edge &p_edge) {
			return hash_one_uint64(p_edge.id);
		}

		bool operator<(const Edge &p_edge) const {
			return id < p_edge.id;
		}
		bool operator==(const Edge &p_edge) const {
			return id == p_edge.id;
		}

		Edge(int p_vtx_a = 0, int p_vtx_b = 0) {
			if (p_vtx_a > p_vtx_b) {
				SWAP(p_vtx_a, p_vtx_b);
			}

			vertices[0] = p_vtx_a;
			vertices[1] = p_vtx_b;
		}
	};

	struct Face {
		Plane plane;
		uint32_t vertices[3] = { 0 };
		Vector<int> points_over;

		bool operator<(const Face &p_face) const {
			return points_over.size() < p_face.points_over.size();
		}
	};

private:
	struct FaceConnect {
		List<Face>::Element *left = nullptr;
		List<Face>::Element *right = nullptr;
	};
	struct RetFaceConnect {
		List<Geometry3D::MeshData::Face>::Element *left = nullptr;
		List<Geometry3D::MeshData::Face>::Element *right = nullptr;
	};

public:
	static uint32_t debug_stop_after;
	static Error build(const Vector<Vector3> &p_points, Geometry3D::MeshData &r_mesh);
};
