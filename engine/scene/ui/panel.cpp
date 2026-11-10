/**************************************************************************/
/*  panel.cpp                                                             */

#include "panel.h"

#include "scene/theme/theme_db.h"
#include "systems/display/accessibility_system.h"

void Panel::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ACCESSIBILITY_UPDATE: {
			RID ae = get_accessibility_element();
			ERR_FAIL_COND(ae.is_null());

			AccessibilitySystem::get_singleton()->update_set_role(ae, AccessibilitySystemEnums::AccessibilityRole::ROLE_PANEL);
		} break;

		case NOTIFICATION_DRAW: {
			RID ci = get_canvas_item();
			theme_cache.panel_style->draw(ci, Rect2(Point2(), get_size()));
		} break;
	}
}

void Panel::register_members() {
	BIND_THEME_ITEM_CUSTOM(Theme::DATA_TYPE_STYLEBOX, Panel, panel_style, "panel");
}

Panel::Panel() {
	// Has visible stylebox, so stop by default.
	set_mouse_filter(MOUSE_FILTER_STOP);
}
