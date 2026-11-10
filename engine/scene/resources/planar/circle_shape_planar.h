/**************************************************************************/
/*  circle_shape_planar.h                                                     */

#pragma once

#include "scene/resources/planar/shape_planar.h"

class CircleShape2D : public Shape2D {
	ORBLIT_CLASS(CircleShape2D, Shape2D);

	real_t radius = 10.0;
	void _update_shape();

protected:
	static void register_members();

public:
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const override;

	void set_radius(real_t p_radius);
	real_t get_radius() const;

	virtual void draw(const RID &p_to_rid, const Color &p_color) override;
	virtual Rect2 get_rect() const override;
	virtual real_t get_enclosing_radius() const override;

	CircleShape2D();
};
