/**************************************************************************/
/*  convex_polygon_shape_planar.h                                             */

#pragma once

#include "scene/resources/planar/shape_planar.h"

class ConvexPolygonShape2D : public Shape2D {
	ORBLIT_CLASS(ConvexPolygonShape2D, Shape2D);

	Vector<Vector2> points;
	void _update_shape();

protected:
	static void register_members();

public:
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const override;

	void set_point_cloud(const Vector<Vector2> &p_points);
	void set_points(const Vector<Vector2> &p_points);
	Vector<Vector2> get_points() const;

	virtual void draw(const RID &p_to_rid, const Color &p_color) override;
	virtual Rect2 get_rect() const override;
	virtual real_t get_enclosing_radius() const override;

	ConvexPolygonShape2D();
};
