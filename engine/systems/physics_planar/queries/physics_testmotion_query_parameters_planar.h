/**************************************************************************/
/*  physics_testmotion_query_parameters_planar.h                              */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_planar/physics_system_planar_types.h"

class PhysicsTestMotionParameters2D : public RefCounted {
	ORBLIT_CLASS(PhysicsTestMotionParameters2D, RefCounted);

	PS2DT::MotionParameters parameters;

protected:
	static void register_members();

public:
	const PS2DT::MotionParameters &get_parameters() const { return parameters; }

	const Transform2D &get_from() const { return parameters.from; }
	void set_from(const Transform2D &p_from) { parameters.from = p_from; }

	const Vector2 &get_motion() const { return parameters.motion; }
	void set_motion(const Vector2 &p_motion) { parameters.motion = p_motion; }

	real_t get_margin() const { return parameters.margin; }
	void set_margin(real_t p_margin) { parameters.margin = p_margin; }

	bool is_collide_separation_ray_enabled() const { return parameters.collide_separation_ray; }
	void set_collide_separation_ray_enabled(bool p_enabled) { parameters.collide_separation_ray = p_enabled; }

	TypedArray<RID> get_exclude_bodies() const;
	void set_exclude_bodies(const TypedArray<RID> &p_exclude);

	TypedArray<uint64_t> get_exclude_objects() const;
	void set_exclude_objects(const TypedArray<uint64_t> &p_exclude);

	bool is_recovery_as_collision_enabled() const { return parameters.recovery_as_collision; }
	void set_recovery_as_collision_enabled(bool p_enabled) { parameters.recovery_as_collision = p_enabled; }
};
