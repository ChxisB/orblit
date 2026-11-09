/**************************************************************************/
/*  nav_region_builder_spatial.h                                               */

#pragma once

#include "../nav_utils_spatial.h"

struct NavRegionIterationBuild3D;

class NavRegionBuilder3D {
	static void _build_step_process_navmesh_data(NavRegionIterationBuild3D &r_build);
	static void _build_step_find_edge_connection_pairs(NavRegionIterationBuild3D &r_build);
	static void _build_step_merge_edge_connection_pairs(NavRegionIterationBuild3D &r_build);
	static void _build_update_iteration(NavRegionIterationBuild3D &r_build);

public:
	static Nav3D::PointKey get_point_key(const Vector3 &p_pos, const Vector3 &p_cell_size);
	static Nav3D::EdgeKey get_edge_key(const Vector3 &p_vertex1, const Vector3 &p_vertex2, const Vector3 &p_cell_size);

	static void build_iteration(NavRegionIterationBuild3D &r_build);
};
