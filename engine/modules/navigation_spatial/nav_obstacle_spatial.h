/**************************************************************************/
/*  nav_obstacle_spatial.h                                                     */

#pragma once

#include "nav_rid_spatial.h"

#include "core/containers/self_list.h"
#include "core/math/vector3.h"

class NavAgent3D;
class NavMap3D;

class NavObstacle3D : public NavRid3D {
	NavAgent3D *agent = nullptr;
	NavMap3D *map = nullptr;
	Vector3 velocity;
	Vector3 position;
	Vector<Vector3> vertices;

	real_t radius = 0.0;
	real_t height = 0.0;

	bool avoidance_enabled = false;
	bool use_3d_avoidance = false;
	uint32_t avoidance_layers = 1;

	bool obstacle_dirty = true;

	uint32_t last_map_iteration_id = 0;
	bool paused = false;

	SelfList<NavObstacle3D> sync_dirty_request_list_element;

public:
	NavObstacle3D();
	~NavObstacle3D();

	void set_avoidance_enabled(bool p_enabled);
	bool is_avoidance_enabled() { return avoidance_enabled; }

	void set_use_3d_avoidance(bool p_enabled);
	bool get_use_3d_avoidance() { return use_3d_avoidance; }

	void set_map(NavMap3D *p_map);
	NavMap3D *get_map() { return map; }

	void set_agent(NavAgent3D *p_agent);
	NavAgent3D *get_agent() { return agent; }

	void set_position(const Vector3 p_position);
	const Vector3 &get_position() const { return position; }

	void set_radius(real_t p_radius);
	real_t get_radius() const { return radius; }

	void set_height(const real_t p_height);
	real_t get_height() const { return height; }

	void set_velocity(const Vector3 p_velocity);
	const Vector3 &get_velocity() const { return velocity; }

	void set_vertices(const Vector<Vector3> &p_vertices);
	const Vector<Vector3> &get_vertices() const { return vertices; }

	bool is_map_changed();

	void set_avoidance_layers(uint32_t p_layers);
	uint32_t get_avoidance_layers() const { return avoidance_layers; }

	void set_paused(bool p_paused);
	bool get_paused() const;

	bool is_dirty() const;
	void sync();
	void request_sync();
	void cancel_sync_request();

private:
	void internal_update_agent();
};
