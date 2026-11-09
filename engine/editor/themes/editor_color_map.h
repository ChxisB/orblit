/**************************************************************************/
/*  editor_color_map.h                                                    */

#pragma once

#include "core/math/color.h"
#include "core/string/string_name.h"
#include "core/containers/hash_map.h"
#include "core/containers/hash_set.h"

// The default icon theme is designed to be used for a dark theme. This map stores
// Color values to convert to other colors for better readability on a light theme.
class EditorColorMap {
	// Orblit Color values are used to avoid the ambiguity of strings
	// (where "#ffffff", "fff", and "white" are all equivalent).
	static HashMap<Color, Color> color_conversion_map;
	// The names of the icons to never convert, even if one of their colors
	// are contained in the color map from above.
	static HashSet<StringName> color_conversion_exceptions;

public:
	static void add_conversion_color_pair(const String &p_from_color, const String &p_to_color);
	static void add_conversion_exception(const StringName &p_icon_name);

	static HashMap<Color, Color> &get_color_conversion_map() { return color_conversion_map; }
	static HashSet<StringName> &get_color_conversion_exceptions() { return color_conversion_exceptions; }

	static void create();
	static void finish();
};
