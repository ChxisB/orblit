/**************************************************************************/
/*  concave_polygon_shape_planar.h                                            */

#pragma once

#include "scene/resources/planar/shape_planar.h"

class ConcavePolygonShape2D : public Shape2D {
	ORBLIT_CLASS(ConcavePolygonShape2D, Shape2D);

protected:
	static void register_members();

public:
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const override;

	void set_segments(const Vector<Vector2> &p_segments);
	Vector<Vector2> get_segments() const;

	virtual void draw(const RID &p_to_rid, const Color &p_color) override;
	virtual Rect2 get_rect() const override;
	virtual real_t get_enclosing_radius() const override;

	ConcavePolygonShape2D();
};
