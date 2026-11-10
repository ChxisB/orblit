/**************************************************************************/
/*  world_planar.cpp                                                          */

#include "world_planar.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"
#include "scene/planar/visible_on_screen_notifier_planar.h"
#include "systems/rendering/rendering_system.h"

#ifndef NAVIGATION_PLANAR_DISABLED
#include "systems/navigation_planar/navigation_system_planar.h"
#endif // NAVIGATION_PLANAR_DISABLED

#ifndef PHYSICS_PLANAR_DISABLED
#include "systems/physics_planar/physics_system_planar.h"
#endif // PHYSICS_PLANAR_DISABLED

RID World2D::get_canvas() const {
	return canvas;
}

#ifndef NAVIGATION_PLANAR_DISABLED
RID World2D::get_navigation_map() const {
	if (navigation_map.is_null()) {
		navigation_map = NavigationSystem2D::get_singleton()->map_create();
		NavigationSystem2D::get_singleton()->map_set_active(navigation_map, true);
		NavigationSystem2D::get_singleton()->map_set_cell_size(navigation_map, GLOBAL_GET("navigation/planar/default_cell_size"));
		NavigationSystem2D::get_singleton()->map_set_merge_rasterizer_cell_scale(navigation_map, GLOBAL_GET("navigation/planar/merge_rasterizer_cell_scale"));
		NavigationSystem2D::get_singleton()->map_set_use_edge_connections(navigation_map, GLOBAL_GET("navigation/planar/use_edge_connections"));
		NavigationSystem2D::get_singleton()->map_set_edge_connection_margin(navigation_map, GLOBAL_GET("navigation/planar/default_edge_connection_margin"));
		NavigationSystem2D::get_singleton()->map_set_link_connection_radius(navigation_map, GLOBAL_GET("navigation/planar/default_link_connection_radius"));
	}
	return navigation_map;
}
#endif // NAVIGATION_PLANAR_DISABLED

#ifndef PHYSICS_PLANAR_DISABLED
RID World2D::get_space() const {
	if (space.is_null()) {
		space = PhysicsSystem2D::get_singleton()->space_create();
		PhysicsSystem2D::get_singleton()->space_set_active(space, true);
		PhysicsSystem2D::get_singleton()->area_set_param(space, PS2DE::AREA_PARAM_GRAVITY, GLOBAL_GET("physics/planar/default_gravity"));
		PhysicsSystem2D::get_singleton()->area_set_param(space, PS2DE::AREA_PARAM_GRAVITY_VECTOR, GLOBAL_GET("physics/planar/default_gravity_vector"));
		PhysicsSystem2D::get_singleton()->area_set_param(space, PS2DE::AREA_PARAM_LINEAR_DAMP, GLOBAL_GET("physics/planar/default_linear_damp"));
		PhysicsSystem2D::get_singleton()->area_set_param(space, PS2DE::AREA_PARAM_ANGULAR_DAMP, GLOBAL_GET("physics/planar/default_angular_damp"));
	}
	return space;
}

PhysicsDirectSpaceState2D *World2D::get_direct_space_state() {
	return PhysicsSystem2D::get_singleton()->space_get_direct_state(get_space());
}
#endif // PHYSICS_PLANAR_DISABLED

void World2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_canvas"), &World2D::get_canvas);
	ADD_PROPERTY(PropertyInfo(Variant::RID, "canvas", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "", "get_canvas");

#ifndef NAVIGATION_PLANAR_DISABLED
	ClassRegistry::bind_method(D_METHOD("get_navigation_map"), &World2D::get_navigation_map);
	ADD_PROPERTY(PropertyInfo(Variant::RID, "navigation_map", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "", "get_navigation_map");
#endif // NAVIGATION_PLANAR_DISABLED

#ifndef PHYSICS_PLANAR_DISABLED
	ClassRegistry::bind_method(D_METHOD("get_space"), &World2D::get_space);
	ClassRegistry::bind_method(D_METHOD("get_direct_space_state"), &World2D::get_direct_space_state);
	ADD_PROPERTY(PropertyInfo(Variant::RID, "space", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "", "get_space");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "direct_space_state", PROPERTY_HINT_RESOURCE_TYPE, PhysicsDirectSpaceState2D::get_static_class_name(), PROPERTY_USAGE_NONE), "", "get_direct_space_state");
#endif // PHYSICS_PLANAR_DISABLED
}

void World2D::register_viewport(Viewport *p_viewport) {
	viewports.insert(p_viewport);
}

void World2D::remove_viewport(Viewport *p_viewport) {
	viewports.erase(p_viewport);
}

World2D::World2D() {
	canvas = RenderingSystem::get_singleton()->canvas_create();
}

World2D::~World2D() {
	ERR_FAIL_NULL(RenderingSystem::get_singleton());
	RenderingSystem::get_singleton()->free_rid(canvas);

#ifndef NAVIGATION_PLANAR_DISABLED
	ERR_FAIL_NULL(NavigationSystem2D::get_singleton());
	if (navigation_map.is_valid()) {
		NavigationSystem2D::get_singleton()->free_rid(navigation_map);
	}
#endif // NAVIGATION_PLANAR_DISABLED

#ifndef PHYSICS_PLANAR_DISABLED
	ERR_FAIL_NULL(PhysicsSystem2D::get_singleton());
	if (space.is_valid()) {
		PhysicsSystem2D::get_singleton()->free_rid(space);
	}
#endif // PHYSICS_PLANAR_DISABLED
}
