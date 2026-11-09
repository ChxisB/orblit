/**************************************************************************/
/*  static_body_planar.h                                                      */

#pragma once

#include "scene/planar/physics/physics_body_planar.h"

class PhysicsMaterial;

#ifndef NAVIGATION_PLANAR_DISABLED
class NavigationPolygon;
class NavigationMeshSourceGeometryData2D;
#endif

class StaticBody2D : public PhysicsBody2D {
	ORBLIT_CLASS(StaticBody2D, PhysicsBody2D);

private:
	Vector2 constant_linear_velocity;
	real_t constant_angular_velocity = 0.0;

	Ref<PhysicsMaterial> physics_material_override;

protected:
	static void register_members();

public:
	void set_physics_material_override(const Ref<PhysicsMaterial> &p_physics_material_override);
	Ref<PhysicsMaterial> get_physics_material_override() const;

	void set_constant_linear_velocity(const Vector2 &p_vel);
	void set_constant_angular_velocity(real_t p_vel);

	Vector2 get_constant_linear_velocity() const;
	real_t get_constant_angular_velocity() const;

	StaticBody2D(PS2DE::BodyMode p_mode = PS2DE::BODY_MODE_STATIC);

private:
	static Callable _navmesh_source_geometry_parsing_callback;
	static RID _navmesh_source_geometry_parser;

#ifndef NAVIGATION_PLANAR_DISABLED
public:
	static void navmesh_parse_init();
	static void navmesh_parse_source_geometry(const Ref<NavigationPolygon> &p_navigation_mesh, Ref<NavigationMeshSourceGeometryData2D> p_source_geometry_data, Entity *p_node);
#endif // NAVIGATION_PLANAR_DISABLED

private:
	void _reload_physics_characteristics();
};
