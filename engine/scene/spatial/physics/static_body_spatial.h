/**************************************************************************/
/*  static_body_spatial.h                                                      */

#pragma once

#include "scene/spatial/physics/physics_body_spatial.h"

class PhysicsMaterial;

#ifndef NAVIGATION_SPATIAL_DISABLED
class NavigationMesh;
class NavigationMeshSourceGeometryData3D;
#endif // NAVIGATION_SPATIAL_DISABLED

class StaticBody3D : public PhysicsBody3D {
	ORBLIT_CLASS(StaticBody3D, PhysicsBody3D);

private:
	Vector3 constant_linear_velocity;
	Vector3 constant_angular_velocity;

	Ref<PhysicsMaterial> physics_material_override;

protected:
	static void register_members();

public:
	void set_physics_material_override(const Ref<PhysicsMaterial> &p_physics_material_override);
	Ref<PhysicsMaterial> get_physics_material_override() const;

	void set_constant_linear_velocity(const Vector3 &p_vel);
	void set_constant_angular_velocity(const Vector3 &p_vel);

	Vector3 get_constant_linear_velocity() const;
	Vector3 get_constant_angular_velocity() const;

	StaticBody3D(PS3DE::BodyMode p_mode = PS3DE::BODY_MODE_STATIC);

private:
	void _reload_physics_characteristics();

#ifndef NAVIGATION_SPATIAL_DISABLED
	static Callable _navmesh_source_geometry_parsing_callback;
	static RID _navmesh_source_geometry_parser;

public:
	static void navmesh_parse_init();
	static void navmesh_parse_source_geometry(const Ref<NavigationMesh> &p_navigation_mesh, Ref<NavigationMeshSourceGeometryData3D> p_source_geometry_data, Entity *p_node);
#endif // NAVIGATION_SPATIAL_DISABLED
};
