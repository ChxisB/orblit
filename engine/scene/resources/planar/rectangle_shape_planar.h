/**************************************************************************/
/*  rectangle_shape_planar.h                                                  */

#pragma once

#include "scene/resources/planar/shape_planar.h"

class RectangleShape2D : public Shape2D {
	ORBLIT_CLASS(RectangleShape2D, Shape2D);

	Size2 size;
	void _update_shape();

protected:
	static void register_members();

public:
	void set_size(const Size2 &p_size);
	Size2 get_size() const;

	virtual void draw(const RID &p_to_rid, const Color &p_color) override;
	virtual Rect2 get_rect() const override;
	virtual real_t get_enclosing_radius() const override;

	RectangleShape2D();
};
