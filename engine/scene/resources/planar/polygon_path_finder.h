/**************************************************************************/
/*  polygon_path_finder.h                                                 */

#pragma once

#include "core/io/resource.h"

class PolygonPathFinder : public Resource {
	ORBLIT_CLASS(PolygonPathFinder, Resource);

	struct Point {
		Vector2 pos;
		HashSet<int> connections;
		float distance = 0.0;
		float penalty = 0.0;
		int prev = 0;
	};

	union Edge {
		struct {
			int32_t points[2];
		};
		uint64_t key = 0;

		_FORCE_INLINE_ bool operator==(const Edge &p_edge) const {
			return key == p_edge.key;
		}
		_FORCE_INLINE_ static uint32_t hash(const Edge &p_edge) {
			return hash_one_uint64(p_edge.key);
		}

		Edge(int a = 0, int b = 0) {
			if (a > b) {
				SWAP(a, b);
			}
			points[0] = a;
			points[1] = b;
		}
	};

	Vector2 outside_point;
	Rect2 bounds;

	Vector<Point> points;
	HashSet<Edge, Edge> edges;

	bool _is_point_inside(const Vector2 &p_point) const;

	void _set_data(const Dictionary &p_data);
	Dictionary _get_data() const;

protected:
	static void register_members();

public:
	void setup(const Vector<Vector2> &p_points, const Vector<int> &p_connections);
	Vector<Vector2> find_path(const Vector2 &p_from, const Vector2 &p_to);

	void set_point_penalty(int p_point, float p_penalty);
	float get_point_penalty(int p_point) const;

	bool is_point_inside(const Vector2 &p_point) const;
	Vector2 get_closest_point(const Vector2 &p_point) const;
	Vector<Vector2> get_intersections(const Vector2 &p_from, const Vector2 &p_to) const;
	Rect2 get_bounds() const;

	PolygonPathFinder();
};
