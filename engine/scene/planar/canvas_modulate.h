/**************************************************************************/
/*  canvas_modulate.h                                                     */

#pragma once

#include "scene/planar/entity_planar.h"

class CanvasModulate : public Entity2D {
	ORBLIT_CLASS(CanvasModulate, Entity2D);

	Color color = Color(1, 1, 1, 1);

	// CanvasModulate is in canvas-specific modulate group when both in canvas and visible in tree.
	// Exactly one CanvasModulate in each such non-empty group is active.
	bool is_in_canvas = false;
	bool was_visible_in_tree = false; // Relevant only when in canvas.
	bool is_active = false;

	void _on_in_canvas_visibility_changed(bool p_new_visibility);

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void set_color(const Color &p_color);
	Color get_color() const;

	PackedStringArray get_configuration_warnings() const override;

	CanvasModulate();
	~CanvasModulate();
};
