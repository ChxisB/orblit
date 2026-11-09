/**************************************************************************/
/*  nav_link_planar.h                                                         */

#pragma once

#include "planar/nav_base_iteration_planar.h"
#include "nav_base_planar.h"
#include "nav_utils_planar.h"

#include "core/os/rw_lock.h"

class NavLinkIteration2D : public NavBaseIteration2D {
	ORBLIT_CLASS(NavLinkIteration2D, NavBaseIteration2D);

public:
	bool bidirectional = true;
	Vector2 start_position;
	Vector2 end_position;

	Vector2 get_start_position() const { return start_position; }
	Vector2 get_end_position() const { return end_position; }
	bool is_bidirectional() const { return bidirectional; }

	virtual ~NavLinkIteration2D() override {
		navmesh_polygons.clear();
		internal_connections.clear();
	}
};

#include "core/containers/self_list.h"

class NavLink2D : public NavBase2D {
	NavMap2D *map = nullptr;
	bool bidirectional = true;
	Vector2 start_position;
	Vector2 end_position;
	bool enabled = true;

	SelfList<NavLink2D> sync_dirty_request_list_element;

	uint32_t iteration_id = 0;

	mutable RWLock iteration_rwlock;
	Ref<NavLinkIteration2D> iteration;

	bool iteration_dirty = true;
	bool iteration_building = false;
	bool iteration_ready = false;

	void _build_iteration();
	void _sync_iteration();

public:
	NavLink2D();
	~NavLink2D();

	uint32_t get_iteration_id() const { return iteration_id; }

	void set_map(NavMap2D *p_map);
	NavMap2D *get_map() const {
		return map;
	}

	void set_enabled(bool p_enabled);
	bool get_enabled() const { return enabled; }

	void set_bidirectional(bool p_bidirectional);
	bool is_bidirectional() const {
		return bidirectional;
	}

	void set_start_position(Vector2 p_position);
	Vector2 get_start_position() const {
		return start_position;
	}

	void set_end_position(Vector2 p_position);
	Vector2 get_end_position() const {
		return end_position;
	}

	// NavBase properties.
	virtual void set_navigation_layers(uint32_t p_navigation_layers) override;
	virtual void set_enter_cost(real_t p_enter_cost) override;
	virtual void set_travel_cost(real_t p_travel_cost) override;
	virtual void set_owner_id(ObjectID p_owner_id) override;

	bool sync();
	void request_sync();
	void cancel_sync_request();

	Ref<NavLinkIteration2D> get_iteration();
};
