/**************************************************************************/
/*  physics_get_rest_info_result_spatial.h                                     */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_spatial/physics_system_spatial_types.h"

class PhysicsGetRestInfoResult3D : public RefCounted {
	ORBLIT_CLASS(PhysicsGetRestInfoResult3D, RefCounted);

	friend class PhysicsDirectSpaceState3D;

	PS3DT::ShapeRestInfo result;

protected:
	static void register_members();

public:
	Vector3 get_point() const;
	Vector3 get_normal() const;
	RID get_collider_rid() const;
	ObjectID get_collider_id() const;
	int get_collider_shape() const;
	Vector3 get_collider_velocity() const;
};
