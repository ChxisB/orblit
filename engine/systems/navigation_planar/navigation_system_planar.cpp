/**************************************************************************/
/*  navigation_system_planar.cpp                                              */

#include "navigation_system_planar.h"

#include "core/config/engine.h"
#include "core/config/project_settings.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "scene/resources/planar/navigation_mesh_source_geometry_data_planar.h" // IWYU pragma: keep. Bind `NavigationMeshSourceGeometryData2D`.
#include "scene/resources/planar/navigation_polygon.h" // IWYU pragma: keep. Bind `NavigationPolygon`.
#include "scene/stage/entity.h" // IWYU pragma: keep. Needed to bind `Entity *` arg.
#include "systems/navigation_planar/navigation_path_query_parameters_planar.h" // IWYU pragma: keep. Bind `NavigationPathQueryParameters2D`.
#include "systems/navigation_planar/navigation_path_query_result_planar.h" // IWYU pragma: keep. Bind `NavigationPathQueryResult2D`.

NavigationSystem2D *NavigationSystem2D::singleton = nullptr;

RWLock NavigationSystem2D::geometry_parser_rwlock;
RID_Owner<NavMeshGeometryParser2D> NavigationSystem2D::geometry_parser_owner;
LocalVector<NavMeshGeometryParser2D *> NavigationSystem2D::generator_parsers;

void NavigationSystem2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_maps"), &NavigationSystem2D::get_maps);

	ClassRegistry::bind_method(D_METHOD("map_create"), &NavigationSystem2D::map_create);
	ClassRegistry::bind_method(D_METHOD("map_set_active", "map", "active"), &NavigationSystem2D::map_set_active);
	ClassRegistry::bind_method(D_METHOD("map_is_active", "map"), &NavigationSystem2D::map_is_active);
	ClassRegistry::bind_method(D_METHOD("map_set_cell_size", "map", "cell_size"), &NavigationSystem2D::map_set_cell_size);
	ClassRegistry::bind_method(D_METHOD("map_get_cell_size", "map"), &NavigationSystem2D::map_get_cell_size);
	ClassRegistry::bind_method(D_METHOD("map_set_merge_rasterizer_cell_scale", "map", "scale"), &NavigationSystem2D::map_set_merge_rasterizer_cell_scale);
	ClassRegistry::bind_method(D_METHOD("map_get_merge_rasterizer_cell_scale", "map"), &NavigationSystem2D::map_get_merge_rasterizer_cell_scale);
	ClassRegistry::bind_method(D_METHOD("map_set_use_edge_connections", "map", "enabled"), &NavigationSystem2D::map_set_use_edge_connections);
	ClassRegistry::bind_method(D_METHOD("map_get_use_edge_connections", "map"), &NavigationSystem2D::map_get_use_edge_connections);
	ClassRegistry::bind_method(D_METHOD("map_set_edge_connection_margin", "map", "margin"), &NavigationSystem2D::map_set_edge_connection_margin);
	ClassRegistry::bind_method(D_METHOD("map_get_edge_connection_margin", "map"), &NavigationSystem2D::map_get_edge_connection_margin);
	ClassRegistry::bind_method(D_METHOD("map_set_link_connection_radius", "map", "radius"), &NavigationSystem2D::map_set_link_connection_radius);
	ClassRegistry::bind_method(D_METHOD("map_get_link_connection_radius", "map"), &NavigationSystem2D::map_get_link_connection_radius);
	ClassRegistry::bind_method(D_METHOD("map_get_path", "map", "origin", "destination", "optimize", "navigation_layers"), &NavigationSystem2D::map_get_path, DEFVAL(1));
	ClassRegistry::bind_method(D_METHOD("map_get_closest_point", "map", "to_point"), &NavigationSystem2D::map_get_closest_point);
	ClassRegistry::bind_method(D_METHOD("map_get_closest_point_owner", "map", "to_point"), &NavigationSystem2D::map_get_closest_point_owner);

	ClassRegistry::bind_method(D_METHOD("map_get_links", "map"), &NavigationSystem2D::map_get_links);
	ClassRegistry::bind_method(D_METHOD("map_get_regions", "map"), &NavigationSystem2D::map_get_regions);
	ClassRegistry::bind_method(D_METHOD("map_get_agents", "map"), &NavigationSystem2D::map_get_agents);
	ClassRegistry::bind_method(D_METHOD("map_get_obstacles", "map"), &NavigationSystem2D::map_get_obstacles);

	ClassRegistry::bind_method(D_METHOD("map_get_iteration_id", "map"), &NavigationSystem2D::map_get_iteration_id);
	ClassRegistry::bind_method(D_METHOD("map_set_use_async_iterations", "map", "enabled"), &NavigationSystem2D::map_set_use_async_iterations);
	ClassRegistry::bind_method(D_METHOD("map_get_use_async_iterations", "map"), &NavigationSystem2D::map_get_use_async_iterations);

	ClassRegistry::bind_method(D_METHOD("map_get_random_point", "map", "navigation_layers", "uniformly"), &NavigationSystem2D::map_get_random_point);

	ClassRegistry::bind_method(D_METHOD("query_path", "parameters", "result", "callback"), &NavigationSystem2D::query_path, DEFVAL(Callable()));

	ClassRegistry::bind_method(D_METHOD("region_create"), &NavigationSystem2D::region_create);
	ClassRegistry::bind_method(D_METHOD("region_get_iteration_id", "region"), &NavigationSystem2D::region_get_iteration_id);
	ClassRegistry::bind_method(D_METHOD("region_set_use_async_iterations", "region", "enabled"), &NavigationSystem2D::region_set_use_async_iterations);
	ClassRegistry::bind_method(D_METHOD("region_get_use_async_iterations", "region"), &NavigationSystem2D::region_get_use_async_iterations);
	ClassRegistry::bind_method(D_METHOD("region_set_enabled", "region", "enabled"), &NavigationSystem2D::region_set_enabled);
	ClassRegistry::bind_method(D_METHOD("region_get_enabled", "region"), &NavigationSystem2D::region_get_enabled);
	ClassRegistry::bind_method(D_METHOD("region_set_use_edge_connections", "region", "enabled"), &NavigationSystem2D::region_set_use_edge_connections);
	ClassRegistry::bind_method(D_METHOD("region_get_use_edge_connections", "region"), &NavigationSystem2D::region_get_use_edge_connections);
	ClassRegistry::bind_method(D_METHOD("region_set_enter_cost", "region", "enter_cost"), &NavigationSystem2D::region_set_enter_cost);
	ClassRegistry::bind_method(D_METHOD("region_get_enter_cost", "region"), &NavigationSystem2D::region_get_enter_cost);
	ClassRegistry::bind_method(D_METHOD("region_set_travel_cost", "region", "travel_cost"), &NavigationSystem2D::region_set_travel_cost);
	ClassRegistry::bind_method(D_METHOD("region_get_travel_cost", "region"), &NavigationSystem2D::region_get_travel_cost);
	ClassRegistry::bind_method(D_METHOD("region_set_owner_id", "region", "owner_id"), &NavigationSystem2D::region_set_owner_id);
	ClassRegistry::bind_method(D_METHOD("region_get_owner_id", "region"), &NavigationSystem2D::region_get_owner_id);
	ClassRegistry::bind_method(D_METHOD("region_owns_point", "region", "point"), &NavigationSystem2D::region_owns_point);
	ClassRegistry::bind_method(D_METHOD("region_set_map", "region", "map"), &NavigationSystem2D::region_set_map);
	ClassRegistry::bind_method(D_METHOD("region_get_map", "region"), &NavigationSystem2D::region_get_map);
	ClassRegistry::bind_method(D_METHOD("region_set_navigation_layers", "region", "navigation_layers"), &NavigationSystem2D::region_set_navigation_layers);
	ClassRegistry::bind_method(D_METHOD("region_get_navigation_layers", "region"), &NavigationSystem2D::region_get_navigation_layers);
	ClassRegistry::bind_method(D_METHOD("region_set_transform", "region", "transform"), &NavigationSystem2D::region_set_transform);
	ClassRegistry::bind_method(D_METHOD("region_get_transform", "region"), &NavigationSystem2D::region_get_transform);
	ClassRegistry::bind_method(D_METHOD("region_set_navigation_polygon", "region", "navigation_polygon"), &NavigationSystem2D::region_set_navigation_polygon);
	ClassRegistry::bind_method(D_METHOD("region_get_connections_count", "region"), &NavigationSystem2D::region_get_connections_count);
	ClassRegistry::bind_method(D_METHOD("region_get_connection_pathway_start", "region", "connection"), &NavigationSystem2D::region_get_connection_pathway_start);
	ClassRegistry::bind_method(D_METHOD("region_get_connection_pathway_end", "region", "connection"), &NavigationSystem2D::region_get_connection_pathway_end);
	ClassRegistry::bind_method(D_METHOD("region_get_closest_point", "region", "to_point"), &NavigationSystem2D::region_get_closest_point);
	ClassRegistry::bind_method(D_METHOD("region_get_random_point", "region", "navigation_layers", "uniformly"), &NavigationSystem2D::region_get_random_point);
	ClassRegistry::bind_method(D_METHOD("region_get_bounds", "region"), &NavigationSystem2D::region_get_bounds);

	ClassRegistry::bind_method(D_METHOD("link_create"), &NavigationSystem2D::link_create);
	ClassRegistry::bind_method(D_METHOD("link_get_iteration_id", "link"), &NavigationSystem2D::link_get_iteration_id);
	ClassRegistry::bind_method(D_METHOD("link_set_map", "link", "map"), &NavigationSystem2D::link_set_map);
	ClassRegistry::bind_method(D_METHOD("link_get_map", "link"), &NavigationSystem2D::link_get_map);
	ClassRegistry::bind_method(D_METHOD("link_set_enabled", "link", "enabled"), &NavigationSystem2D::link_set_enabled);
	ClassRegistry::bind_method(D_METHOD("link_get_enabled", "link"), &NavigationSystem2D::link_get_enabled);
	ClassRegistry::bind_method(D_METHOD("link_set_bidirectional", "link", "bidirectional"), &NavigationSystem2D::link_set_bidirectional);
	ClassRegistry::bind_method(D_METHOD("link_is_bidirectional", "link"), &NavigationSystem2D::link_is_bidirectional);
	ClassRegistry::bind_method(D_METHOD("link_set_navigation_layers", "link", "navigation_layers"), &NavigationSystem2D::link_set_navigation_layers);
	ClassRegistry::bind_method(D_METHOD("link_get_navigation_layers", "link"), &NavigationSystem2D::link_get_navigation_layers);
	ClassRegistry::bind_method(D_METHOD("link_set_start_position", "link", "position"), &NavigationSystem2D::link_set_start_position);
	ClassRegistry::bind_method(D_METHOD("link_get_start_position", "link"), &NavigationSystem2D::link_get_start_position);
	ClassRegistry::bind_method(D_METHOD("link_set_end_position", "link", "position"), &NavigationSystem2D::link_set_end_position);
	ClassRegistry::bind_method(D_METHOD("link_get_end_position", "link"), &NavigationSystem2D::link_get_end_position);
	ClassRegistry::bind_method(D_METHOD("link_set_enter_cost", "link", "enter_cost"), &NavigationSystem2D::link_set_enter_cost);
	ClassRegistry::bind_method(D_METHOD("link_get_enter_cost", "link"), &NavigationSystem2D::link_get_enter_cost);
	ClassRegistry::bind_method(D_METHOD("link_set_travel_cost", "link", "travel_cost"), &NavigationSystem2D::link_set_travel_cost);
	ClassRegistry::bind_method(D_METHOD("link_get_travel_cost", "link"), &NavigationSystem2D::link_get_travel_cost);
	ClassRegistry::bind_method(D_METHOD("link_set_owner_id", "link", "owner_id"), &NavigationSystem2D::link_set_owner_id);
	ClassRegistry::bind_method(D_METHOD("link_get_owner_id", "link"), &NavigationSystem2D::link_get_owner_id);

	ClassRegistry::bind_method(D_METHOD("agent_create"), &NavigationSystem2D::agent_create);
	ClassRegistry::bind_method(D_METHOD("agent_set_avoidance_enabled", "agent", "enabled"), &NavigationSystem2D::agent_set_avoidance_enabled);
	ClassRegistry::bind_method(D_METHOD("agent_get_avoidance_enabled", "agent"), &NavigationSystem2D::agent_get_avoidance_enabled);
	ClassRegistry::bind_method(D_METHOD("agent_set_map", "agent", "map"), &NavigationSystem2D::agent_set_map);
	ClassRegistry::bind_method(D_METHOD("agent_get_map", "agent"), &NavigationSystem2D::agent_get_map);
	ClassRegistry::bind_method(D_METHOD("agent_set_paused", "agent", "paused"), &NavigationSystem2D::agent_set_paused);
	ClassRegistry::bind_method(D_METHOD("agent_get_paused", "agent"), &NavigationSystem2D::agent_get_paused);
	ClassRegistry::bind_method(D_METHOD("agent_set_neighbor_distance", "agent", "distance"), &NavigationSystem2D::agent_set_neighbor_distance);
	ClassRegistry::bind_method(D_METHOD("agent_get_neighbor_distance", "agent"), &NavigationSystem2D::agent_get_neighbor_distance);
	ClassRegistry::bind_method(D_METHOD("agent_set_max_neighbors", "agent", "count"), &NavigationSystem2D::agent_set_max_neighbors);
	ClassRegistry::bind_method(D_METHOD("agent_get_max_neighbors", "agent"), &NavigationSystem2D::agent_get_max_neighbors);
	ClassRegistry::bind_method(D_METHOD("agent_set_time_horizon_agents", "agent", "time_horizon"), &NavigationSystem2D::agent_set_time_horizon_agents);
	ClassRegistry::bind_method(D_METHOD("agent_get_time_horizon_agents", "agent"), &NavigationSystem2D::agent_get_time_horizon_agents);
	ClassRegistry::bind_method(D_METHOD("agent_set_time_horizon_obstacles", "agent", "time_horizon"), &NavigationSystem2D::agent_set_time_horizon_obstacles);
	ClassRegistry::bind_method(D_METHOD("agent_get_time_horizon_obstacles", "agent"), &NavigationSystem2D::agent_get_time_horizon_obstacles);
	ClassRegistry::bind_method(D_METHOD("agent_set_radius", "agent", "radius"), &NavigationSystem2D::agent_set_radius);
	ClassRegistry::bind_method(D_METHOD("agent_get_radius", "agent"), &NavigationSystem2D::agent_get_radius);
	ClassRegistry::bind_method(D_METHOD("agent_set_max_speed", "agent", "max_speed"), &NavigationSystem2D::agent_set_max_speed);
	ClassRegistry::bind_method(D_METHOD("agent_get_max_speed", "agent"), &NavigationSystem2D::agent_get_max_speed);
	ClassRegistry::bind_method(D_METHOD("agent_set_velocity_forced", "agent", "velocity"), &NavigationSystem2D::agent_set_velocity_forced);
	ClassRegistry::bind_method(D_METHOD("agent_set_velocity", "agent", "velocity"), &NavigationSystem2D::agent_set_velocity);
	ClassRegistry::bind_method(D_METHOD("agent_get_velocity", "agent"), &NavigationSystem2D::agent_get_velocity);
	ClassRegistry::bind_method(D_METHOD("agent_set_position", "agent", "position"), &NavigationSystem2D::agent_set_position);
	ClassRegistry::bind_method(D_METHOD("agent_get_position", "agent"), &NavigationSystem2D::agent_get_position);
	ClassRegistry::bind_method(D_METHOD("agent_is_map_changed", "agent"), &NavigationSystem2D::agent_is_map_changed);
	ClassRegistry::bind_method(D_METHOD("agent_set_avoidance_callback", "agent", "callback"), &NavigationSystem2D::agent_set_avoidance_callback);
	ClassRegistry::bind_method(D_METHOD("agent_has_avoidance_callback", "agent"), &NavigationSystem2D::agent_has_avoidance_callback);
	ClassRegistry::bind_method(D_METHOD("agent_set_avoidance_layers", "agent", "layers"), &NavigationSystem2D::agent_set_avoidance_layers);
	ClassRegistry::bind_method(D_METHOD("agent_get_avoidance_layers", "agent"), &NavigationSystem2D::agent_get_avoidance_layers);
	ClassRegistry::bind_method(D_METHOD("agent_set_avoidance_mask", "agent", "mask"), &NavigationSystem2D::agent_set_avoidance_mask);
	ClassRegistry::bind_method(D_METHOD("agent_get_avoidance_mask", "agent"), &NavigationSystem2D::agent_get_avoidance_mask);
	ClassRegistry::bind_method(D_METHOD("agent_set_avoidance_priority", "agent", "priority"), &NavigationSystem2D::agent_set_avoidance_priority);
	ClassRegistry::bind_method(D_METHOD("agent_get_avoidance_priority", "agent"), &NavigationSystem2D::agent_get_avoidance_priority);

	ClassRegistry::bind_method(D_METHOD("obstacle_create"), &NavigationSystem2D::obstacle_create);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_avoidance_enabled", "obstacle", "enabled"), &NavigationSystem2D::obstacle_set_avoidance_enabled);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_avoidance_enabled", "obstacle"), &NavigationSystem2D::obstacle_get_avoidance_enabled);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_map", "obstacle", "map"), &NavigationSystem2D::obstacle_set_map);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_map", "obstacle"), &NavigationSystem2D::obstacle_get_map);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_paused", "obstacle", "paused"), &NavigationSystem2D::obstacle_set_paused);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_paused", "obstacle"), &NavigationSystem2D::obstacle_get_paused);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_radius", "obstacle", "radius"), &NavigationSystem2D::obstacle_set_radius);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_radius", "obstacle"), &NavigationSystem2D::obstacle_get_radius);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_velocity", "obstacle", "velocity"), &NavigationSystem2D::obstacle_set_velocity);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_velocity", "obstacle"), &NavigationSystem2D::obstacle_get_velocity);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_position", "obstacle", "position"), &NavigationSystem2D::obstacle_set_position);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_position", "obstacle"), &NavigationSystem2D::obstacle_get_position);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_vertices", "obstacle", "vertices"), &NavigationSystem2D::obstacle_set_vertices);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_vertices", "obstacle"), &NavigationSystem2D::obstacle_get_vertices);
	ClassRegistry::bind_method(D_METHOD("obstacle_set_avoidance_layers", "obstacle", "layers"), &NavigationSystem2D::obstacle_set_avoidance_layers);
	ClassRegistry::bind_method(D_METHOD("obstacle_get_avoidance_layers", "obstacle"), &NavigationSystem2D::obstacle_get_avoidance_layers);

	ClassRegistry::bind_method(D_METHOD("parse_source_geometry_data", "navigation_polygon", "source_geometry_data", "root_node", "callback"), &NavigationSystem2D::parse_source_geometry_data, DEFVAL(Callable()));
	ClassRegistry::bind_method(D_METHOD("bake_from_source_geometry_data", "navigation_polygon", "source_geometry_data", "callback"), &NavigationSystem2D::bake_from_source_geometry_data, DEFVAL(Callable()));
	ClassRegistry::bind_method(D_METHOD("bake_from_source_geometry_data_async", "navigation_polygon", "source_geometry_data", "callback"), &NavigationSystem2D::bake_from_source_geometry_data_async, DEFVAL(Callable()));
	ClassRegistry::bind_method(D_METHOD("is_baking_navigation_polygon", "navigation_polygon"), &NavigationSystem2D::is_baking_navigation_polygon);

	ClassRegistry::bind_method(D_METHOD("source_geometry_parser_create"), &NavigationSystem2D::source_geometry_parser_create);
	ClassRegistry::bind_method(D_METHOD("source_geometry_parser_set_callback", "parser", "callback"), &NavigationSystem2D::source_geometry_parser_set_callback);

	ClassRegistry::bind_method(D_METHOD("simplify_path", "path", "epsilon"), &NavigationSystem2D::simplify_path);

	ClassRegistry::bind_method(D_METHOD("free_rid", "rid"), &NavigationSystem2D::free_rid);

	ClassRegistry::bind_method(D_METHOD("set_active", "active"), &NavigationSystem2D::set_active);

	ClassRegistry::bind_method(D_METHOD("set_debug_enabled", "enabled"), &NavigationSystem2D::set_debug_enabled);
	ClassRegistry::bind_method(D_METHOD("get_debug_enabled"), &NavigationSystem2D::get_debug_enabled);

	ADD_SIGNAL(MethodInfo("map_changed", PropertyInfo(Variant::RID, "map")));

	ADD_SIGNAL(MethodInfo("navigation_debug_changed"));
	ADD_SIGNAL(MethodInfo("avoidance_debug_changed"));

	ClassRegistry::bind_method(D_METHOD("get_process_info", "process_info"), &NavigationSystem2D::get_process_info);

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

NavigationSystem2D *NavigationSystem2D::get_singleton() {
	return singleton;
}

NavigationSystem2D::NavigationSystem2D() {
	ERR_FAIL_COND(singleton != nullptr);
	singleton = this;

	GLOBAL_DEF_BASIC(PropertyInfo(Variant::FLOAT, "navigation/planar/default_cell_size", PROPERTY_HINT_RANGE, NavigationDefaults2D::NAV_MESH_CELL_SIZE_HINT), NavigationDefaults2D::NAV_MESH_CELL_SIZE);
	GLOBAL_DEF("navigation/planar/use_edge_connections", true);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "navigation/planar/merge_rasterizer_cell_scale", PROPERTY_HINT_RANGE, "0.001,1,0.001,or_greater"), 1.0);
	GLOBAL_DEF_BASIC(PropertyInfo(Variant::FLOAT, "navigation/planar/default_edge_connection_margin", PROPERTY_HINT_RANGE, "0.01,10,0.001,or_greater"), NavigationDefaults2D::EDGE_CONNECTION_MARGIN);
	GLOBAL_DEF_BASIC(PropertyInfo(Variant::FLOAT, "navigation/planar/default_link_connection_radius", PROPERTY_HINT_RANGE, "0.01,10,0.001,or_greater"), NavigationDefaults2D::LINK_CONNECTION_RADIUS);

#ifdef DEBUG_ENABLED
	debug_navigation_edge_connection_color = GLOBAL_DEF("debug/shapes/navigation/planar/edge_connection_color", Color(1.0, 0.0, 1.0, 1.0));
	debug_navigation_geometry_edge_color = GLOBAL_DEF("debug/shapes/navigation/planar/geometry_edge_color", Color(0.5, 1.0, 1.0, 1.0));
	debug_navigation_geometry_face_color = GLOBAL_DEF("debug/shapes/navigation/planar/geometry_face_color", Color(0.5, 1.0, 1.0, 0.4));
	debug_navigation_geometry_edge_disabled_color = GLOBAL_DEF("debug/shapes/navigation/planar/geometry_edge_disabled_color", Color(0.5, 0.5, 0.5, 1.0));
	debug_navigation_geometry_face_disabled_color = GLOBAL_DEF("debug/shapes/navigation/planar/geometry_face_disabled_color", Color(0.5, 0.5, 0.5, 0.4));
	debug_navigation_link_connection_color = GLOBAL_DEF("debug/shapes/navigation/planar/link_connection_color", Color(1.0, 0.5, 1.0, 1.0));
	debug_navigation_link_connection_disabled_color = GLOBAL_DEF("debug/shapes/navigation/planar/link_connection_disabled_color", Color(0.5, 0.5, 0.5, 1.0));
	debug_navigation_agent_path_color = GLOBAL_DEF("debug/shapes/navigation/planar/agent_path_color", Color(1.0, 0.0, 0.0, 1.0));

	debug_navigation_enable_edge_connections = GLOBAL_DEF("debug/shapes/navigation/planar/enable_edge_connections", true);
	debug_navigation_enable_edge_lines = GLOBAL_DEF("debug/shapes/navigation/planar/enable_edge_lines", true);
	debug_navigation_enable_geometry_face_random_color = GLOBAL_DEF("debug/shapes/navigation/planar/enable_geometry_face_random_color", true);
	debug_navigation_enable_link_connections = GLOBAL_DEF("debug/shapes/navigation/planar/enable_link_connections", true);

	debug_navigation_enable_agent_paths = GLOBAL_DEF("debug/shapes/navigation/planar/enable_agent_paths", true);
	debug_navigation_agent_path_point_size = GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "debug/shapes/navigation/planar/agent_path_point_size", PROPERTY_HINT_RANGE, "0.01,10,0.001,or_greater"), 4.0);

	debug_navigation_avoidance_agents_radius_color = GLOBAL_DEF("debug/shapes/avoidance/planar/agents_radius_color", Color(1.0, 1.0, 0.0, 0.25));
	debug_navigation_avoidance_obstacles_radius_color = GLOBAL_DEF("debug/shapes/avoidance/planar/obstacles_radius_color", Color(1.0, 0.5, 0.0, 0.25));
	debug_navigation_avoidance_static_obstacle_pushin_face_color = GLOBAL_DEF("debug/shapes/avoidance/planar/obstacles_static_face_pushin_color", Color(1.0, 0.0, 0.0, 0.0));
	debug_navigation_avoidance_static_obstacle_pushin_edge_color = GLOBAL_DEF("debug/shapes/avoidance/planar/obstacles_static_edge_pushin_color", Color(1.0, 0.0, 0.0, 1.0));
	debug_navigation_avoidance_static_obstacle_pushout_face_color = GLOBAL_DEF("debug/shapes/avoidance/planar/obstacles_static_face_pushout_color", Color(1.0, 1.0, 0.0, 0.5));
	debug_navigation_avoidance_static_obstacle_pushout_edge_color = GLOBAL_DEF("debug/shapes/avoidance/planar/obstacles_static_edge_pushout_color", Color(1.0, 1.0, 0.0, 1.0));
	debug_navigation_avoidance_enable_agents_radius = GLOBAL_DEF("debug/shapes/avoidance/planar/enable_agents_radius", true);
	debug_navigation_avoidance_enable_obstacles_radius = GLOBAL_DEF("debug/shapes/avoidance/planar/enable_obstacles_radius", true);
	debug_navigation_avoidance_enable_obstacles_static = GLOBAL_DEF("debug/shapes/avoidance/planar/enable_obstacles_static", true);

	if (Engine::get_singleton()->is_editor_hint()) {
		// Enable NavigationSystem2D when in Editor or navigation mesh edge connections are invisible.
		// On runtime tests Stage has "Visible Navigation" set and main iteration takes care of this.
		set_debug_enabled(true);
		set_debug_navigation_enabled(true);
		set_debug_avoidance_enabled(true);
	}
#endif // DEBUG_ENABLED
}

NavigationSystem2D::~NavigationSystem2D() {
	singleton = nullptr;

	RWLockWrite write_lock(geometry_parser_rwlock);
	for (NavMeshGeometryParser2D *parser : generator_parsers) {
		geometry_parser_owner.free(parser->self);
	}
	generator_parsers.clear();
}

RID NavigationSystem2D::source_geometry_parser_create() {
	RWLockWrite write_lock(geometry_parser_rwlock);

	RID rid = geometry_parser_owner.make_rid();

	NavMeshGeometryParser2D *parser = geometry_parser_owner.get_or_null(rid);
	parser->self = rid;

	generator_parsers.push_back(parser);

	return rid;
}

void NavigationSystem2D::free_rid(RID p_rid) {
	if (!geometry_parser_owner.owns(p_rid)) {
		return;
	}
	RWLockWrite write_lock(geometry_parser_rwlock);

	NavMeshGeometryParser2D *parser = geometry_parser_owner.get_or_null(p_rid);
	ERR_FAIL_NULL(parser);

	generator_parsers.erase(parser);
	geometry_parser_owner.free(parser->self);
}

void NavigationSystem2D::source_geometry_parser_set_callback(RID p_parser, const Callable &p_callback) {
	RWLockWrite write_lock(geometry_parser_rwlock);

	NavMeshGeometryParser2D *parser = geometry_parser_owner.get_or_null(p_parser);
	ERR_FAIL_NULL(parser);

	parser->callback = p_callback;
}

void NavigationSystem2D::set_debug_enabled(bool p_enabled) {
#ifdef DEBUG_ENABLED
	if (debug_enabled != p_enabled) {
		debug_dirty = true;
	}

	debug_enabled = p_enabled;

	if (debug_dirty) {
		navigation_debug_dirty = true;
		callable_mp(this, &NavigationSystem2D::_emit_navigation_debug_changed_signal).call_deferred();

		avoidance_debug_dirty = true;
		callable_mp(this, &NavigationSystem2D::_emit_avoidance_debug_changed_signal).call_deferred();
	}
#endif // DEBUG_ENABLED
}

bool NavigationSystem2D::get_debug_enabled() const {
	return debug_enabled;
}

#ifdef DEBUG_ENABLED
void NavigationSystem2D::_emit_navigation_debug_changed_signal() {
	if (navigation_debug_dirty) {
		navigation_debug_dirty = false;
		emit_signal(SNAME("navigation_debug_changed"));
	}
}

void NavigationSystem2D::_emit_avoidance_debug_changed_signal() {
	if (avoidance_debug_dirty) {
		avoidance_debug_dirty = false;
		emit_signal(SNAME("avoidance_debug_changed"));
	}
}
#endif // DEBUG_ENABLED

#ifdef DEBUG_ENABLED
void NavigationSystem2D::set_debug_navigation_enabled(bool p_enabled) {
	debug_navigation_enabled = p_enabled;
	navigation_debug_dirty = true;
	callable_mp(this, &NavigationSystem2D::_emit_navigation_debug_changed_signal).call_deferred();
}

bool NavigationSystem2D::get_debug_navigation_enabled() const {
	return debug_navigation_enabled;
}

void NavigationSystem2D::set_debug_avoidance_enabled(bool p_enabled) {
	debug_avoidance_enabled = p_enabled;
	avoidance_debug_dirty = true;
	callable_mp(this, &NavigationSystem2D::_emit_avoidance_debug_changed_signal).call_deferred();
}

bool NavigationSystem2D::get_debug_avoidance_enabled() const {
	return debug_avoidance_enabled;
}

void NavigationSystem2D::set_debug_navigation_edge_connection_color(const Color &p_color) {
	debug_navigation_edge_connection_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_edge_connection_color() const {
	return debug_navigation_edge_connection_color;
}

void NavigationSystem2D::set_debug_navigation_geometry_face_color(const Color &p_color) {
	debug_navigation_geometry_face_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_geometry_face_color() const {
	return debug_navigation_geometry_face_color;
}

void NavigationSystem2D::set_debug_navigation_geometry_face_disabled_color(const Color &p_color) {
	debug_navigation_geometry_face_disabled_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_geometry_face_disabled_color() const {
	return debug_navigation_geometry_face_disabled_color;
}

void NavigationSystem2D::set_debug_navigation_link_connection_color(const Color &p_color) {
	debug_navigation_link_connection_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_link_connection_color() const {
	return debug_navigation_link_connection_color;
}

void NavigationSystem2D::set_debug_navigation_link_connection_disabled_color(const Color &p_color) {
	debug_navigation_link_connection_disabled_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_link_connection_disabled_color() const {
	return debug_navigation_link_connection_disabled_color;
}

void NavigationSystem2D::set_debug_navigation_geometry_edge_color(const Color &p_color) {
	debug_navigation_geometry_edge_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_geometry_edge_color() const {
	return debug_navigation_geometry_edge_color;
}

void NavigationSystem2D::set_debug_navigation_geometry_edge_disabled_color(const Color &p_color) {
	debug_navigation_geometry_edge_disabled_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_geometry_edge_disabled_color() const {
	return debug_navigation_geometry_edge_disabled_color;
}

void NavigationSystem2D::set_debug_navigation_enable_edge_connections(const bool p_value) {
	debug_navigation_enable_edge_connections = p_value;
}

bool NavigationSystem2D::get_debug_navigation_enable_edge_connections() const {
	return debug_navigation_enable_edge_connections;
}

void NavigationSystem2D::set_debug_navigation_enable_geometry_face_random_color(const bool p_value) {
	debug_navigation_enable_geometry_face_random_color = p_value;
}

bool NavigationSystem2D::get_debug_navigation_enable_geometry_face_random_color() const {
	return debug_navigation_enable_geometry_face_random_color;
}

void NavigationSystem2D::set_debug_navigation_enable_edge_lines(const bool p_value) {
	debug_navigation_enable_edge_lines = p_value;
}

bool NavigationSystem2D::get_debug_navigation_enable_edge_lines() const {
	return debug_navigation_enable_edge_lines;
}

void NavigationSystem2D::set_debug_navigation_agent_path_color(const Color &p_color) {
	debug_navigation_agent_path_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_agent_path_color() const {
	return debug_navigation_agent_path_color;
}

void NavigationSystem2D::set_debug_navigation_enable_agent_paths(const bool p_value) {
	debug_navigation_enable_agent_paths = p_value;
}

bool NavigationSystem2D::get_debug_navigation_enable_agent_paths() const {
	return debug_navigation_enable_agent_paths;
}

void NavigationSystem2D::set_debug_navigation_agent_path_point_size(real_t p_point_size) {
	debug_navigation_agent_path_point_size = p_point_size;
}

real_t NavigationSystem2D::get_debug_navigation_agent_path_point_size() const {
	return debug_navigation_agent_path_point_size;
}

void NavigationSystem2D::set_debug_navigation_avoidance_enable_agents_radius(const bool p_value) {
	debug_navigation_avoidance_enable_agents_radius = p_value;
}

bool NavigationSystem2D::get_debug_navigation_avoidance_enable_agents_radius() const {
	return debug_navigation_avoidance_enable_agents_radius;
}

void NavigationSystem2D::set_debug_navigation_avoidance_enable_obstacles_radius(const bool p_value) {
	debug_navigation_avoidance_enable_obstacles_radius = p_value;
}

bool NavigationSystem2D::get_debug_navigation_avoidance_enable_obstacles_radius() const {
	return debug_navigation_avoidance_enable_obstacles_radius;
}

void NavigationSystem2D::set_debug_navigation_avoidance_agents_radius_color(const Color &p_color) {
	debug_navigation_avoidance_agents_radius_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_avoidance_agents_radius_color() const {
	return debug_navigation_avoidance_agents_radius_color;
}

void NavigationSystem2D::set_debug_navigation_avoidance_obstacles_radius_color(const Color &p_color) {
	debug_navigation_avoidance_obstacles_radius_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_avoidance_obstacles_radius_color() const {
	return debug_navigation_avoidance_obstacles_radius_color;
}

void NavigationSystem2D::set_debug_navigation_avoidance_static_obstacle_pushin_face_color(const Color &p_color) {
	debug_navigation_avoidance_static_obstacle_pushin_face_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_avoidance_static_obstacle_pushin_face_color() const {
	return debug_navigation_avoidance_static_obstacle_pushin_face_color;
}

void NavigationSystem2D::set_debug_navigation_avoidance_static_obstacle_pushout_face_color(const Color &p_color) {
	debug_navigation_avoidance_static_obstacle_pushout_face_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_avoidance_static_obstacle_pushout_face_color() const {
	return debug_navigation_avoidance_static_obstacle_pushout_face_color;
}

void NavigationSystem2D::set_debug_navigation_avoidance_static_obstacle_pushin_edge_color(const Color &p_color) {
	debug_navigation_avoidance_static_obstacle_pushin_edge_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_avoidance_static_obstacle_pushin_edge_color() const {
	return debug_navigation_avoidance_static_obstacle_pushin_edge_color;
}

void NavigationSystem2D::set_debug_navigation_avoidance_static_obstacle_pushout_edge_color(const Color &p_color) {
	debug_navigation_avoidance_static_obstacle_pushout_edge_color = p_color;
}

Color NavigationSystem2D::get_debug_navigation_avoidance_static_obstacle_pushout_edge_color() const {
	return debug_navigation_avoidance_static_obstacle_pushout_edge_color;
}

void NavigationSystem2D::set_debug_navigation_avoidance_enable_obstacles_static(const bool p_value) {
	debug_navigation_avoidance_enable_obstacles_static = p_value;
}

bool NavigationSystem2D::get_debug_navigation_avoidance_enable_obstacles_static() const {
	return debug_navigation_avoidance_enable_obstacles_static;
}
#endif // DEBUG_ENABLED
