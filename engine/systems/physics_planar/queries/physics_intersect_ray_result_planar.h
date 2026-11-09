/**************************************************************************/
/*  physics_intersect_ray_result_planar.h                                     */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_planar/physics_system_planar_types.h"

class PhysicsIntersectRayResult2D : public RefCounted {
	ORBLIT_CLASS(PhysicsIntersectRayResult2D, RefCounted);

	friend class PhysicsDirectSpaceState2D;

	PS2DT::RayResult result;

protected:
	static void register_members();

public:
	Vector2 get_position() const;
	Vector2 get_normal() const;
	RID get_collider_rid() const;
	ObjectID get_collider_id() const;
	Object *get_collider() const;
	int get_collider_shape() const;
};
