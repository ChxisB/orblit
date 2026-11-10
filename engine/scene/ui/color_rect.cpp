/**************************************************************************/
/*  color_rect.cpp                                                        */

#include "color_rect.h"

#include "core/object/class_registry.h"
#include "systems/display/accessibility_system.h"

void ColorRect::set_color(const Color &p_color) {
	if (color == p_color) {
		return;
	}
	color = p_color;
	queue_accessibility_update();
	queue_redraw();
}

Color ColorRect::get_color() const {
	return color;
}

void ColorRect::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ACCESSIBILITY_UPDATE: {
			RID ae = get_accessibility_element();
			ERR_FAIL_COND(ae.is_null());

			AccessibilitySystem::get_singleton()->update_set_color_value(ae, color);
		} break;

		case NOTIFICATION_DRAW: {
			draw_rect(Rect2(Point2(), get_size()), color);
		} break;
	}
}

void ColorRect::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_color", "color"), &ColorRect::set_color);
	ClassRegistry::bind_method(D_METHOD("get_color"), &ColorRect::get_color);

	ADD_PROPERTY(PropertyInfo(Variant::COLOR, "color"), "set_color", "get_color");
}
