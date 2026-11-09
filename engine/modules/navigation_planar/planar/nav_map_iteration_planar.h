/**************************************************************************/
/*  nav_map_iteration_planar.h                                                */

#pragma once

#include "../nav_utils_planar.h"
#include "nav_mesh_queries_planar.h"

#include "core/math/math_defs.h"
#include "core/os/rw_lock.h"
#include "core/os/semaphore.h"

class NavLinkIteration2D;
class NavRegion2D;
class NavRegionIteration2D;
struct NavMapIteration2D;

struct NavMapIterationBuild2D {
	Vector2 merge_rasterizer_cell_size;
	bool use_edge_connections = true;
	real_t edge_connection_margin;
	real_t link_connection_radius;
	Nav2D::PerformanceData performance_data;
	int polygon_count = 0;
	int free_edge_count = 0;

	HashMap<Nav2D::EdgeKey, Nav2D::EdgeConnectionPair, Nav2D::EdgeKey> iter_connection_pairs_map;
	LocalVector<Nav2D::Connection> iter_free_edges;

	NavMapIteration2D *map_iteration = nullptr;

	int navmesh_polygon_count = 0;

	void reset() {
		performance_data.reset();

		iter_connection_pairs_map.clear();
		iter_free_edges.clear();
		polygon_count = 0;
		free_edge_count = 0;

		navmesh_polygon_count = 0;
	}
};

struct NavMapIteration2D {
	mutable SafeNumeric<uint32_t> users;
	RWLock rwlock;

	LocalVector<Ref<NavRegionIteration2D>> region_iterations;
	LocalVector<Ref<NavLinkIteration2D>> link_iterations;

	int navmesh_polygon_count = 0;

	// The edge connections that the map builds on top with the edge connection margin.
	HashMap<const NavBaseIteration2D *, LocalVector<Nav2D::Connection>> external_region_connections;
	HashMap<const NavBaseIteration2D *, LocalVector<LocalVector<Nav2D::Connection>>> navbases_polygons_external_connections;

	LocalVector<Nav2D::Polygon> navlink_polygons;

	HashMap<NavRegion2D *, Ref<NavRegionIteration2D>> region_ptr_to_region_iteration;

	LocalVector<NavMeshQueries2D::PathQuerySlot> path_query_slots;
	Mutex path_query_slots_mutex;
	Semaphore path_query_slots_semaphore;

	void clear() {
		navmesh_polygon_count = 0;

		region_iterations.clear();
		link_iterations.clear();
		external_region_connections.clear();
		navbases_polygons_external_connections.clear();
		navlink_polygons.clear();
		region_ptr_to_region_iteration.clear();
	}
};

class NavMapIterationRead2D {
	const NavMapIteration2D &map_iteration;

public:
	_ALWAYS_INLINE_ NavMapIterationRead2D(const NavMapIteration2D &p_iteration) :
			map_iteration(p_iteration) {
		map_iteration.rwlock.read_lock();
		map_iteration.users.increment();
	}
	_ALWAYS_INLINE_ ~NavMapIterationRead2D() {
		map_iteration.users.decrement();
		map_iteration.rwlock.read_unlock();
	}
};
