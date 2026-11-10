/**************************************************************************/
/*  nav_region_builder_planar.h                                               */

#pragma once

#include "../nav_utils_planar.h"

struct NavRegionIterationBuild2D;

class NavRegionBuilder2D {
	static void _build_step_process_navmesh_data(NavRegionIterationBuild2D &r_build);
	static void _build_step_find_edge_connection_pairs(NavRegionIterationBuild2D &r_build);
	static void _build_step_merge_edge_connection_pairs(NavRegionIterationBuild2D &r_build);
	static void _build_update_iteration(NavRegionIterationBuild2D &r_build);

public:
	static Nav2D::PointKey get_point_key(const Vector2 &p_pos, const Vector2 &p_cell_size);
	static Nav2D::EdgeKey get_edge_key(const Vector2 &p_vertex1, const Vector2 &p_vertex2, const Vector2 &p_cell_size);

	static void build_iteration(NavRegionIterationBuild2D &r_build);
};
