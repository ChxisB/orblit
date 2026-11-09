/**************************************************************************/
/*  physics_get_rest_info_result_planar.h                                     */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_planar/physics_system_planar_types.h"

class PhysicsGetRestInfoResult2D : public RefCounted {
	ORBLIT_CLASS(PhysicsGetRestInfoResult2D, RefCounted);

	friend class PhysicsDirectSpaceState2D;

	PS2DT::ShapeRestInfo result;

protected:
	static void register_members();

public:
	Vector2 get_point() const;
	Vector2 get_normal() const;
	RID get_collider_rid() const;
	ObjectID get_collider_id() const;
	int get_collider_shape() const;
	Vector2 get_collider_velocity() const;
};
