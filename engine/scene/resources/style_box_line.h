/**************************************************************************/
/*  style_box_line.h                                                      */

#pragma once

#include "scene/resources/style_box.h"

class StyleBoxLine : public StyleBox {
	ORBLIT_CLASS(StyleBoxLine, StyleBox);
	Color color;
	int thickness = 1;
	bool vertical = false;
	float grow_begin = 1.0;
	float grow_end = 1.0;

protected:
	virtual float get_style_margin(Side p_side) const override;
	static void register_members();

public:
	void set_color(const Color &p_color);
	Color get_color() const;

	void set_thickness(int p_thickness);
	int get_thickness() const;

	void set_vertical(bool p_vertical);
	bool is_vertical() const;

	void set_grow_begin(float p_grow);
	float get_grow_begin() const;

	void set_grow_end(float p_grow);
	float get_grow_end() const;

	virtual void draw(RID p_canvas_item, const Rect2 &p_rect) const override;
};
