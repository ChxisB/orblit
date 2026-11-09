/**************************************************************************/
/*  physics_point_query_parameters_spatial.h                                   */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_spatial/physics_system_spatial_types.h"

class PhysicsPointQueryParameters3D : public RefCounted {
	ORBLIT_CLASS(PhysicsPointQueryParameters3D, RefCounted);

	PS3DT::PointParameters parameters;

protected:
	static void register_members();

public:
	const PS3DT::PointParameters &get_parameters() const { return parameters; }

	void set_position(const Vector3 &p_position) { parameters.position = p_position; }
	const Vector3 &get_position() const { return parameters.position; }

	void set_collision_mask(uint32_t p_mask) { parameters.collision_mask = p_mask; }
	uint32_t get_collision_mask() const { return parameters.collision_mask; }

	void set_collide_with_bodies(bool p_enable) { parameters.collide_with_bodies = p_enable; }
	bool is_collide_with_bodies_enabled() const { return parameters.collide_with_bodies; }

	void set_collide_with_areas(bool p_enable) { parameters.collide_with_areas = p_enable; }
	bool is_collide_with_areas_enabled() const { return parameters.collide_with_areas; }

	void set_exclude(const TypedArray<RID> &p_exclude);
	TypedArray<RID> get_exclude() const;
};
