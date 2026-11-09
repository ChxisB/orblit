/**************************************************************************/
/*  nav_base_iteration_planar.h                                               */

#pragma once

#include "../nav_utils_planar.h"

#include "core/object/ref_counted.h"
#include "systems/navigation_planar/navigation_constants_planar.h"

class NavBaseIteration2D : public RefCounted {
	ORBLIT_CLASS(NavBaseIteration2D, RefCounted);

public:
	bool enabled = true;
	uint32_t navigation_layers = 1;
	real_t enter_cost = 0.0;
	real_t travel_cost = 1.0;
	NavigationEnums2D::PathSegmentType owner_type;
	ObjectID owner_object_id;
	RID owner_rid;
	bool owner_use_edge_connections = false;
	LocalVector<Nav2D::Polygon> navmesh_polygons;
	LocalVector<LocalVector<Nav2D::Connection>> internal_connections;

	bool get_enabled() const { return enabled; }
	NavigationEnums2D::PathSegmentType get_type() const { return owner_type; }
	RID get_self() const { return owner_rid; }
	ObjectID get_owner_id() const { return owner_object_id; }
	uint32_t get_navigation_layers() const { return navigation_layers; }
	real_t get_enter_cost() const { return enter_cost; }
	real_t get_travel_cost() const { return travel_cost; }
	bool get_use_edge_connections() const { return owner_use_edge_connections; }
	const LocalVector<Nav2D::Polygon> &get_navmesh_polygons() const { return navmesh_polygons; }
	const LocalVector<LocalVector<Nav2D::Connection>> &get_internal_connections() const { return internal_connections; }

	virtual ~NavBaseIteration2D() {
		navmesh_polygons.clear();
		internal_connections.clear();
	}
};
