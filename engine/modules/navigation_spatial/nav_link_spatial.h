/**************************************************************************/
/*  nav_link_spatial.h                                                         */

#pragma once

#include "spatial/nav_base_iteration_spatial.h"
#include "nav_base_spatial.h"
#include "nav_utils_spatial.h"

#include "core/os/rw_lock.h"

class NavLinkIteration3D : public NavBaseIteration3D {
	ORBLIT_CLASS(NavLinkIteration3D, NavBaseIteration3D);

public:
	bool bidirectional = true;
	Vector3 start_position;
	Vector3 end_position;

	Vector3 get_start_position() const { return start_position; }
	Vector3 get_end_position() const { return end_position; }
	bool is_bidirectional() const { return bidirectional; }

	virtual ~NavLinkIteration3D() override {
		navmesh_polygons.clear();
		internal_connections.clear();
	}
};

#include "core/containers/self_list.h"

class NavLink3D : public NavBase3D {
	NavMap3D *map = nullptr;
	bool bidirectional = true;
	Vector3 start_position;
	Vector3 end_position;
	bool enabled = true;

	SelfList<NavLink3D> sync_dirty_request_list_element;

	uint32_t iteration_id = 0;

	mutable RWLock iteration_rwlock;
	Ref<NavLinkIteration3D> iteration;

	bool iteration_dirty = true;
	bool iteration_building = false;
	bool iteration_ready = false;

	void _build_iteration();
	void _sync_iteration();

public:
	NavLink3D();
	~NavLink3D();

	uint32_t get_iteration_id() const { return iteration_id; }

	void set_map(NavMap3D *p_map);
	NavMap3D *get_map() const {
		return map;
	}

	void set_enabled(bool p_enabled);
	bool get_enabled() const { return enabled; }

	void set_bidirectional(bool p_bidirectional);
	bool is_bidirectional() const {
		return bidirectional;
	}

	void set_start_position(Vector3 p_position);
	Vector3 get_start_position() const {
		return start_position;
	}

	void set_end_position(Vector3 p_position);
	Vector3 get_end_position() const {
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

	Ref<NavLinkIteration3D> get_iteration();
};
