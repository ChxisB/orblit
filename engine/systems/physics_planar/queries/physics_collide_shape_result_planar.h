/**************************************************************************/
/*  physics_collide_shape_result_planar.h                                     */

#pragma once

#include "core/object/ref_counted.h"

class PhysicsCollideShapeResult2D : public RefCounted {
	ORBLIT_CLASS(PhysicsCollideShapeResult2D, RefCounted);

	friend class PhysicsDirectSpaceState2D;

	LocalVector<Vector2> result;
	int collision_count = 0;

protected:
	static void register_members();

public:
	PhysicsCollideShapeResult2D(int p_max_collisions = 32);

	int get_max_collisions() const;
	void set_max_collisions(int p_max_collisions);

	int get_collision_count() const;

	Vector2 get_point_on_queried_shape(int p_collision_index) const;
	Vector2 get_point_on_colliding_shape(int p_collision_index) const;
};
