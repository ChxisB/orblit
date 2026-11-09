/**************************************************************************/
/*  segment_shape_planar.h                                                    */

#pragma once

#include "scene/resources/planar/shape_planar.h"

class SegmentShape2D : public Shape2D {
	ORBLIT_CLASS(SegmentShape2D, Shape2D);

	Vector2 a;
	Vector2 b;

	void _update_shape();

protected:
	static void register_members();

public:
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const override;

	void set_a(const Vector2 &p_a);
	void set_b(const Vector2 &p_b);

	Vector2 get_a() const;
	Vector2 get_b() const;

	virtual void draw(const RID &p_to_rid, const Color &p_color) override;
	virtual Rect2 get_rect() const override;
	virtual real_t get_enclosing_radius() const override;

	SegmentShape2D();
};
