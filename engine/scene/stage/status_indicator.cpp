/**************************************************************************/
/*  status_indicator.cpp                                                  */

#include "status_indicator.h"

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "scene/ui/popup_menu.h"
#include "systems/display/display_system.h"

void StatusIndicator::_notification(int p_what) {
	ERR_MAIN_THREAD_GUARD;
#ifdef TOOLS_ENABLED
	if (is_part_of_edited_scene()) {
		return;
	}
#endif

	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			if (DisplaySystem::get_singleton()->has_feature(DisplaySystemEnums::FEATURE_STATUS_INDICATOR)) {
				if (visible && iid == DisplaySystemEnums::INVALID_INDICATOR_ID) {
					iid = DisplaySystem::get_singleton()->create_status_indicator(icon, tooltip, callable_mp(this, &StatusIndicator::_callback));
					PopupMenu *pm = Object::cast_to<PopupMenu>(get_entity_or_null(menu));
					if (pm) {
						RID menu_rid = pm->bind_global_menu();
						DisplaySystem::get_singleton()->status_indicator_set_menu(iid, menu_rid);
					}
				}
			}
		} break;
		case NOTIFICATION_EXIT_STAGE: {
			if (DisplaySystem::get_singleton()->has_feature(DisplaySystemEnums::FEATURE_STATUS_INDICATOR)) {
				if (iid != DisplaySystemEnums::INVALID_INDICATOR_ID) {
					PopupMenu *pm = Object::cast_to<PopupMenu>(get_entity_or_null(menu));
					if (pm) {
						pm->unbind_global_menu();
						DisplaySystem::get_singleton()->status_indicator_set_menu(iid, RID());
					}
					DisplaySystem::get_singleton()->delete_status_indicator(iid);
					iid = DisplaySystemEnums::INVALID_INDICATOR_ID;
				}
			}
		} break;
		default:
			break;
	}
}

void StatusIndicator::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_tooltip", "tooltip"), &StatusIndicator::set_tooltip);
	ClassRegistry::bind_method(D_METHOD("get_tooltip"), &StatusIndicator::get_tooltip);
	ClassRegistry::bind_method(D_METHOD("set_icon", "texture"), &StatusIndicator::set_icon);
	ClassRegistry::bind_method(D_METHOD("get_icon"), &StatusIndicator::get_icon);
	ClassRegistry::bind_method(D_METHOD("set_visible", "visible"), &StatusIndicator::set_visible);
	ClassRegistry::bind_method(D_METHOD("is_visible"), &StatusIndicator::is_visible);
	ClassRegistry::bind_method(D_METHOD("set_menu", "menu"), &StatusIndicator::set_menu);
	ClassRegistry::bind_method(D_METHOD("get_menu"), &StatusIndicator::get_menu);
	ClassRegistry::bind_method(D_METHOD("get_rect"), &StatusIndicator::get_rect);

	ADD_SIGNAL(MethodInfo("pressed", PropertyInfo(Variant::INT, "mouse_button"), PropertyInfo(Variant::VECTOR2I, "mouse_position")));

	ADD_PROPERTY(PropertyInfo(Variant::STRING, "tooltip", PROPERTY_HINT_MULTILINE_TEXT), "set_tooltip", "get_tooltip");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "icon", PROPERTY_HINT_RESOURCE_TYPE, Texture2D::get_static_class_name()), "set_icon", "get_icon");
	ADD_PROPERTY(PropertyInfo(Variant::ENTITY_PATH, "menu", PROPERTY_HINT_ENTITY_PATH_VALID_TYPES, "PopupMenu"), "set_menu", "get_menu");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "visible"), "set_visible", "is_visible");
}

void StatusIndicator::_callback(MouseButton p_index, const Point2i &p_pos) {
	emit_signal(SceneStringName(pressed), p_index, p_pos);
}

void StatusIndicator::set_icon(const Ref<Texture2D> &p_icon) {
	ERR_MAIN_THREAD_GUARD;
	icon = p_icon;
	if (iid != DisplaySystemEnums::INVALID_INDICATOR_ID) {
		DisplaySystem::get_singleton()->status_indicator_set_icon(iid, icon);
	}
}

Ref<Texture2D> StatusIndicator::get_icon() const {
	return icon;
}

void StatusIndicator::set_tooltip(const String &p_tooltip) {
	ERR_MAIN_THREAD_GUARD;
	tooltip = p_tooltip;
	if (iid != DisplaySystemEnums::INVALID_INDICATOR_ID) {
		DisplaySystem::get_singleton()->status_indicator_set_tooltip(iid, tooltip);
	}
}

String StatusIndicator::get_tooltip() const {
	return tooltip;
}

void StatusIndicator::set_menu(const EntityPath &p_menu) {
	PopupMenu *pm = Object::cast_to<PopupMenu>(get_entity_or_null(menu));
	if (pm) {
		pm->unbind_global_menu();
		if (iid != DisplaySystemEnums::INVALID_INDICATOR_ID) {
			DisplaySystem::get_singleton()->status_indicator_set_menu(iid, RID());
		}
	}

	menu = p_menu;

	pm = Object::cast_to<PopupMenu>(get_entity_or_null(menu));
	if (pm) {
		if (iid != DisplaySystemEnums::INVALID_INDICATOR_ID) {
			RID menu_rid = pm->bind_global_menu();
			DisplaySystem::get_singleton()->status_indicator_set_menu(iid, menu_rid);
		}
	}
}

EntityPath StatusIndicator::get_menu() const {
	return menu;
}

void StatusIndicator::set_visible(bool p_visible) {
	ERR_MAIN_THREAD_GUARD;
	if (visible == p_visible) {
		return;
	}
	visible = p_visible;

	if (!is_inside_stage()) {
		return;
	}

#ifdef TOOLS_ENABLED
	if (is_part_of_edited_scene()) {
		return;
	}
#endif

	if (DisplaySystem::get_singleton()->has_feature(DisplaySystemEnums::FEATURE_STATUS_INDICATOR)) {
		if (visible && iid == DisplaySystemEnums::INVALID_INDICATOR_ID) {
			iid = DisplaySystem::get_singleton()->create_status_indicator(icon, tooltip, callable_mp(this, &StatusIndicator::_callback));
			PopupMenu *pm = Object::cast_to<PopupMenu>(get_entity_or_null(menu));
			if (pm) {
				RID menu_rid = pm->bind_global_menu();
				DisplaySystem::get_singleton()->status_indicator_set_menu(iid, menu_rid);
			}
		}
		if (!visible && iid != DisplaySystemEnums::INVALID_INDICATOR_ID) {
			PopupMenu *pm = Object::cast_to<PopupMenu>(get_entity_or_null(menu));
			if (pm) {
				pm->unbind_global_menu();
				DisplaySystem::get_singleton()->status_indicator_set_menu(iid, RID());
			}
			DisplaySystem::get_singleton()->delete_status_indicator(iid);
			iid = DisplaySystemEnums::INVALID_INDICATOR_ID;
		}
	}
}

bool StatusIndicator::is_visible() const {
	return visible;
}

Rect2 StatusIndicator::get_rect() const {
	if (iid == DisplaySystemEnums::INVALID_INDICATOR_ID) {
		return Rect2();
	}
	return DisplaySystem::get_singleton()->status_indicator_get_rect(iid);
}
