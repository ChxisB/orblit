/**************************************************************************/
/*  nav_base_spatial.h                                                         */

#pragma once

#include "nav_rid_spatial.h"

#include "core/object/object_id.h"
#include "systems/navigation_spatial/navigation_constants_spatial.h"

class NavMap3D;

class NavBase3D : public NavRid3D {
protected:
	uint32_t navigation_layers = 1;
	real_t enter_cost = 0.0;
	real_t travel_cost = 1.0;
	ObjectID owner_id;
	NavigationEnums3D::PathSegmentType type;

public:
	NavigationEnums3D::PathSegmentType get_type() const { return type; }

	virtual void set_use_edge_connections(bool p_enabled) {}
	virtual bool get_use_edge_connections() const { return false; }

	virtual void set_navigation_layers(uint32_t p_navigation_layers) {}
	uint32_t get_navigation_layers() const { return navigation_layers; }

	virtual void set_enter_cost(real_t p_enter_cost) {}
	real_t get_enter_cost() const { return enter_cost; }

	virtual void set_travel_cost(real_t p_travel_cost) {}
	real_t get_travel_cost() const { return travel_cost; }

	virtual void set_owner_id(ObjectID p_owner_id) {}
	ObjectID get_owner_id() const { return owner_id; }

	virtual ~NavBase3D() {}
};
