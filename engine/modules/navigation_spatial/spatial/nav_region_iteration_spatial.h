/**************************************************************************/
/*  nav_region_iteration_spatial.h                                             */

#pragma once

#include "../nav_utils_spatial.h"
#include "nav_base_iteration_spatial.h"

#include "core/math/aabb.h"

class NavRegion3D;
class NavRegionIteration3D;

struct NavRegionIterationBuild3D {
	Nav3D::PerformanceData performance_data;

	NavRegion3D *region = nullptr;

	Vector3 map_cell_size;
	Transform3D region_transform;

	struct NavMeshData {
		Vector<Vector3> vertices;
		Vector<Vector<int>> polygons;

		void clear() {
			vertices.clear();
			polygons.clear();
		}
	} navmesh_data;

	Ref<NavRegionIteration3D> region_iteration;

	HashMap<Nav3D::EdgeKey, Nav3D::EdgeConnectionPair, Nav3D::EdgeKey> iter_connection_pairs_map;

	void reset() {
		performance_data.reset();

		navmesh_data.clear();
		region_iteration = Ref<NavRegionIteration3D>();
		iter_connection_pairs_map.clear();
	}
};

class NavRegionIteration3D : public NavBaseIteration3D {
	ORBLIT_CLASS(NavRegionIteration3D, NavBaseIteration3D);

public:
	Transform3D transform;
	real_t surface_area = 0.0;
	AABB bounds;
	LocalVector<Nav3D::ConnectableEdge> external_edges;

	const Transform3D &get_transform() const { return transform; }
	real_t get_surface_area() const { return surface_area; }
	AABB get_bounds() const { return bounds; }
	const LocalVector<Nav3D::ConnectableEdge> &get_external_edges() const { return external_edges; }

	virtual ~NavRegionIteration3D() override {
		external_edges.clear();
		navmesh_polygons.clear();
		internal_connections.clear();
	}
};
