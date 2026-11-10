/**************************************************************************/
/*  triangulate.h                                                         */

#pragma once

#include "core/containers/vector.h"
#include "core/math/vector2.h"

/*
https://www.flipcode.com/archives/Efficient_Polygon_Triangulation.shtml
*/

class Triangulate {
public:
	// triangulate a contour/polygon, places results in STL vector
	// as series of triangles.
	static bool triangulate(const Vector<Vector2> &p_contour, Vector<int> &r_result);

	// compute area of a contour/polygon
	static real_t get_area(const Vector<Vector2> &p_contour);

	// decide if point Px/Py is inside triangle defined by
	// (Ax,Ay) (Bx,By) (Cx,Cy)
	static bool is_inside_triangle(real_t p_ax, real_t p_ay, real_t p_bx, real_t p_by, real_t p_cx, real_t p_cy, real_t p_px, real_t p_py, bool p_include_edges);

private:
	static bool snip(const Vector<Vector2> &p_contour, int p_u, int p_v, int p_w, int p_n, const Vector<int> &p_values, bool p_relaxed);
};
