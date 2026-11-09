/**************************************************************************/
/*  nav_map_builder_planar.h                                                  */

#pragma once

#include "../nav_utils_planar.h"

struct NavMapIterationBuild2D;

class NavMapBuilder2D {
	static void _build_step_gather_region_polygons(NavMapIterationBuild2D &r_build);
	static void _build_step_find_edge_connection_pairs(NavMapIterationBuild2D &r_build);
	static void _build_step_merge_edge_connection_pairs(NavMapIterationBuild2D &r_build);
	static void _build_step_edge_connection_margin_connections(NavMapIterationBuild2D &r_build);
	static void _build_step_navlink_connections(NavMapIterationBuild2D &r_build);
	static void _build_update_map_iteration(NavMapIterationBuild2D &r_build);

public:
	static Nav2D::PointKey get_point_key(const Vector2 &p_pos, const Vector2 &p_cell_size);

	static void build_navmap_iteration(NavMapIterationBuild2D &r_build);
};
