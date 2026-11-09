/**************************************************************************/
/*  navigation_system_spatial.cpp                                              */

#include "navigation_system_spatial.h"

#include "core/config/engine.h"
#include "core/config/project_settings.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "scene/stage/entity.h" // IWYU pragma: keep. Needed to bind `Entity *` arg.
#include "scene/resources/spatial/navigation_mesh_source_geometry_data_spatial.h" // IWYU pragma: keep. Bind `NavigationMeshSourceGeometryData3D`.
#include "scene/resources/navigation_mesh.h" // IWYU pragma: keep. Bind `NavigationMesh`.
#include "systems/navigation_spatial/navigation_path_query_parameters_spatial.h" // IWYU pragma: keep. Bind `NavigationPathQueryParameters3D`.
#include "systems/navigation_spatial/navigation_path_query_result_spatial.h" // IWYU pragma: keep. Bind `NavigationPathQueryResult3D`.

NavigationSystem3D *NavigationSystem3D::singleton = nullptr;

RWLock NavigationSystem3D::geometry_parser_rwlock;
RID_Owner<NavMeshGeometryParser3D> NavigationSystem3D::geometry_parser_owner;
LocalVector<NavMeshGeometryParser3D *> NavigationSystem3D::generator_parsers;

void NavigationSystem3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_maps"), &NavigationSystem3D::get_maps);

	ClassRegistry::bind_method(D_METHOD("map_create"), &NavigationSystem3D::map_create);
	ClassRegistry::bind_method(D_METHOD("map_set_active", "map", "active"), &NavigationSystem3D::map_set_active);
	ClassRegistry::bind_method(D_METHOD("map_is_active", "map"), &NavigationSystem3D::map_is_active);
	ClassRegistry::bind_method(D_METHOD("map_set_up", "map", "up"), &NavigationSystem3D::map_set_up);
	ClassRegistry::bind_method(D_METHOD("map_get_up", "map"), &NavigationSystem3D::map_get_up);
	ClassRegistry::bind_method(D_METHOD("map_set_cell_size", "map", "cell_size"), &NavigationSystem3D::map_set_cell_size);
	ClassRegistry::bind_method(D_METHOD("map_get_cell_size", "map"), &NavigationSystem3D::map_get_cell_size);
	ClassRegistry::bind_method(D_METHOD("map_set_cell_height", "map", "cell_height"), &NavigationSystem3D::map_set_cell_height);
	ClassRegistry::bind_method(D_METHOD("map_get_cell_height", "map"), &NavigationSystem3D::map_get_cell_height);
	ClassRegistry::bind_method(D_METHOD("map_set_merge_rasterizer_cell_scale", "map", "scale"), &NavigationSystem3D::map_set_merge_rasterizer_cell_scale);
	ClassRegistry::bind_method(D_METHOD("map_get_merge_rasterizer_cell_scale", "map"), &NavigationSystem3D::map_get_merge_rasterizer_cell_scale);
	ClassRegistry::bind_method(D_METHOD("map_set_use_edge_connections", "map", "enabled"), &NavigationSystem3D::map_set_use_edge_connections);
	ClassRegistry::bind_method(D_METHOD("map_get_use_edge_connections", "map"), &NavigationSystem3D::map_get_use_edge_connections);
	ClassRegistry::bind_method(D_METHOD("map_set_edge_connection_margin", "map", "margin"), &NavigationSystem3D::map_set_edge_connection_margin);
	ClassRegistry::bind_method(D_METHOD("map_get_edge_connection_margin", "map"), &NavigationSystem3D::map_get_edge_connection_margin);
	ClassRegistry::bind_method(D_METHOD("map_set_link_connection_radius", "map", "radius"), &NavigationSystem3D::map_set_link_connection_radius);
	ClassRegistry::bind_method(D_METHOD("map_get_link_connection_radius", "map"), &NavigationSystem3D::map_get_link_connection_radius);
	ClassRegistry::bind_method(D_METHOD("map_get_path", "map", "origin", "destination", "optimize", "navigation_layers"), &NavigationSystem3D::map_get_path, DEFVAL(1));
	ClassRegistry::bind_method(D_METHOD("map_get_closest_point_to_segment", "map", "start", "end", "use_collision"), &NavigationSystem3D::map_get_closest_point_to_segment, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("map_get_closest_point", "map", "to_point"), &NavigationSystem3D::map_get_closest_point);
	ClassRegistry::bind_method(D_METHOD("map_get_closest_point_normal", "map", "to_point"), &NavigationSystem3D::map_get_closest_point_normal);
	ClassRegistry::bind_method(D_METHOD("map_get_closest_point_owner", "map", "to_point"), &NavigationSystem3D::map_get_closest_point_owner);

	ClassRegistry::bind_method(D_METHOD("map_get_links", "map"), &NavigationSystem3D::map_get_links);
	ClassRegistry::bind_method(D_METHOD("map_get_regions", "map"), &NavigationSystem3D::map_get_regions);
	ClassRegistry::bind_method(D_METHOD("map_get_agents", "map"), &NavigationSystem3D::map_get_agents);
	ClassRegistry::bind_method(D_METHOD("map_get_obstacles", "map"), &NavigationSystem3D::map_get_obstacles);

	ClassRegistry::bind_method(D_METHOD("map_get_iteration_id", "map"), &NavigationSystem3D::map_get_iteration_id);
	ClassRegistry::bind_method(D_METHOD("map_set_use_async_iterations", "map", "enabled"), &NavigationSystem3D::map_set_use_async_iterations);
	ClassRegistry::bind_method(D_METHOD("map_get_use_async_iterations", "map"), &NavigationSystem3D::map_get_use_async_iterations);

	ClassRegistry::bind_method(D_METHOD("map_get_random_point", "map", "navigation_layers", "uniformly"), &NavigationSystem3D::map_get_random_point);

	ClassRegistry::bind_method(D_METHOD("query_path", "parameters", "result", "callback"), &NavigationSystem3D::query_path, DEFVAL(Callable()));

	ClassRegistry::bind_method(D_METHOD("region_create"), &NavigationSystem3D::region_create);
	ClassRegistry::bind_method(D_METHOD("region_get_iteration_id", "region"), &NavigationSystem3D::region_get_iteration_id);
	ClassRegistry::bind_method(D_METHOD("region_set_use_async_iterations", "region", "enabled"), &NavigationSystem3D::region_set_use_async_iterations);
	ClassRegistry::bind_method(D_METHOD("region_get_use_async_iterations", "region"), &NavigationSystem3D::region_get_use_async_iterations);
	ClassRegistry::bind_method(D_METHOD("region_set_enabled", "region", "enabled"), &NavigationSystem3D::region_set_enabled);
	ClassRegistry::bind_method(D_METHOD("region_get_enabled", "region"), &NavigationSystem3D::region_get_enabled);
	ClassRegistry::bind_method(D_METHOD("region_set_use_edge_connections", "region", "enabled"), &NavigationSystem3D::region_set_use_edge_connections);
	ClassRegistry::bind_method(D_METHOD("region_get_use_edge_connections", "region"), &NavigationSystem3D::region_get_use_edge_connections);
	ClassRegistry::bind_method(D_METHOD("region_set_enter_cost", "region", "enter_cost"), &NavigationSystem3D::region_set_enter_cost);
	ClassRegistry::bind_method(D_METHOD("region_get_enter_cost", "region"), &NavigationSystem3D::region_get_enter_cost);
	ClassRegistry::bind_method(D_METHOD("region_set_travel_cost", "region", "travel_cost"), &NavigationSystem3D::region_set_travel_cost);
	ClassRegistry::bind_method(D_METHOD("region_get_travel_cost", "region"), &NavigationSystem3D::region_get_travel_cost);
	ClassRegistry::bind_method(D_METHOD("region_set_owner_id", "region", "owner_id"), &NavigationSystem3D::region_set_owner_id);
	ClassRegistry::bind_method(D_METHOD("region_get_owner_id", "region"), &NavigationSystem3D::region_get_owner_id);
	ClassRegistry::bind_method(D_METHOD("region_owns_point", "region", "point"), &NavigationSystem3D::region_owns_point);
	ClassRegistry::bind_method(D_METHOD("region_set_map", "region", "map"), &NavigationSystem3D::region_set_map);
	ClassRegistry::bind_method(D_METHOD("region_get_map", "region"), &NavigationSystem3D::region_get_map);
	ClassRegistry::bind_method(D_METHOD("region_set_navigation_layers", "region", "navigation_layers"), &NavigationSystem3D::region_set_navigation_layers);
	ClassRegistry::bind_method(D_METHOD("region_get_navigation_layers", "region"), &NavigationSystem3D::region_get_navigation_layers);
	ClassRegistry::bind_method(D_METHOD("region_set_transform", "region", "transform"), &NavigationSystem3D::region_set_transform);
	ClassRegistry::bind_method(D_METHOD("region_get_transform", "region"), &NavigationSystem3D::region_get_transform);
	ClassRegistry::bind_method(D_METHOD("region_set_navigation_mesh", "region", "navigation_mesh"), &NavigationSystem3D::region_set_navigation_mesh);
	ClassRegistry::bind_method(D_METHOD("region_get_connections_count", "region"), &NavigationSystem3D::region_get_connections_count);
	ClassRegistry::bind_method(D_METHOD("region_get_connection_pathway_start", "region", "connection"), &NavigationSystem3D::region_get_connection_pathway_start);
	ClassRegistry::bind_method(D_METHOD("region_get_connection_pathway_end", "region", "connection"), &NavigationSystem3D::region_get_connection_pathway_end);
	ClassRegistry::bind_method(D_METHOD("region_get_closest_point_to_segment", "region", "start", "end", "use_collision"), &NavigationSystem3D::region_get_closest_point_to_segment, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("region_get_closest_point", "region", "to_point"), &NavigationSystem3D::region_get_closest_point);
	ClassRegistry::bind_method(D_METHOD("region_get_closest_point_normal", "region", "to_point"), &NavigationSystem3D::region_get_closest_point_normal);
	ClassRegistry::bind_method(D_METHOD("region_get_random_point", "region", "navigation_layers", "uniformly"), &NavigationSystem3D::region_get_random_point);
	ClassRegistry::bind_method(D_METHOD("region_get_bounds", "region"), &NavigationSystem3D::region_get_bounds);

	ClassRegistry::bind_method(D_METHOD("link_create"), &NavigationSystem3D::link_create);
	ClassRegistry::bind_method(D_METHOD("link_get_iteration_id", "link"), &NavigationSystem3D::link_get_iteration_id);
	ClassRegistry::bind_method(D_METHOD("link_set_map", "link", "map"), &NavigationSystem3D::link_set_map);
	ClassRegistry::bind_method(D_METHOD("link_get_map", "link"), &NavigationSystem3D::link_get_map);
	ClassRegistry::bind_method(D_METHOD("link_set_enabled", "link", "enabled"), &NavigationSystem3D::link_set_enabled);
	ClassRegistry::bind_method(D_METHOD("link_get_enabled", "link"), &NavigationSystem3D::link_get_enabled);
	ClassRegistry::bind_method(D_METHOD("link_set_bidirectional", "link", "bidirectional"), &NavigationSystem3D::link_set_bidirectional);
	ClassRegistry::bind_method(D_METHOD("link_is_bidirectional", "link"), &NavigationSystem3D::link_is_bidirectional);
	ClassRegistry::bind_method(D_METHOD("link_set_navigation_layers", "link", "navigation_layers"), &NavigationSystem3D::link_set_navigation_layers);
	ClassRegistry::bind_method(D_METHOD("link_get_navigation_layers", "link"), &NavigationSystem3D::link_get_navigation_layers);
	ClassRegistry::bind_method(D_METHOD("link_set_start_position", "link", "position"), &NavigationSystem3D::link_set_start_position);
	ClassRegistry::bind_method(D_METHOD("link_get_start_position", "link"), &NavigationSystem3D::link_get_start_position);
	ClassRegistry::bind_method(D_METHOD("link_set_end_position", "link", "position"), &NavigationSystem3D::link_set_end_position);
	ClassRegistry::bind_method(D_METHOD("link_get_end_position", "link"), &NavigationSystem3D::link_get_end_position);
	ClassRegistry::bind_method(D_METHOD("link_set_enter_cost", "link", "enter_cost"), &NavigationSystem3D::link_set_enter_cost);
	ClassRegistry::bind_method(D_METHOD("link_get_enter_cost", "link"), &NavigationSystem3D::link_get_enter_cost);
	ClassRegistry::bind_method(D_METHOD("link_set_travel_cost", "link", "travel_cost"), &NavigationSystem3D::link_set_travel_cost);
	ClassRegistry::bind_method(D_METHOD("link_get_travel_cost", "link"), &NavigationSystem3D::link_get_travel_cost);
	ClassRegistry::bind_method(D_METHOD("link_set_owner_id", "link", "owner_id"), &NavigationSystem3D::link_set_owner_id);
	ClassRegistry::bind_method(D_METHOD("link_get_owner_id", "link"), &NavigationSystem3D::link_get_owner_id);

	ClassRegistry::bind_method(D_METHOD("agent_create"), &NavigationSystem3D::agent_create);
	ClassRegistry::bind_method(D_METHOD("agent_set_avoidance_enabled", "agent", "enabled"), &NavigationSystem3D::agent_set_avoidance_enabled);
	ClassRegistry::bind_method(D_METHOD("agent_get_avoidance_enabled", "agent"), &NavigationSystem3D::agent_get_avoidance_enabled);
	ClassRegistry::bind_method(D_METHOD("agent_set_use_3d_avoidance", "agent", "enabled"), &NavigationSystem3D::agent_set_use_3d_avoidance);
	ClassRegistry::bind_method(D_METHOD("agent_get_use_3d_avoidance", "agent"), &NavigationSystem3D::agent_get_use_3d_avoidance);

	ClassRegistry::bind_method(D_METHOD("agent_set_map", "agent", "map"), &NavigationSystem3D::agent_set_map);
	ClassRegistry::bind_method(D_METHOD("agent_get_map", "agent"), &NavigationSystem3D::agent_get_map);
	ClassRegistry::bind_method(D_METHOD("agent_set_paused", "agent", "paused"), &NavigationSystem3D::agent_set_paused);
	ClassRegistry::bind_method(D_METHOD("agent_get_paused", "agent"), &NavigationSystem3D::agent_get_paused);
	ClassRegistry::bind_method(D_METHOD("agent_set_neighbor_distance", "agent", "distance"), &NavigationSystem3D::agent_set_neighbor_distance);
	ClassRegistry::bind_method(D_METHOD("agent_get_neighbor_distance", "agent"), &NavigationSystem3D::agent_get_neighbor_distance);
	ClassRegistry::bind_method(D_METHOD("agent_set_max_neighbors", "agent", "count"), &NavigationSystem3D::agent_set_max_neighbors);
	ClassRegistry::bind_method(D_METHOD("agent_get_max_neighbors", "agent"), &NavigationSystem3D::agent_get_max_neighbors);
	ClassRegistry::bind_method(D_METHOD("agent_set_time_horizon_agents", "agent", "time_horizon"), &NavigationSystem3D::agent_set_time_horizon_agents);
	ClassRegistry::bind_method(D_METHOD("agent_get_time_horizon_agents", "agent"), &NavigationSystem3D::agent_get_time_horizon_agents);
	ClassRegistry::bind_method(D_METHOD("agent_set_time_horizon_obstacles", "agent", "time_horizon"), &NavigationSystem3D::agent_set_time_horizon_obstacles);
	ClassRegistry::bind_method(D_METHOD("agent_get_time_horizon_obstacles", "agent"), &NavigationSystem3D::agent_get_time_horizon_obstacles);
	ClassRegistry::bind_method(D_METHOD("agent_set_radius", "agent", "radius"), &NavigationSystem3D::agent_set_radius);
	ClassRegistry::bind_method(D_METHOD("agent_get_radius", "agent"), &NavigationSystem3D::agent_get_radius);
	ClassRegistry::bind_method(D_METHOD("agent_set_height", "agent", "height"), &NavigationSystem3D::agent_set_height);
	ClassRegistry::bind_method(D_METHOD("agent_get_height", "agent"), &NavigationSystem3D::agent_get_height);
	ClassRegistry::bind_method(D_METHOD("agent_set_max_speed", "agent", "max_speed"), &NavigationSystem3D::agent_set_max_speed);
	ClassRegistry::bind_method(D_METHOD("agent_get_max_speed", "agent"), &NavigationSystem3D::agent_get_max_speed);
	ClassRegistry::bind_method(D_METHOD("agent_set_velocity_forced", "agent", "velocity"), &NavigationSystem3D::agent_set_velocity_forced);
	ClassRegistry::bind_method(D_METHOD("agent_set_velocity", "agent", "velocity"), &NavigationSystem3D::agent_set_velocity);
	ClassRegistry::bind_method(D_METHOD("agent_get_velocity", "agent"), &NavigationSystem3D::agent_get_velocity);
	ClassRegistry::bind_method(D_METHOD("agent_set_position", "agent", "position"), &NavigationSystem3D::agent_set_position);
	ClassRegistry::bind_method(D_METHOD("agent_get_position", "agent"), &NavigationSystem3D::agent_get_position);
	ClassRegistry::bind_method(D_METHOD("agent_is_map_changed", "agent"), &NavigationSystem3D::agent_is_map_changed);
	ClassRegistry::bind_method(D_METHOD("agent_set_avoidance_callback", "agent", "callback"), &NavigationSystem3D::agent_set_avoidance_callback);
	ClassRegistry::bind_method(D_METHOD("agent_has_avoidance_callback", "agent"), &NavigationSystem3D::agent_has_avoidance_callback);
	ClassRegistry::bind_method(D_METHOD("agent_set_avoidance_layers", "agent", "layers"), &NavigationSystem3D::agent_set_avoidance_layers);
	ClassRegistry::bind_method(D_METHOD("agent_get_avoidance_layers", "agent"), &NavigationSystem3D::agent_get_avoidance_layers);
	ClassRegistry::bind_method(D_METHOD("agent_set_avoidance_mask", "agent", "mask"), &NavigationSystem3D::agent_set_avoidance_mask);
	ClassRegistry::bind_method(D_METHOD("agent_get_avoidance_mask", "agent"), &NavigationSystem3D::agent_get_avoidance_mask);
	ClassRegistry::bind_method(D_METHOD("agent_set_avoidance_priority", "agent", "priority"), &NavigationSystem3D::agent_set_avoidance_priority);
	ClassRegistry::bind_method(D_METHOD("agent_get_avoidance_priority", "agent"), &NavigationSystem3D::agent_get_avoidance_priority);

	ClassRegistry::bind_method(D_METHOD("obstacle_create"), &NavigationSystem3D::obstacle_create);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_avoidance_enabled", "obstacle", "enabled"), &NavigationSystem3D::obstacle_set_avoidance_enabled);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_avoidance_enabled", "obstacle"), &NavigationSystem3D::obstacle_get_avoidance_enabled);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_use_3d_avoidance", "obstacle", "enabled"), &NavigationSystem3D::obstacle_set_use_3d_avoidance);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_use_3d_avoidance", "obstacle"), &NavigationSystem3D::obstacle_get_use_3d_avoidance);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_map", "obstacle", "map"), &NavigationSystem3D::obstacle_set_map);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_map", "obstacle"), &NavigationSystem3D::obstacle_get_map);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_paused", "obstacle", "paused"), &NavigationSystem3D::obstacle_set_paused);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_paused", "obstacle"), &NavigationSystem3D::obstacle_get_paused);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_radius", "obstacle", "radius"), &NavigationSystem3D::obstacle_set_radius);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_radius", "obstacle"), &NavigationSystem3D::obstacle_get_radius);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_height", "obstacle", "height"), &NavigationSystem3D::obstacle_set_height);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_height", "obstacle"), &NavigationSystem3D::obstacle_get_height);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_velocity", "obstacle", "velocity"), &NavigationSystem3D::obstacle_set_velocity);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_velocity", "obstacle"), &NavigationSystem3D::obstacle_get_velocity);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_position", "obstacle", "position"), &NavigationSystem3D::obstacle_set_position);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_position", "obstacle"), &NavigationSystem3D::obstacle_get_position);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_vertices", "obstacle", "vertices"), &NavigationSystem3D::obstacle_set_vertices);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_vertices", "obstacle"), &NavigationSystem3D::obstacle_get_vertices);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_avoidance_layers", "obstacle", "layers"), &NavigationSystem3D::obstacle_set_avoidance_layers);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_avoidance_layers", "obstacle"), &NavigationSystem3D::obstacle_get_avoidance_layers);

#ifndef SPATIAL_DISABLED
	ClassRegistry::bind_method(D_METHOD("parse_source_geometry_data", "navigation_mesh", "source_geometry_data", "root_node", "callback"), &NavigationSystem3D::parse_source_geometry_data, DEFVAL(Callable()));
	ClassRegistry::bind_method(D_METHOD("bake_from_source_geometry_data", "navigation_mesh", "source_geometry_data", "callback"), &NavigationSystem3D::bake_from_source_geometry_data, DEFVAL(Callable()));
	ClassRegistry::bind_method(D_METHOD("bake_from_source_geometry_data_async", "navigation_mesh", "source_geometry_data", "callback"), &NavigationSystem3D::bake_from_source_geometry_data_async, DEFVAL(Callable()));
	ClassRegistry::bind_method(D_METHOD("is_baking_navigation_mesh", "navigation_mesh"), &NavigationSystem3D::is_baking_navigation_mesh);
#endif // SPATIAL_DISABLED

	ClassRegistry::bind_method(D_METHOD("source_geometry_parser_create"), &NavigationSystem3D::source_geometry_parser_create);
	ClassRegistry::bind_method(D_METHOD("source_geometry_parser_set_callback", "parser", "callback"), &NavigationSystem3D::source_geometry_parser_set_callback);

	ClassRegistry::bind_method(D_METHOD("simplify_path", "path", "epsilon"), &NavigationSystem3D::simplify_path);

	ClassRegistry::bind_method(D_METHOD("free_rid", "rid"), &NavigationSystem3D::free_rid);

	ClassRegistry::bind_method(D_METHOD("set_active", "active"), &NavigationSystem3D::set_active);

	ClassRegistry::bind_method(D_METHOD("set_debug_enabled", "enabled"), &NavigationSystem3D::set_debug_enabled);
	ClassRegistry::bind_method(D_METHOD("get_debug_enabled"), &NavigationSystem3D::get_debug_enabled);

	ADD_SIGNAL(MethodInfo("map_changed", PropertyInfo(Variant::RID, "map")));

	ADD_SIGNAL(MethodInfo("navigation_debug_changed"));
	ADD_SIGNAL(MethodInfo("avoidance_debug_changed"));

	ClassRegistry::bind_method(D_METHOD("get_process_info", "process_info"), &NavigationSystem3D::get_process_info);

	BIND_ENUM_CONSTANT(INFO_ACTIVE_MAPS);
	BIND_ENUM_CONSTANT(INFO_REGION_COUNT);
	BIND_ENUM_CONSTANT(INFO_AGENT_COUNT);
	BIND_ENUM_CONSTANT(INFO_LINK_COUNT);
	BIND_ENUM_CONSTANT(INFO_POLYGON_COUNT);
	BIND_ENUM_CONSTANT(INFO_EDGE_COUNT);
	BIND_ENUM_CONSTANT(INFO_EDGE_MERGE_COUNT);
	BIND_ENUM_CONSTANT(INFO_EDGE_CONNECTION_COUNT);
	BIND_ENUM_CONSTANT(INFO_EDGE_FREE_COUNT);
	BIND_ENUM_CONSTANT(INFO_OBSTACLE_COUNT);
}

NavigationSystem3D *NavigationSystem3D::get_singleton() {
	return singleton;
}

NavigationSystem3D::NavigationSystem3D() {
	ERR_FAIL_COND(singleton != nullptr);
	singleton = this;

	GLOBAL_DEF_BASIC(PropertyInfo(Variant::FLOAT, "navigation/spatial/default_cell_size", PROPERTY_HINT_RANGE, NavigationDefaults3D::NAV_MESH_CELL_SIZE_HINT), NavigationDefaults3D::NAV_MESH_CELL_SIZE);
	GLOBAL_DEF_BASIC(PropertyInfo(Variant::FLOAT, "navigation/spatial/default_cell_height", PROPERTY_HINT_RANGE, "0.001,100,0.001,or_greater"), NavigationDefaults3D::NAV_MESH_CELL_HEIGHT);
	GLOBAL_DEF("navigation/spatial/default_up", Vector3(0, 1, 0));
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "navigation/spatial/merge_rasterizer_cell_scale", PROPERTY_HINT_RANGE, "0.001,1,0.001,or_greater"), 1.0);
	GLOBAL_DEF("navigation/spatial/use_edge_connections", true);
	GLOBAL_DEF_BASIC(PropertyInfo(Variant::FLOAT, "navigation/spatial/default_edge_connection_margin", PROPERTY_HINT_RANGE, "0.01,10,0.001,or_greater"), NavigationDefaults3D::EDGE_CONNECTION_MARGIN);
	GLOBAL_DEF_BASIC(PropertyInfo(Variant::FLOAT, "navigation/spatial/default_link_connection_radius", PROPERTY_HINT_RANGE, "0.01,10,0.001,or_greater"), NavigationDefaults3D::LINK_CONNECTION_RADIUS);

#ifdef DEBUG_ENABLED

	debug_navigation_edge_connection_color = GLOBAL_DEF("debug/shapes/navigation/spatial/edge_connection_color", Color(1.0, 0.0, 1.0, 1.0));
	debug_navigation_geometry_edge_color = GLOBAL_DEF("debug/shapes/navigation/spatial/geometry_edge_color", Color(0.5, 1.0, 1.0, 1.0));
	debug_navigation_geometry_face_color = GLOBAL_DEF("debug/shapes/navigation/spatial/geometry_face_color", Color(0.5, 1.0, 1.0, 0.4));
	debug_navigation_geometry_edge_disabled_color = GLOBAL_DEF("debug/shapes/navigation/spatial/geometry_edge_disabled_color", Color(0.5, 0.5, 0.5, 1.0));
	debug_navigation_geometry_face_disabled_color = GLOBAL_DEF("debug/shapes/navigation/spatial/geometry_face_disabled_color", Color(0.5, 0.5, 0.5, 0.4));
	debug_navigation_link_connection_color = GLOBAL_DEF("debug/shapes/navigation/spatial/link_connection_color", Color(1.0, 0.5, 1.0, 1.0));
	debug_navigation_link_connection_disabled_color = GLOBAL_DEF("debug/shapes/navigation/spatial/link_connection_disabled_color", Color(0.5, 0.5, 0.5, 1.0));
	debug_navigation_agent_path_color = GLOBAL_DEF("debug/shapes/navigation/spatial/agent_path_color", Color(1.0, 0.0, 0.0, 1.0));

	debug_navigation_enable_edge_connections = GLOBAL_DEF("debug/shapes/navigation/spatial/enable_edge_connections", true);
	debug_navigation_enable_edge_connections_xray = GLOBAL_DEF("debug/shapes/navigation/spatial/enable_edge_connections_xray", true);
	debug_navigation_enable_edge_lines = GLOBAL_DEF("debug/shapes/navigation/spatial/enable_edge_lines", true);
	debug_navigation_enable_edge_lines_xray = GLOBAL_DEF("debug/shapes/navigation/spatial/enable_edge_lines_xray", true);
	debug_navigation_enable_geometry_face_random_color = GLOBAL_DEF("debug/shapes/navigation/spatial/enable_geometry_face_random_color", true);
	debug_navigation_enable_link_connections = GLOBAL_DEF("debug/shapes/navigation/spatial/enable_link_connections", true);
	debug_navigation_enable_link_connections_xray = GLOBAL_DEF("debug/shapes/navigation/spatial/enable_link_connections_xray", true);

	debug_navigation_enable_agent_paths = GLOBAL_DEF("debug/shapes/navigation/spatial/enable_agent_paths", true);
	debug_navigation_enable_agent_paths_xray = GLOBAL_DEF("debug/shapes/navigation/spatial/enable_agent_paths_xray", true);
	debug_navigation_agent_path_point_size = GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "debug/shapes/navigation/spatial/agent_path_point_size", PROPERTY_HINT_RANGE, "0.01,10,0.001,or_greater"), 4.0);

	debug_navigation_avoidance_agents_radius_color = GLOBAL_DEF("debug/shapes/avoidance/spatial/agents_radius_color", Color(1.0, 1.0, 0.0, 0.25));
	debug_navigation_avoidance_obstacles_radius_color = GLOBAL_DEF("debug/shapes/avoidance/spatial/obstacles_radius_color", Color(1.0, 0.5, 0.0, 0.25));
	debug_navigation_avoidance_static_obstacle_pushin_face_color = GLOBAL_DEF("debug/shapes/avoidance/spatial/obstacles_static_face_pushin_color", Color(1.0, 0.0, 0.0, 0.0));
	debug_navigation_avoidance_static_obstacle_pushin_edge_color = GLOBAL_DEF("debug/shapes/avoidance/spatial/obstacles_static_edge_pushin_color", Color(1.0, 0.0, 0.0, 1.0));
	debug_navigation_avoidance_static_obstacle_pushout_face_color = GLOBAL_DEF("debug/shapes/avoidance/spatial/obstacles_static_face_pushout_color", Color(1.0, 1.0, 0.0, 0.5));
	debug_navigation_avoidance_static_obstacle_pushout_edge_color = GLOBAL_DEF("debug/shapes/avoidance/spatial/obstacles_static_edge_pushout_color", Color(1.0, 1.0, 0.0, 1.0));
	debug_navigation_avoidance_enable_agents_radius = GLOBAL_DEF("debug/shapes/avoidance/spatial/enable_agents_radius", true);
	debug_navigation_avoidance_enable_obstacles_radius = GLOBAL_DEF("debug/shapes/avoidance/spatial/enable_obstacles_radius", true);
	debug_navigation_avoidance_enable_obstacles_static = GLOBAL_DEF("debug/shapes/avoidance/spatial/enable_obstacles_static", true);

	if (Engine::get_singleton()->is_editor_hint()) {
		// enable NavigationSystem3D when in Editor or else navigation mesh edge connections are invisible
		// on runtime tests Stage has "Visible Navigation" set and main iteration takes care of this
		set_debug_enabled(true);
		set_debug_navigation_enabled(true);
		set_debug_avoidance_enabled(true);
	}
#endif // DEBUG_ENABLED
}

NavigationSystem3D::~NavigationSystem3D() {
	singleton = nullptr;

	RWLockWrite write_lock(geometry_parser_rwlock);
	for (NavMeshGeometryParser3D *parser : generator_parsers) {
		geometry_parser_owner.free(parser->self);
	}
	generator_parsers.clear();
}

RID NavigationSystem3D::source_geometry_parser_create() {
	RWLockWrite write_lock(geometry_parser_rwlock);

	RID rid = geometry_parser_owner.make_rid();

	NavMeshGeometryParser3D *parser = geometry_parser_owner.get_or_null(rid);
	parser->self = rid;

	generator_parsers.push_back(parser);

	return rid;
}

void NavigationSystem3D::free_rid(RID p_rid) {
	if (!geometry_parser_owner.owns(p_rid)) {
		return;
	}
	RWLockWrite write_lock(geometry_parser_rwlock);

	NavMeshGeometryParser3D *parser = geometry_parser_owner.get_or_null(p_rid);
	ERR_FAIL_NULL(parser);

	generator_parsers.erase(parser);
	geometry_parser_owner.free(parser->self);
}

void NavigationSystem3D::source_geometry_parser_set_callback(RID p_parser, const Callable &p_callback) {
	RWLockWrite write_lock(geometry_parser_rwlock);

	NavMeshGeometryParser3D *parser = geometry_parser_owner.get_or_null(p_parser);
	ERR_FAIL_NULL(parser);

	parser->callback = p_callback;
}

void NavigationSystem3D::set_debug_enabled(bool p_enabled) {
#ifdef DEBUG_ENABLED
	if (debug_enabled != p_enabled) {
		debug_dirty = true;
	}

	debug_enabled = p_enabled;

	if (debug_dirty) {
		navigation_debug_dirty = true;
		callable_mp(this, &NavigationSystem3D::_emit_navigation_debug_changed_signal).call_deferred();

		avoidance_debug_dirty = true;
		callable_mp(this, &NavigationSystem3D::_emit_avoidance_debug_changed_signal).call_deferred();
	}
#endif // DEBUG_ENABLED
}

bool NavigationSystem3D::get_debug_enabled() const {
	return debug_enabled;
}

#ifdef DEBUG_ENABLED
void NavigationSystem3D::_emit_navigation_debug_changed_signal() {
	if (navigation_debug_dirty) {
		navigation_debug_dirty = false;
		emit_signal(SNAME("navigation_debug_changed"));
	}
}

void NavigationSystem3D::_emit_avoidance_debug_changed_signal() {
	if (avoidance_debug_dirty) {
		avoidance_debug_dirty = false;
		emit_signal(SNAME("avoidance_debug_changed"));
	}
}
#endif // DEBUG_ENABLED

#ifdef DEBUG_ENABLED
Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_geometry_face_material() {
	if (debug_navigation_geometry_face_material.is_valid()) {
		return debug_navigation_geometry_face_material;
	}

	bool enabled_geometry_face_random_color = get_debug_navigation_enable_geometry_face_random_color();

	Ref<StandardMaterial3D> face_material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	face_material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	face_material->set_transparency(StandardMaterial3D::TRANSPARENCY_ALPHA);
	face_material->set_albedo(get_debug_navigation_geometry_face_color());
	face_material->set_cull_mode(StandardMaterial3D::CULL_DISABLED);
	face_material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	if (enabled_geometry_face_random_color) {
		face_material->set_flag(StandardMaterial3D::FLAG_SRGB_VERTEX_COLOR, true);
		face_material->set_flag(StandardMaterial3D::FLAG_ALBEDO_FROM_VERTEX_COLOR, true);
	}

	debug_navigation_geometry_face_material = face_material;

	return debug_navigation_geometry_face_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_geometry_edge_material() {
	if (debug_navigation_geometry_edge_material.is_valid()) {
		return debug_navigation_geometry_edge_material;
	}

	bool enabled_edge_lines_xray = get_debug_navigation_enable_edge_lines_xray();

	Ref<StandardMaterial3D> line_material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	line_material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	line_material->set_albedo(get_debug_navigation_geometry_edge_color());
	line_material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	if (enabled_edge_lines_xray) {
		line_material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, true);
	}

	debug_navigation_geometry_edge_material = line_material;

	return debug_navigation_geometry_edge_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_geometry_face_disabled_material() {
	if (debug_navigation_geometry_face_disabled_material.is_valid()) {
		return debug_navigation_geometry_face_disabled_material;
	}

	Ref<StandardMaterial3D> face_disabled_material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	face_disabled_material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	face_disabled_material->set_transparency(StandardMaterial3D::TRANSPARENCY_ALPHA);
	face_disabled_material->set_albedo(get_debug_navigation_geometry_face_disabled_color());
	face_disabled_material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);

	debug_navigation_geometry_face_disabled_material = face_disabled_material;

	return debug_navigation_geometry_face_disabled_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_geometry_edge_disabled_material() {
	if (debug_navigation_geometry_edge_disabled_material.is_valid()) {
		return debug_navigation_geometry_edge_disabled_material;
	}

	bool enabled_edge_lines_xray = get_debug_navigation_enable_edge_lines_xray();

	Ref<StandardMaterial3D> line_disabled_material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	line_disabled_material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	line_disabled_material->set_albedo(get_debug_navigation_geometry_edge_disabled_color());
	line_disabled_material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	if (enabled_edge_lines_xray) {
		line_disabled_material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, true);
	}

	debug_navigation_geometry_edge_disabled_material = line_disabled_material;

	return debug_navigation_geometry_edge_disabled_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_edge_connections_material() {
	if (debug_navigation_edge_connections_material.is_valid()) {
		return debug_navigation_edge_connections_material;
	}

	bool enabled_edge_connections_xray = get_debug_navigation_enable_edge_connections_xray();

	Ref<StandardMaterial3D> edge_connections_material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	edge_connections_material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	edge_connections_material->set_albedo(get_debug_navigation_edge_connection_color());
	edge_connections_material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	if (enabled_edge_connections_xray) {
		edge_connections_material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, true);
	}
	edge_connections_material->set_render_priority(StandardMaterial3D::RENDER_PRIORITY_MAX - 2);

	debug_navigation_edge_connections_material = edge_connections_material;

	return debug_navigation_edge_connections_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_link_connections_material() {
	if (debug_navigation_link_connections_material.is_valid()) {
		return debug_navigation_link_connections_material;
	}

	Ref<StandardMaterial3D> material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	material->set_albedo(debug_navigation_link_connection_color);
	material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	if (debug_navigation_enable_link_connections_xray) {
		material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, true);
	}
	material->set_render_priority(StandardMaterial3D::RENDER_PRIORITY_MAX - 2);

	debug_navigation_link_connections_material = material;
	return debug_navigation_link_connections_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_link_connections_disabled_material() {
	if (debug_navigation_link_connections_disabled_material.is_valid()) {
		return debug_navigation_link_connections_disabled_material;
	}

	Ref<StandardMaterial3D> material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	material->set_albedo(debug_navigation_link_connection_disabled_color);
	material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	if (debug_navigation_enable_link_connections_xray) {
		material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, true);
	}
	material->set_render_priority(StandardMaterial3D::RENDER_PRIORITY_MAX - 2);

	debug_navigation_link_connections_disabled_material = material;
	return debug_navigation_link_connections_disabled_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_agent_path_line_material() {
	if (debug_navigation_agent_path_line_material.is_valid()) {
		return debug_navigation_agent_path_line_material;
	}

	Ref<StandardMaterial3D> material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);

	material->set_albedo(debug_navigation_agent_path_color);
	material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	if (debug_navigation_enable_agent_paths_xray) {
		material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, true);
	}
	material->set_render_priority(StandardMaterial3D::RENDER_PRIORITY_MAX - 2);

	debug_navigation_agent_path_line_material = material;
	return debug_navigation_agent_path_line_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_agent_path_point_material() {
	if (debug_navigation_agent_path_point_material.is_valid()) {
		return debug_navigation_agent_path_point_material;
	}

	Ref<StandardMaterial3D> material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	material->set_albedo(debug_navigation_agent_path_color);
	material->set_flag(StandardMaterial3D::FLAG_USE_POINT_SIZE, true);
	material->set_point_size(debug_navigation_agent_path_point_size);
	material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	if (debug_navigation_enable_agent_paths_xray) {
		material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, true);
	}
	material->set_render_priority(StandardMaterial3D::RENDER_PRIORITY_MAX - 2);

	debug_navigation_agent_path_point_material = material;
	return debug_navigation_agent_path_point_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_avoidance_agents_radius_material() {
	if (debug_navigation_avoidance_agents_radius_material.is_valid()) {
		return debug_navigation_avoidance_agents_radius_material;
	}

	Ref<StandardMaterial3D> material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	material->set_transparency(StandardMaterial3D::TRANSPARENCY_ALPHA);
	material->set_cull_mode(StandardMaterial3D::CULL_DISABLED);
	material->set_albedo(debug_navigation_avoidance_agents_radius_color);
	material->set_render_priority(StandardMaterial3D::RENDER_PRIORITY_MIN + 2);

	debug_navigation_avoidance_agents_radius_material = material;
	return debug_navigation_avoidance_agents_radius_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_avoidance_obstacles_radius_material() {
	if (debug_navigation_avoidance_obstacles_radius_material.is_valid()) {
		return debug_navigation_avoidance_obstacles_radius_material;
	}

	Ref<StandardMaterial3D> material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	material->set_transparency(StandardMaterial3D::TRANSPARENCY_ALPHA);
	material->set_cull_mode(StandardMaterial3D::CULL_DISABLED);
	material->set_albedo(debug_navigation_avoidance_obstacles_radius_color);
	material->set_render_priority(StandardMaterial3D::RENDER_PRIORITY_MIN + 2);

	debug_navigation_avoidance_obstacles_radius_material = material;
	return debug_navigation_avoidance_obstacles_radius_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_avoidance_static_obstacle_pushin_face_material() {
	if (debug_navigation_avoidance_static_obstacle_pushin_face_material.is_valid()) {
		return debug_navigation_avoidance_static_obstacle_pushin_face_material;
	}

	Ref<StandardMaterial3D> material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	material->set_transparency(StandardMaterial3D::TRANSPARENCY_ALPHA);
	material->set_cull_mode(StandardMaterial3D::CULL_DISABLED);
	material->set_albedo(debug_navigation_avoidance_static_obstacle_pushin_face_color);
	material->set_render_priority(StandardMaterial3D::RENDER_PRIORITY_MIN + 2);

	debug_navigation_avoidance_static_obstacle_pushin_face_material = material;
	return debug_navigation_avoidance_static_obstacle_pushin_face_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_avoidance_static_obstacle_pushout_face_material() {
	if (debug_navigation_avoidance_static_obstacle_pushout_face_material.is_valid()) {
		return debug_navigation_avoidance_static_obstacle_pushout_face_material;
	}

	Ref<StandardMaterial3D> material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	material->set_transparency(StandardMaterial3D::TRANSPARENCY_ALPHA);
	material->set_cull_mode(StandardMaterial3D::CULL_DISABLED);
	material->set_albedo(debug_navigation_avoidance_static_obstacle_pushout_face_color);
	material->set_render_priority(StandardMaterial3D::RENDER_PRIORITY_MIN + 2);

	debug_navigation_avoidance_static_obstacle_pushout_face_material = material;
	return debug_navigation_avoidance_static_obstacle_pushout_face_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_avoidance_static_obstacle_pushin_edge_material() {
	if (debug_navigation_avoidance_static_obstacle_pushin_edge_material.is_valid()) {
		return debug_navigation_avoidance_static_obstacle_pushin_edge_material;
	}

	Ref<StandardMaterial3D> material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	//material->set_transparency(StandardMaterial3D::TRANSPARENCY_ALPHA);
	//material->set_cull_mode(StandardMaterial3D::CULL_DISABLED);
	material->set_albedo(debug_navigation_avoidance_static_obstacle_pushin_edge_color);
	//material->set_render_priority(StandardMaterial3D::RENDER_PRIORITY_MIN + 2);
	material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, true);

	debug_navigation_avoidance_static_obstacle_pushin_edge_material = material;
	return debug_navigation_avoidance_static_obstacle_pushin_edge_material;
}

Ref<StandardMaterial3D> NavigationSystem3D::get_debug_navigation_avoidance_static_obstacle_pushout_edge_material() {
	if (debug_navigation_avoidance_static_obstacle_pushout_edge_material.is_valid()) {
		return debug_navigation_avoidance_static_obstacle_pushout_edge_material;
	}

	Ref<StandardMaterial3D> material = Ref<StandardMaterial3D>(memnew(StandardMaterial3D));
	material->set_shading_mode(StandardMaterial3D::SHADING_MODE_UNSHADED);
	material->set_flag(StandardMaterial3D::FLAG_DISABLE_FOG, true);
	///material->set_transparency(StandardMaterial3D::TRANSPARENCY_ALPHA);
	//material->set_cull_mode(StandardMaterial3D::CULL_DISABLED);
	material->set_albedo(debug_navigation_avoidance_static_obstacle_pushout_edge_color);
	//material->set_render_priority(StandardMaterial3D::RENDER_PRIORITY_MIN + 2);
	material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, true);

	debug_navigation_avoidance_static_obstacle_pushout_edge_material = material;
	return debug_navigation_avoidance_static_obstacle_pushout_edge_material;
}

void NavigationSystem3D::set_debug_navigation_edge_connection_color(const Color &p_color) {
	debug_navigation_edge_connection_color = p_color;
	if (debug_navigation_edge_connections_material.is_valid()) {
		debug_navigation_edge_connections_material->set_albedo(debug_navigation_edge_connection_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_edge_connection_color() const {
	return debug_navigation_edge_connection_color;
}

void NavigationSystem3D::set_debug_navigation_geometry_edge_color(const Color &p_color) {
	debug_navigation_geometry_edge_color = p_color;
	if (debug_navigation_geometry_edge_material.is_valid()) {
		debug_navigation_geometry_edge_material->set_albedo(debug_navigation_geometry_edge_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_geometry_edge_color() const {
	return debug_navigation_geometry_edge_color;
}

void NavigationSystem3D::set_debug_navigation_geometry_face_color(const Color &p_color) {
	debug_navigation_geometry_face_color = p_color;
	if (debug_navigation_geometry_face_material.is_valid()) {
		debug_navigation_geometry_face_material->set_albedo(debug_navigation_geometry_face_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_geometry_face_color() const {
	return debug_navigation_geometry_face_color;
}

void NavigationSystem3D::set_debug_navigation_geometry_edge_disabled_color(const Color &p_color) {
	debug_navigation_geometry_edge_disabled_color = p_color;
	if (debug_navigation_geometry_edge_disabled_material.is_valid()) {
		debug_navigation_geometry_edge_disabled_material->set_albedo(debug_navigation_geometry_edge_disabled_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_geometry_edge_disabled_color() const {
	return debug_navigation_geometry_edge_disabled_color;
}

void NavigationSystem3D::set_debug_navigation_geometry_face_disabled_color(const Color &p_color) {
	debug_navigation_geometry_face_disabled_color = p_color;
	if (debug_navigation_geometry_face_disabled_material.is_valid()) {
		debug_navigation_geometry_face_disabled_material->set_albedo(debug_navigation_geometry_face_disabled_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_geometry_face_disabled_color() const {
	return debug_navigation_geometry_face_disabled_color;
}

void NavigationSystem3D::set_debug_navigation_link_connection_color(const Color &p_color) {
	debug_navigation_link_connection_color = p_color;
	if (debug_navigation_link_connections_material.is_valid()) {
		debug_navigation_link_connections_material->set_albedo(debug_navigation_link_connection_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_link_connection_color() const {
	return debug_navigation_link_connection_color;
}

void NavigationSystem3D::set_debug_navigation_link_connection_disabled_color(const Color &p_color) {
	debug_navigation_link_connection_disabled_color = p_color;
	if (debug_navigation_link_connections_disabled_material.is_valid()) {
		debug_navigation_link_connections_disabled_material->set_albedo(debug_navigation_link_connection_disabled_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_link_connection_disabled_color() const {
	return debug_navigation_link_connection_disabled_color;
}

void NavigationSystem3D::set_debug_navigation_agent_path_point_size(real_t p_point_size) {
	debug_navigation_agent_path_point_size = MAX(0.1, p_point_size);
	if (debug_navigation_agent_path_point_material.is_valid()) {
		debug_navigation_agent_path_point_material->set_point_size(debug_navigation_agent_path_point_size);
	}
}

real_t NavigationSystem3D::get_debug_navigation_agent_path_point_size() const {
	return debug_navigation_agent_path_point_size;
}

void NavigationSystem3D::set_debug_navigation_agent_path_color(const Color &p_color) {
	debug_navigation_agent_path_color = p_color;
	if (debug_navigation_agent_path_line_material.is_valid()) {
		debug_navigation_agent_path_line_material->set_albedo(debug_navigation_agent_path_color);
	}
	if (debug_navigation_agent_path_point_material.is_valid()) {
		debug_navigation_agent_path_point_material->set_albedo(debug_navigation_agent_path_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_agent_path_color() const {
	return debug_navigation_agent_path_color;
}

void NavigationSystem3D::set_debug_navigation_enable_edge_connections(const bool p_value) {
	debug_navigation_enable_edge_connections = p_value;
	navigation_debug_dirty = true;
	callable_mp(this, &NavigationSystem3D::_emit_navigation_debug_changed_signal).call_deferred();
}

bool NavigationSystem3D::get_debug_navigation_enable_edge_connections() const {
	return debug_navigation_enable_edge_connections;
}

void NavigationSystem3D::set_debug_navigation_enable_edge_connections_xray(const bool p_value) {
	debug_navigation_enable_edge_connections_xray = p_value;
	if (debug_navigation_edge_connections_material.is_valid()) {
		debug_navigation_edge_connections_material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, debug_navigation_enable_edge_connections_xray);
	}
}

bool NavigationSystem3D::get_debug_navigation_enable_edge_connections_xray() const {
	return debug_navigation_enable_edge_connections_xray;
}

void NavigationSystem3D::set_debug_navigation_enable_edge_lines(const bool p_value) {
	debug_navigation_enable_edge_lines = p_value;
	navigation_debug_dirty = true;
	callable_mp(this, &NavigationSystem3D::_emit_navigation_debug_changed_signal).call_deferred();
}

bool NavigationSystem3D::get_debug_navigation_enable_edge_lines() const {
	return debug_navigation_enable_edge_lines;
}

void NavigationSystem3D::set_debug_navigation_enable_edge_lines_xray(const bool p_value) {
	debug_navigation_enable_edge_lines_xray = p_value;
	if (debug_navigation_geometry_edge_material.is_valid()) {
		debug_navigation_geometry_edge_material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, debug_navigation_enable_edge_lines_xray);
	}
}

bool NavigationSystem3D::get_debug_navigation_enable_edge_lines_xray() const {
	return debug_navigation_enable_edge_lines_xray;
}

void NavigationSystem3D::set_debug_navigation_enable_geometry_face_random_color(const bool p_value) {
	debug_navigation_enable_geometry_face_random_color = p_value;
	navigation_debug_dirty = true;
	callable_mp(this, &NavigationSystem3D::_emit_navigation_debug_changed_signal).call_deferred();
}

bool NavigationSystem3D::get_debug_navigation_enable_geometry_face_random_color() const {
	return debug_navigation_enable_geometry_face_random_color;
}

void NavigationSystem3D::set_debug_navigation_enable_link_connections(const bool p_value) {
	debug_navigation_enable_link_connections = p_value;
	navigation_debug_dirty = true;
	callable_mp(this, &NavigationSystem3D::_emit_navigation_debug_changed_signal).call_deferred();
}

bool NavigationSystem3D::get_debug_navigation_enable_link_connections() const {
	return debug_navigation_enable_link_connections;
}

void NavigationSystem3D::set_debug_navigation_enable_link_connections_xray(const bool p_value) {
	debug_navigation_enable_link_connections_xray = p_value;
	if (debug_navigation_link_connections_material.is_valid()) {
		debug_navigation_link_connections_material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, debug_navigation_enable_link_connections_xray);
	}
}

bool NavigationSystem3D::get_debug_navigation_enable_link_connections_xray() const {
	return debug_navigation_enable_link_connections_xray;
}

void NavigationSystem3D::set_debug_navigation_avoidance_enable_agents_radius(const bool p_value) {
	debug_navigation_avoidance_enable_agents_radius = p_value;
	avoidance_debug_dirty = true;
	callable_mp(this, &NavigationSystem3D::_emit_avoidance_debug_changed_signal).call_deferred();
}

bool NavigationSystem3D::get_debug_navigation_avoidance_enable_agents_radius() const {
	return debug_navigation_avoidance_enable_agents_radius;
}

void NavigationSystem3D::set_debug_navigation_avoidance_enable_obstacles_radius(const bool p_value) {
	debug_navigation_avoidance_enable_obstacles_radius = p_value;
	avoidance_debug_dirty = true;
	callable_mp(this, &NavigationSystem3D::_emit_avoidance_debug_changed_signal).call_deferred();
}

bool NavigationSystem3D::get_debug_navigation_avoidance_enable_obstacles_radius() const {
	return debug_navigation_avoidance_enable_obstacles_radius;
}

void NavigationSystem3D::set_debug_navigation_avoidance_enable_obstacles_static(const bool p_value) {
	debug_navigation_avoidance_enable_obstacles_static = p_value;
	avoidance_debug_dirty = true;
	callable_mp(this, &NavigationSystem3D::_emit_avoidance_debug_changed_signal).call_deferred();
}

bool NavigationSystem3D::get_debug_navigation_avoidance_enable_obstacles_static() const {
	return debug_navigation_avoidance_enable_obstacles_static;
}

void NavigationSystem3D::set_debug_navigation_avoidance_agents_radius_color(const Color &p_color) {
	debug_navigation_avoidance_agents_radius_color = p_color;
	if (debug_navigation_avoidance_agents_radius_material.is_valid()) {
		debug_navigation_avoidance_agents_radius_material->set_albedo(debug_navigation_avoidance_agents_radius_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_avoidance_agents_radius_color() const {
	return debug_navigation_avoidance_agents_radius_color;
}

void NavigationSystem3D::set_debug_navigation_avoidance_obstacles_radius_color(const Color &p_color) {
	debug_navigation_avoidance_obstacles_radius_color = p_color;
	if (debug_navigation_avoidance_obstacles_radius_material.is_valid()) {
		debug_navigation_avoidance_obstacles_radius_material->set_albedo(debug_navigation_avoidance_obstacles_radius_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_avoidance_obstacles_radius_color() const {
	return debug_navigation_avoidance_obstacles_radius_color;
}

void NavigationSystem3D::set_debug_navigation_avoidance_static_obstacle_pushin_face_color(const Color &p_color) {
	debug_navigation_avoidance_static_obstacle_pushin_face_color = p_color;
	if (debug_navigation_avoidance_static_obstacle_pushin_face_material.is_valid()) {
		debug_navigation_avoidance_static_obstacle_pushin_face_material->set_albedo(debug_navigation_avoidance_static_obstacle_pushin_face_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_avoidance_static_obstacle_pushin_face_color() const {
	return debug_navigation_avoidance_static_obstacle_pushin_face_color;
}

void NavigationSystem3D::set_debug_navigation_avoidance_static_obstacle_pushout_face_color(const Color &p_color) {
	debug_navigation_avoidance_static_obstacle_pushout_face_color = p_color;
	if (debug_navigation_avoidance_static_obstacle_pushout_face_material.is_valid()) {
		debug_navigation_avoidance_static_obstacle_pushout_face_material->set_albedo(debug_navigation_avoidance_static_obstacle_pushout_face_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_avoidance_static_obstacle_pushout_face_color() const {
	return debug_navigation_avoidance_static_obstacle_pushout_face_color;
}

void NavigationSystem3D::set_debug_navigation_avoidance_static_obstacle_pushin_edge_color(const Color &p_color) {
	debug_navigation_avoidance_static_obstacle_pushin_edge_color = p_color;
	if (debug_navigation_avoidance_static_obstacle_pushin_edge_material.is_valid()) {
		debug_navigation_avoidance_static_obstacle_pushin_edge_material->set_albedo(debug_navigation_avoidance_static_obstacle_pushin_edge_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_avoidance_static_obstacle_pushin_edge_color() const {
	return debug_navigation_avoidance_static_obstacle_pushin_edge_color;
}

void NavigationSystem3D::set_debug_navigation_avoidance_static_obstacle_pushout_edge_color(const Color &p_color) {
	debug_navigation_avoidance_static_obstacle_pushout_edge_color = p_color;
	if (debug_navigation_avoidance_static_obstacle_pushout_edge_material.is_valid()) {
		debug_navigation_avoidance_static_obstacle_pushout_edge_material->set_albedo(debug_navigation_avoidance_static_obstacle_pushout_edge_color);
	}
}

Color NavigationSystem3D::get_debug_navigation_avoidance_static_obstacle_pushout_edge_color() const {
	return debug_navigation_avoidance_static_obstacle_pushout_edge_color;
}

void NavigationSystem3D::set_debug_navigation_enable_agent_paths(const bool p_value) {
	if (debug_navigation_enable_agent_paths != p_value) {
		debug_dirty = true;
	}

	debug_navigation_enable_agent_paths = p_value;

	if (debug_dirty) {
		callable_mp(this, &NavigationSystem3D::_emit_navigation_debug_changed_signal).call_deferred();
	}
}

bool NavigationSystem3D::get_debug_navigation_enable_agent_paths() const {
	return debug_navigation_enable_agent_paths;
}

void NavigationSystem3D::set_debug_navigation_enable_agent_paths_xray(const bool p_value) {
	debug_navigation_enable_agent_paths_xray = p_value;
	if (debug_navigation_agent_path_line_material.is_valid()) {
		debug_navigation_agent_path_line_material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, debug_navigation_enable_agent_paths_xray);
	}
	if (debug_navigation_agent_path_point_material.is_valid()) {
		debug_navigation_agent_path_point_material->set_flag(StandardMaterial3D::FLAG_DISABLE_DEPTH_TEST, debug_navigation_enable_agent_paths_xray);
	}
}

bool NavigationSystem3D::get_debug_navigation_enable_agent_paths_xray() const {
	return debug_navigation_enable_agent_paths_xray;
}

void NavigationSystem3D::set_debug_navigation_enabled(bool p_enabled) {
	debug_navigation_enabled = p_enabled;
	navigation_debug_dirty = true;
	callable_mp(this, &NavigationSystem3D::_emit_navigation_debug_changed_signal).call_deferred();
}

bool NavigationSystem3D::get_debug_navigation_enabled() const {
	return debug_navigation_enabled;
}

void NavigationSystem3D::set_debug_avoidance_enabled(bool p_enabled) {
	debug_avoidance_enabled = p_enabled;
	avoidance_debug_dirty = true;
	callable_mp(this, &NavigationSystem3D::_emit_avoidance_debug_changed_signal).call_deferred();
}

bool NavigationSystem3D::get_debug_avoidance_enabled() const {
	return debug_avoidance_enabled;
}

#endif // DEBUG_ENABLED
