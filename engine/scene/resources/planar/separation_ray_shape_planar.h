/**************************************************************************/
/*  separation_ray_shape_planar.h                                             */

#pragma once

#include "scene/resources/planar/shape_planar.h"

class SeparationRayShape2D : public Shape2D {
	ORBLIT_CLASS(SeparationRayShape2D, Shape2D);

	real_t length = 20.0;
	bool slide_on_slope = false;

	void _update_shape();

protected:
	static void register_members();

public:
	void set_length(real_t p_length);
	real_t get_length() const;

	void set_slide_on_slope(bool p_active);
	bool get_slide_on_slope() const;

	virtual void draw(const RID &p_to_rid, const Color &p_color) override;
	virtual Rect2 get_rect() const override;
	virtual real_t get_enclosing_radius() const override;

	SeparationRayShape2D();
};
