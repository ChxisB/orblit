/**************************************************************************/
/*  nav_map_builder_spatial.h                                                  */

#pragma once

#include "../nav_utils_spatial.h"

struct NavMapIterationBuild3D;

class NavMapBuilder3D {
	static void _build_step_gather_region_polygons(NavMapIterationBuild3D &r_build);
	static void _build_step_find_edge_connection_pairs(NavMapIterationBuild3D &r_build);
	static void _build_step_merge_edge_connection_pairs(NavMapIterationBuild3D &r_build);
	static void _build_step_edge_connection_margin_connections(NavMapIterationBuild3D &r_build);
	static void _build_step_navlink_connections(NavMapIterationBuild3D &r_build);
	static void _build_update_map_iteration(NavMapIterationBuild3D &r_build);

public:
	static Nav3D::PointKey get_point_key(const Vector3 &p_pos, const Vector3 &p_cell_size);

	static void build_navmap_iteration(NavMapIterationBuild3D &r_build);
};
