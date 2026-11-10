/**************************************************************************/
/*  physics_intersect_ray_result_spatial.h                                     */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_spatial/physics_system_spatial_types.h"

class PhysicsIntersectRayResult3D : public RefCounted {
	ORBLIT_CLASS(PhysicsIntersectRayResult3D, RefCounted);

	friend class PhysicsDirectSpaceState3D;

	PS3DT::RayResult result;

protected:
	static void register_members();

public:
	Vector3 get_position() const;
	Vector3 get_normal() const;
	RID get_collider_rid() const;
	ObjectID get_collider_id() const;
	Object *get_collider() const;
	int get_collider_shape() const;
	int get_collider_face_index() const;
};
