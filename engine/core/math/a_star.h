/**************************************************************************/
/*  a_star.h                                                              */

#pragma once

#include "core/object/orblit_virtual.gen.h"
#include "core/object/ref_counted.h"
#include "core/containers/a_hash_map.h"

/**
	A* pathfinding algorithm.
*/

class AStar3D : public RefCounted {
	ORBLIT_CLASS(AStar3D, RefCounted);
	friend class AStar2D;

	struct Point {
		int64_t id = 0;
		Vector3 pos;
		real_t weight_scale = 0;
		bool enabled = false;

		AHashMap<int64_t, Point *> neighbors{ 4u };
		AHashMap<int64_t, Point *> unlinked_neighbours{ 4u };

		// Used for pathfinding.
		Point *prev_point = nullptr;
		real_t g_score = 0;
		real_t f_score = 0;
		uint64_t open_pass = 0;
		uint64_t closed_pass = 0;

		// Used for getting closest_point_of_last_pathing_call.
		real_t abs_g_score = 0;
		real_t abs_f_score = 0;
	};

	struct SortPoints {
		_FORCE_INLINE_ bool operator()(const Point *p_left, const Point *p_right) const { // Returns true when the Point A is worse than Point B.
			if (p_left->f_score > p_right->f_score) {
				return true;
			} else if (p_left->f_score < p_right->f_score) {
				return false;
			} else {
				return p_left->g_score < p_right->g_score; // If the f_costs are the same then prioritize the points that are further away from the start.
			}
		}
	};

	struct Segment {
		Pair<int64_t, int64_t> key;

		enum {
			NONE = 0,
			FORWARD = 1,
			BACKWARD = 2,
			BIDIRECTIONAL = FORWARD | BACKWARD
		};
		unsigned char direction = NONE;

		static uint32_t hash(const Segment &p_seg) {
			return HashMapHasherDefault::hash(p_seg.key);
		}
		bool operator==(const Segment &p_s) const { return key == p_s.key; }

		Segment() {}
		Segment(int64_t p_from, int64_t p_to) {
			if (p_from < p_to) {
				key.first = p_from;
				key.second = p_to;
				direction = FORWARD;
			} else {
				key.first = p_to;
				key.second = p_from;
				direction = BACKWARD;
			}
		}
	};

	mutable int64_t last_free_id = 0;
	uint64_t pass = 1;

	AHashMap<int64_t, Point *> points;
	HashSet<Segment, Segment> segments;
	Point *last_closest_point = nullptr;
	bool neighbor_filter_enabled = false;

	bool _solve(Point *p_begin_point, Point *p_end_point, bool p_allow_partial_path);

protected:
	static void register_members();

	virtual real_t _estimate_cost(int64_t p_from_id, int64_t p_end_id);
	virtual real_t _compute_cost(int64_t p_from_id, int64_t p_to_id);

	ORBLIT_VIRTUAL2RC(bool, _filter_neighbor, int64_t, int64_t)
	ORBLIT_VIRTUAL2RC(real_t, _estimate_cost, int64_t, int64_t)
	ORBLIT_VIRTUAL2RC(real_t, _compute_cost, int64_t, int64_t)

public:
	int64_t get_available_point_id() const;

	void add_point(int64_t p_id, const Vector3 &p_pos, real_t p_weight_scale = 1);
	Vector3 get_point_position(int64_t p_id) const;
	void set_point_position(int64_t p_id, const Vector3 &p_pos);
	real_t get_point_weight_scale(int64_t p_id) const;
	void set_point_weight_scale(int64_t p_id, real_t p_weight_scale);
	void remove_point(int64_t p_id);
	bool has_point(int64_t p_id) const;
	Vector<int64_t> get_point_connections(int64_t p_id);
	PackedInt64Array get_point_ids();

	bool is_neighbor_filter_enabled() const;
	void set_neighbor_filter_enabled(bool p_enabled);

	void set_point_disabled(int64_t p_id, bool p_disabled = true);
	bool is_point_disabled(int64_t p_id) const;

	void connect_points(int64_t p_id, int64_t p_with_id, bool p_bidirectional = true);
	void disconnect_points(int64_t p_id, int64_t p_with_id, bool p_bidirectional = true);
	bool are_points_connected(int64_t p_id, int64_t p_with_id, bool p_bidirectional = true) const;

	int64_t get_point_count() const;
	int64_t get_point_capacity() const;
	void reserve_space(int64_t p_num_nodes);
	void clear();

	int64_t get_closest_point(const Vector3 &p_point, bool p_include_disabled = false) const;
	Vector3 get_closest_position_in_segment(const Vector3 &p_point) const;

	Vector<Vector3> get_point_path(int64_t p_from_id, int64_t p_to_id, bool p_allow_partial_path = false);
	Vector<int64_t> get_id_path(int64_t p_from_id, int64_t p_to_id, bool p_allow_partial_path = false);

	~AStar3D();
};

class AStar2D : public RefCounted {
	ORBLIT_CLASS(AStar2D, RefCounted);
	AStar3D astar;

	bool _solve(AStar3D::Point *p_begin_point, AStar3D::Point *p_end_point, bool p_allow_partial_path);

protected:
	static void register_members();

	virtual real_t _estimate_cost(int64_t p_from_id, int64_t p_end_id);
	virtual real_t _compute_cost(int64_t p_from_id, int64_t p_to_id);

	ORBLIT_VIRTUAL2RC(bool, _filter_neighbor, int64_t, int64_t)
	ORBLIT_VIRTUAL2RC(real_t, _estimate_cost, int64_t, int64_t)
	ORBLIT_VIRTUAL2RC(real_t, _compute_cost, int64_t, int64_t)

public:
	int64_t get_available_point_id() const;

	void add_point(int64_t p_id, const Vector2 &p_pos, real_t p_weight_scale = 1);
	Vector2 get_point_position(int64_t p_id) const;
	void set_point_position(int64_t p_id, const Vector2 &p_pos);
	real_t get_point_weight_scale(int64_t p_id) const;
	void set_point_weight_scale(int64_t p_id, real_t p_weight_scale);
	void remove_point(int64_t p_id);
	bool has_point(int64_t p_id) const;
	Vector<int64_t> get_point_connections(int64_t p_id);
	PackedInt64Array get_point_ids();

	bool is_neighbor_filter_enabled() const;
	void set_neighbor_filter_enabled(bool p_enabled);

	void set_point_disabled(int64_t p_id, bool p_disabled = true);
	bool is_point_disabled(int64_t p_id) const;

	void connect_points(int64_t p_id, int64_t p_with_id, bool p_bidirectional = true);
	void disconnect_points(int64_t p_id, int64_t p_with_id, bool p_bidirectional = true);
	bool are_points_connected(int64_t p_id, int64_t p_with_id, bool p_bidirectional = true) const;

	int64_t get_point_count() const;
	int64_t get_point_capacity() const;
	void reserve_space(int64_t p_num_nodes);
	void clear();

	int64_t get_closest_point(const Vector2 &p_point, bool p_include_disabled = false) const;
	Vector2 get_closest_position_in_segment(const Vector2 &p_point) const;

	Vector<Vector2> get_point_path(int64_t p_from_id, int64_t p_to_id, bool p_allow_partial_path = false);
	Vector<int64_t> get_id_path(int64_t p_from_id, int64_t p_to_id, bool p_allow_partial_path = false);
};
