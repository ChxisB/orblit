/**************************************************************************/
/*  color_rect.h                                                          */

#pragma once

#include "scene/ui/control.h"

class ColorRect : public Control {
	ORBLIT_CLASS(ColorRect, Control);

	Color color = Color(1, 1, 1);

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void set_color(const Color &p_color);
	Color get_color() const;
};
