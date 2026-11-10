/**************************************************************************/
/*  physics_intersect_point_result_spatial.h                                   */

#pragma once

#include "core/object/ref_counted.h"
#include "systems/physics_spatial/physics_system_spatial_types.h"

class PhysicsIntersectPointResult3D : public RefCounted {
	ORBLIT_CLASS(PhysicsIntersectPointResult3D, RefCounted);

	friend class PhysicsDirectSpaceState3D;

	LocalVector<PS3DT::ShapeResult> result;
	int intersection_count = 0;

protected:
	static void register_members();

public:
	PhysicsIntersectPointResult3D(int p_max_intersections = 32);

	int get_max_intersections() const;
	void set_max_intersections(int p_max_intersections);

	int get_intersection_count() const;

	RID get_collider_rid(int p_intersection_index) const;
	ObjectID get_collider_id(int p_intersection_index) const;
	Object *get_collider(int p_intersection_index) const;
	int get_collider_shape(int p_intersection_index) const;
};
