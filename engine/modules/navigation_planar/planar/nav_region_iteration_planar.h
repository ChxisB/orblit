/**************************************************************************/
/*  nav_region_iteration_planar.h                                             */

#pragma once

#include "../nav_utils_planar.h"
#include "nav_base_iteration_planar.h"

#include "core/math/rect2.h"

class NavRegion2D;
class NavRegionIteration2D;

struct NavRegionIterationBuild2D {
	Nav2D::PerformanceData performance_data;

	NavRegion2D *region = nullptr;

	Vector2 map_cell_size;
	Transform2D region_transform;

	struct NavMeshData {
		Vector<Vector2> vertices;
		Vector<Vector<int>> polygons;

		void clear() {
			vertices.clear();
			polygons.clear();
		}
	} navmesh_data;

	Ref<NavRegionIteration2D> region_iteration;

	HashMap<Nav2D::EdgeKey, Nav2D::EdgeConnectionPair, Nav2D::EdgeKey> iter_connection_pairs_map;

	void reset() {
		performance_data.reset();

		navmesh_data.clear();
		region_iteration = Ref<NavRegionIteration2D>();
		iter_connection_pairs_map.clear();
	}
};

class NavRegionIteration2D : public NavBaseIteration2D {
	ORBLIT_CLASS(NavRegionIteration2D, NavBaseIteration2D);

public:
	Transform2D transform;
	real_t surface_area = 0.0;
	Rect2 bounds;
	LocalVector<Nav2D::ConnectableEdge> external_edges;

	const Transform2D &get_transform() const { return transform; }
	real_t get_surface_area() const { return surface_area; }
	Rect2 get_bounds() const { return bounds; }
	const LocalVector<Nav2D::ConnectableEdge> &get_external_edges() const { return external_edges; }

	virtual ~NavRegionIteration2D() override {
		external_edges.clear();
		navmesh_polygons.clear();
		internal_connections.clear();
	}
};
