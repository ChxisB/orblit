/**************************************************************************/
/*  color_palette.h                                                       */

#pragma once

#include "core/io/resource.h"

class ColorPalette : public Resource {
	ORBLIT_CLASS(ColorPalette, Resource)

private:
	PackedColorArray colors;

protected:
	static void register_members();

public:
	void set_colors(const PackedColorArray &p_colors);
	PackedColorArray get_colors() const;
};
