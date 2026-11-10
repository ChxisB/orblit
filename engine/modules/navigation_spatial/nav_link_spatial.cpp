/**************************************************************************/
/*  nav_link_spatial.cpp                                                       */

#include "nav_link_spatial.h"

#include "nav_map_spatial.h"

void NavLink3D::set_map(NavMap3D *p_map) {
	if (map == p_map) {
		return;
	}

	cancel_sync_request();

	if (map) {
		map->remove_link(this);
	}

	map = p_map;
	iteration_dirty = true;

	if (map) {
		map->add_link(this);
		request_sync();
	}
}

void NavLink3D::set_enabled(bool p_enabled) {
	if (enabled == p_enabled) {
		return;
	}
	enabled = p_enabled;
	iteration_dirty = true;

	request_sync();
}

void NavLink3D::set_bidirectional(bool p_bidirectional) {
	if (bidirectional == p_bidirectional) {
		return;
	}
	bidirectional = p_bidirectional;
	iteration_dirty = true;

	request_sync();
}

void NavLink3D::set_start_position(const Vector3 p_position) {
	if (start_position == p_position) {
		return;
	}
	start_position = p_position;
	iteration_dirty = true;

	request_sync();
}

void NavLink3D::set_end_position(const Vector3 p_position) {
	if (end_position == p_position) {
		return;
	}
	end_position = p_position;
	iteration_dirty = true;

	request_sync();
}

void NavLink3D::set_navigation_layers(uint32_t p_navigation_layers) {
	if (navigation_layers == p_navigation_layers) {
		return;
	}
	navigation_layers = p_navigation_layers;
	iteration_dirty = true;

	request_sync();
}

void NavLink3D::set_enter_cost(real_t p_enter_cost) {
	real_t new_enter_cost = MAX(p_enter_cost, 0.0);
	if (enter_cost == new_enter_cost) {
		return;
	}
	enter_cost = new_enter_cost;
	iteration_dirty = true;

	request_sync();
}

void NavLink3D::set_travel_cost(real_t p_travel_cost) {
	real_t new_travel_cost = MAX(p_travel_cost, 0.0);
	if (travel_cost == new_travel_cost) {
		return;
	}
	travel_cost = new_travel_cost;
	iteration_dirty = true;

	request_sync();
}

void NavLink3D::set_owner_id(ObjectID p_owner_id) {
	if (owner_id == p_owner_id) {
		return;
	}
	owner_id = p_owner_id;
	iteration_dirty = true;

	request_sync();
}

bool NavLink3D::sync() {
	bool requires_map_update = false;
	if (!map) {
		return requires_map_update;
	}

	if (iteration_dirty && !iteration_building && !iteration_ready) {
		_build_iteration();
		iteration_ready = false;
		requires_map_update = true;
	}

	return requires_map_update;
}

void NavLink3D::_build_iteration() {
	if (!iteration_dirty || iteration_building || iteration_ready) {
		return;
	}

	iteration_dirty = false;
	iteration_building = true;
	iteration_ready = false;

	Ref<NavLinkIteration3D> new_iteration;
	new_iteration.instantiate();

	new_iteration->navigation_layers = get_navigation_layers();
	new_iteration->enter_cost = get_enter_cost();
	new_iteration->travel_cost = get_travel_cost();
	new_iteration->owner_object_id = get_owner_id();
	new_iteration->owner_type = get_type();
	new_iteration->owner_rid = get_self();

	new_iteration->enabled = get_enabled();
	new_iteration->start_position = get_start_position();
	new_iteration->end_position = get_end_position();
	new_iteration->bidirectional = is_bidirectional();

	RWLockWrite write_lock(iteration_rwlock);
	ERR_FAIL_COND(iteration.is_null());
	iteration = Ref<NavLinkIteration3D>();
	DEV_ASSERT(iteration.is_null());
	iteration = new_iteration;
	iteration_id = iteration_id % UINT32_MAX + 1;

	iteration_building = false;
	iteration_ready = true;
}

void NavLink3D::request_sync() {
	if (map && !sync_dirty_request_list_element.in_list()) {
		map->add_link_sync_dirty_request(&sync_dirty_request_list_element);
	}
}

void NavLink3D::cancel_sync_request() {
	if (map && sync_dirty_request_list_element.in_list()) {
		map->remove_link_sync_dirty_request(&sync_dirty_request_list_element);
	}
}

Ref<NavLinkIteration3D> NavLink3D::get_iteration() {
	RWLockRead read_lock(iteration_rwlock);
	return iteration;
}

NavLink3D::NavLink3D() :
		sync_dirty_request_list_element(this) {
	type = NavigationEnums3D::PathSegmentType::PATH_SEGMENT_TYPE_LINK;
	iteration.instantiate();
}

NavLink3D::~NavLink3D() {
	cancel_sync_request();

	iteration = Ref<NavLinkIteration3D>();
}
