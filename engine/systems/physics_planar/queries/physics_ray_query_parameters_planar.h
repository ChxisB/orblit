/**************************************************************************/
/*  physics_ray_query_parameters_planar.h                                     */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_planar/physics_system_planar_types.h"

template <typename T>
class TypedArray;

class PhysicsRayQueryParameters2D : public RefCounted {
	ORBLIT_CLASS(PhysicsRayQueryParameters2D, RefCounted);

	PS2DT::RayParameters parameters;

protected:
	static void register_members();

public:
	static Ref<PhysicsRayQueryParameters2D> create(Vector2 p_from, Vector2 p_to, uint32_t p_mask, const TypedArray<RID> &p_exclude);
	const PS2DT::RayParameters &get_parameters() const { return parameters; }

	void set_from(const Vector2 &p_from) { parameters.from = p_from; }
	const Vector2 &get_from() const { return parameters.from; }

	void set_to(const Vector2 &p_to) { parameters.to = p_to; }
	const Vector2 &get_to() const { return parameters.to; }

	void set_collision_mask(uint32_t p_mask) { parameters.collision_mask = p_mask; }
	uint32_t get_collision_mask() const { return parameters.collision_mask; }

	void set_collide_with_bodies(bool p_enable) { parameters.collide_with_bodies = p_enable; }
	bool is_collide_with_bodies_enabled() const { return parameters.collide_with_bodies; }

	void set_collide_with_areas(bool p_enable) { parameters.collide_with_areas = p_enable; }
	bool is_collide_with_areas_enabled() const { return parameters.collide_with_areas; }

	void set_hit_from_inside(bool p_enable) { parameters.hit_from_inside = p_enable; }
	bool is_hit_from_inside_enabled() const { return parameters.hit_from_inside; }

	void set_exclude(const TypedArray<RID> &p_exclude);
	TypedArray<RID> get_exclude() const;
};
