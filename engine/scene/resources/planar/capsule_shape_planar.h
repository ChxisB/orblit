/**************************************************************************/
/*  capsule_shape_planar.h                                                    */

#pragma once

#include "scene/resources/planar/shape_planar.h"

class CapsuleShape2D : public Shape2D {
	ORBLIT_CLASS(CapsuleShape2D, Shape2D);

	real_t height = 30.0;
	real_t radius = 10.0;

	void _update_shape();
	Vector<Vector2> _get_points() const;

protected:
	static void register_members();

public:
	virtual bool _edit_is_selected_on_click(const Point2 &p_point, double p_tolerance) const override;

	void set_height(real_t p_height);
	real_t get_height() const;

	void set_radius(real_t p_radius);
	real_t get_radius() const;

	void set_mid_height(real_t p_mid_height);
	real_t get_mid_height() const;

	virtual void draw(const RID &p_to_rid, const Color &p_color) override;
	virtual Rect2 get_rect() const override;
	virtual real_t get_enclosing_radius() const override;

	CapsuleShape2D();
};
