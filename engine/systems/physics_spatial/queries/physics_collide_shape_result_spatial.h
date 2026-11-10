/**************************************************************************/
/*  physics_collide_shape_result_spatial.h                                     */

#pragma once

#include "core/object/ref_counted.h"

class PhysicsCollideShapeResult3D : public RefCounted {
	ORBLIT_CLASS(PhysicsCollideShapeResult3D, RefCounted);

	friend class PhysicsDirectSpaceState3D;

	LocalVector<Vector3> result;
	int collision_count = 0;

protected:
	static void register_members();

public:
	PhysicsCollideShapeResult3D(int p_max_collisions = 32);

	int get_max_collisions() const;
	void set_max_collisions(int p_max_collisions);

	int get_collision_count() const;

	Vector3 get_point_on_queried_shape(int p_collision_index) const;
	Vector3 get_point_on_colliding_shape(int p_collision_index) const;
};
