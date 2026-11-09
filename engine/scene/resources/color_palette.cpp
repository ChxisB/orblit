/**************************************************************************/
/*  color_palette.cpp                                                     */

#include "color_palette.h"

#include "core/object/class_registry.h"

void ColorPalette::set_colors(const PackedColorArray &p_colors) {
	colors = p_colors;
}

PackedColorArray ColorPalette::get_colors() const {
	return colors;
}

void ColorPalette::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_colors", "colors"), &ColorPalette::set_colors);
	ClassRegistry::bind_method(D_METHOD("get_colors"), &ColorPalette::get_colors);

	ADD_PROPERTY(PropertyInfo(Variant::PACKED_COLOR_ARRAY, "colors"), "set_colors", "get_colors");
}
