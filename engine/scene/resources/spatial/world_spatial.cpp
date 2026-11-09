/**************************************************************************/
/*  world_spatial.cpp                                                          */

#include "world_spatial.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"
#include "scene/spatial/camera_spatial.h"
#include "scene/resources/camera_attributes.h"
#include "scene/resources/environment.h"
#include "systems/rendering/rendering_system.h"

#ifndef NAVIGATION_SPATIAL_DISABLED
#include "systems/navigation_spatial/navigation_system_spatial.h"
#endif // NAVIGATION_SPATIAL_DISABLED

#ifndef PHYSICS_SPATIAL_DISABLED
#include "systems/physics_spatial/physics_system_spatial.h"
#endif // PHYSICS_SPATIAL_DISABLED

void World3D::_register_camera(Camera3D *p_camera) {
	cameras.insert(p_camera);
}

void World3D::_remove_camera(Camera3D *p_camera) {
	cameras.erase(p_camera);
}

RID World3D::get_space() const {
#ifndef PHYSICS_SPATIAL_DISABLED
	if (space.is_null()) {
		space = PhysicsSystem3D::get_singleton()->space_create();
		PhysicsSystem3D::get_singleton()->space_set_active(space, true);
		PhysicsSystem3D::get_singleton()->area_set_param(space, PS3DE::AREA_PARAM_GRAVITY, GLOBAL_GET("physics/spatial/default_gravity"));
		PhysicsSystem3D::get_singleton()->area_set_param(space, PS3DE::AREA_PARAM_GRAVITY_VECTOR, GLOBAL_GET("physics/spatial/default_gravity_vector"));
		PhysicsSystem3D::get_singleton()->area_set_param(space, PS3DE::AREA_PARAM_LINEAR_DAMP, GLOBAL_GET("physics/spatial/default_linear_damp"));
		PhysicsSystem3D::get_singleton()->area_set_param(space, PS3DE::AREA_PARAM_ANGULAR_DAMP, GLOBAL_GET("physics/spatial/default_angular_damp"));
	}
#endif // PHYSICS_SPATIAL_DISABLED
	return space;
}

#ifndef NAVIGATION_SPATIAL_DISABLED
RID World3D::get_navigation_map() const {
	if (navigation_map.is_null()) {
		navigation_map = NavigationSystem3D::get_singleton()->map_create();
		NavigationSystem3D::get_singleton()->map_set_active(navigation_map, true);
		NavigationSystem3D::get_singleton()->map_set_cell_size(navigation_map, GLOBAL_GET("navigation/spatial/default_cell_size"));
		NavigationSystem3D::get_singleton()->map_set_cell_height(navigation_map, GLOBAL_GET("navigation/spatial/default_cell_height"));
		NavigationSystem3D::get_singleton()->map_set_up(navigation_map, GLOBAL_GET("navigation/spatial/default_up"));
		NavigationSystem3D::get_singleton()->map_set_merge_rasterizer_cell_scale(navigation_map, GLOBAL_GET("navigation/spatial/merge_rasterizer_cell_scale"));
		NavigationSystem3D::get_singleton()->map_set_use_edge_connections(navigation_map, GLOBAL_GET("navigation/spatial/use_edge_connections"));
		NavigationSystem3D::get_singleton()->map_set_edge_connection_margin(navigation_map, GLOBAL_GET("navigation/spatial/default_edge_connection_margin"));
		NavigationSystem3D::get_singleton()->map_set_link_connection_radius(navigation_map, GLOBAL_GET("navigation/spatial/default_link_connection_radius"));
	}
	return navigation_map;
}
#endif // NAVIGATION_SPATIAL_DISABLED

RID World3D::get_scenario() const {
	return scenario;
}

void World3D::set_environment(const Ref<Environment> &p_environment) {
	if (environment == p_environment) {
		return;
	}

	environment = p_environment;
	if (environment.is_valid()) {
		RS::get_singleton()->scenario_set_environment(scenario, environment->get_rid());
	} else {
		RS::get_singleton()->scenario_set_environment(scenario, RID());
	}

	emit_changed();
}

Ref<Environment> World3D::get_environment() const {
	return environment;
}

void World3D::set_fallback_environment(const Ref<Environment> &p_environment) {
	if (fallback_environment == p_environment) {
		return;
	}

	fallback_environment = p_environment;
	if (fallback_environment.is_valid()) {
		RS::get_singleton()->scenario_set_fallback_environment(scenario, p_environment->get_rid());
	} else {
		RS::get_singleton()->scenario_set_fallback_environment(scenario, RID());
	}

	emit_changed();
}

Ref<Environment> World3D::get_fallback_environment() const {
	return fallback_environment;
}

void World3D::set_camera_attributes(const Ref<CameraAttributes> &p_camera_attributes) {
	camera_attributes = p_camera_attributes;
	if (camera_attributes.is_valid()) {
		RS::get_singleton()->scenario_set_camera_attributes(scenario, camera_attributes->get_rid());
	} else {
		RS::get_singleton()->scenario_set_camera_attributes(scenario, RID());
	}
}

Ref<CameraAttributes> World3D::get_camera_attributes() const {
	return camera_attributes;
}

void World3D::set_compositor(const Ref<Compositor> &p_compositor) {
	compositor = p_compositor;
	if (compositor.is_valid()) {
		RS::get_singleton()->scenario_set_compositor(scenario, compositor->get_rid());
	} else {
		RS::get_singleton()->scenario_set_compositor(scenario, RID());
	}
}

Ref<Compositor> World3D::get_compositor() const {
	return compositor;
}

#ifndef PHYSICS_SPATIAL_DISABLED
PhysicsDirectSpaceState3D *World3D::get_direct_space_state() {
	return PhysicsSystem3D::get_singleton()->space_get_direct_state(get_space());
}
#endif // PHYSICS_SPATIAL_DISABLED

void World3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_space"), &World3D::get_space);
#ifndef NAVIGATION_SPATIAL_DISABLED
	ClassRegistry::bind_method(D_METHOD("get_navigation_map"), &World3D::get_navigation_map);
#endif // NAVIGATION_SPATIAL_DISABLED
	ClassRegistry::bind_method(D_METHOD("get_scenario"), &World3D::get_scenario);
	ClassRegistry::bind_method(D_METHOD("set_environment", "env"), &World3D::set_environment);
	ClassRegistry::bind_method(D_METHOD("get_environment"), &World3D::get_environment);
	ClassRegistry::bind_method(D_METHOD("set_fallback_environment", "env"), &World3D::set_fallback_environment);
	ClassRegistry::bind_method(D_METHOD("get_fallback_environment"), &World3D::get_fallback_environment);
	ClassRegistry::bind_method(D_METHOD("set_camera_attributes", "attributes"), &World3D::set_camera_attributes);
	ClassRegistry::bind_method(D_METHOD("get_camera_attributes"), &World3D::get_camera_attributes);
#ifndef PHYSICS_SPATIAL_DISABLED
	ClassRegistry::bind_method(D_METHOD("get_direct_space_state"), &World3D::get_direct_space_state);
#endif // PHYSICS_SPATIAL_DISABLED
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "environment", PROPERTY_HINT_RESOURCE_TYPE, Environment::get_static_class_name()), "set_environment", "get_environment");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "fallback_environment", PROPERTY_HINT_RESOURCE_TYPE, Environment::get_static_class_name()), "set_fallback_environment", "get_fallback_environment");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "camera_attributes", PROPERTY_HINT_RESOURCE_TYPE, "CameraAttributesPractical,CameraAttributesPhysical"), "set_camera_attributes", "get_camera_attributes");
	ADD_PROPERTY(PropertyInfo(Variant::RID, "space", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "", "get_space");
#ifndef NAVIGATION_SPATIAL_DISABLED
	ADD_PROPERTY(PropertyInfo(Variant::RID, "navigation_map", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "", "get_navigation_map");
#endif // NAVIGATION_SPATIAL_DISABLED
	ADD_PROPERTY(PropertyInfo(Variant::RID, "scenario", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "", "get_scenario");
#ifndef PHYSICS_SPATIAL_DISABLED
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "direct_space_state", PROPERTY_HINT_RESOURCE_TYPE, PhysicsDirectSpaceState3D::get_static_class_name(), PROPERTY_USAGE_NONE), "", "get_direct_space_state");
#endif // PHYSICS_SPATIAL_DISABLED
}

World3D::World3D() {
	scenario = RenderingSystem::get_singleton()->scenario_create();
}

World3D::~World3D() {
	ERR_FAIL_NULL(RenderingSystem::get_singleton());

#ifndef PHYSICS_SPATIAL_DISABLED
	ERR_FAIL_NULL(PhysicsSystem3D::get_singleton());
#endif // PHYSICS_SPATIAL_DISABLED

#ifndef NAVIGATION_SPATIAL_DISABLED
	ERR_FAIL_NULL(NavigationSystem3D::get_singleton());
#endif // NAVIGATION_SPATIAL_DISABLED

	RenderingSystem::get_singleton()->free_rid(scenario);

#ifndef PHYSICS_SPATIAL_DISABLED
	if (space.is_valid()) {
		PhysicsSystem3D::get_singleton()->free_rid(space);
	}
#endif // PHYSICS_SPATIAL_DISABLED

#ifndef NAVIGATION_SPATIAL_DISABLED
	if (navigation_map.is_valid()) {
		NavigationSystem3D::get_singleton()->free_rid(navigation_map);
	}
#endif // NAVIGATION_SPATIAL_DISABLED
}
