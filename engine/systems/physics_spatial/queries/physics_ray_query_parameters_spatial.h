/**************************************************************************/
/*  physics_ray_query_parameters_spatial.h                                     */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_spatial/physics_system_spatial_types.h"

class PhysicsRayQueryParameters3D : public RefCounted {
	ORBLIT_CLASS(PhysicsRayQueryParameters3D, RefCounted);

	PS3DT::RayParameters parameters;

protected:
	static void register_members();

public:
	static Ref<PhysicsRayQueryParameters3D> create(Vector3 p_from, Vector3 p_to, uint32_t p_mask, const TypedArray<RID> &p_exclude);
	const PS3DT::RayParameters &get_parameters() const { return parameters; }

	void set_from(const Vector3 &p_from) { parameters.from = p_from; }
	const Vector3 &get_from() const { return parameters.from; }

	void set_to(const Vector3 &p_to) { parameters.to = p_to; }
	const Vector3 &get_to() const { return parameters.to; }

	void set_collision_mask(uint32_t p_mask) { parameters.collision_mask = p_mask; }
	uint32_t get_collision_mask() const { return parameters.collision_mask; }

	void set_collide_with_bodies(bool p_enable) { parameters.collide_with_bodies = p_enable; }
	bool is_collide_with_bodies_enabled() const { return parameters.collide_with_bodies; }

	void set_collide_with_areas(bool p_enable) { parameters.collide_with_areas = p_enable; }
	bool is_collide_with_areas_enabled() const { return parameters.collide_with_areas; }

	void set_hit_from_inside(bool p_enable) { parameters.hit_from_inside = p_enable; }
	bool is_hit_from_inside_enabled() const { return parameters.hit_from_inside; }

	void set_hit_back_faces(bool p_enable) { parameters.hit_back_faces = p_enable; }
	bool is_hit_back_faces_enabled() const { return parameters.hit_back_faces; }

	void set_exclude(const TypedArray<RID> &p_exclude);
	TypedArray<RID> get_exclude() const;
};
