/**************************************************************************/
/*  marker_planar.cpp                                                         */

#include "marker_planar.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"

void Marker2D::_draw_cross() {
	const real_t extents = get_gizmo_extents();

	PackedVector2Array points = {
		Point2(+extents, 0),
		Point2(),
		Point2(),
		Point2(-extents, 0),
		Point2(0, +extents),
		Point2(),
		Point2(),
		Point2(0, -extents),
	};

	// Use the axis color which is brighter for the positive axis.
	// Use a darkened axis color for the negative axis.
	// This makes it possible to see in which direction the Marker3D node is rotated
	// (which can be important depending on how it's used).
	// Axis colors are taken from `axis_x_color` and `axis_y_color` (defined in `editor/themes/editor_theme_manager.cpp`).
	const Color color_x = Color(0.96, 0.20, 0.32);
	const Color color_y = Color(0.53, 0.84, 0.01);
	PackedColorArray colors = {
		color_x,
		color_x.darkened(0.5),
		color_y,
		color_y.darkened(0.5),
	};

	draw_multiline_colors(points, colors);
}

#ifdef DEBUG_ENABLED
Rect2 Marker2D::_edit_get_rect() const {
	real_t extents = get_gizmo_extents();
	return Rect2(Point2(-extents, -extents), Size2(extents * 2, extents * 2));
}

bool Marker2D::_edit_use_rect() const {
	return false;
}
#endif // DEBUG_ENABLED

void Marker2D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			queue_redraw();
		} break;

		case NOTIFICATION_DRAW: {
			if (!is_inside_stage()) {
				break;
			}
			if (Engine::get_singleton()->is_editor_hint()) {
				_draw_cross();
			}
		} break;
	}
}

void Marker2D::set_gizmo_extents(real_t p_extents) {
	gizmo_extents = p_extents;
	queue_redraw();
}

real_t Marker2D::get_gizmo_extents() const {
	return gizmo_extents;
}

void Marker2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_gizmo_extents", "extents"), &Marker2D::set_gizmo_extents);
	ClassRegistry::bind_method(D_METHOD("get_gizmo_extents"), &Marker2D::get_gizmo_extents);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "gizmo_extents", PROPERTY_HINT_RANGE, "0,1000,0.1,or_greater,suffix:px"), "set_gizmo_extents", "get_gizmo_extents");
}

Marker2D::Marker2D() {
	set_hide_clip_children(true);
}
