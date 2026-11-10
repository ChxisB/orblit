/**************************************************************************/
/*  accessibility_system.cpp                                              */

#include "accessibility_system.h"

#include "core/object/class_registry.h"
#include "systems/display/accessibility_system_dummy.h"

AccessibilitySystem::AccessibilitySystemCreate AccessibilitySystem::system_create_functions[AccessibilitySystem::MAX_SYSTEMS] = {
	{ "dummy", &AccessibilitySystemDummy::create_func }
};

int AccessibilitySystem::system_create_count = 1;

void AccessibilitySystem::register_members() {
	ClassRegistry::bind_method(D_METHOD("is_supported"), &AccessibilitySystem::is_supported);

	ClassRegistry::bind_method(D_METHOD("create_element", "window_id", "role"), &AccessibilitySystem::create_element);
	ClassRegistry::bind_method(D_METHOD("create_sub_element", "parent_rid", "role", "insert_pos"), &AccessibilitySystem::create_sub_element, DEFVAL(-1));
	ClassRegistry::bind_method(D_METHOD("create_sub_text_edit_elements", "parent_rid", "shaped_text", "min_height", "insert_pos", "is_last_line"), &AccessibilitySystem::create_sub_text_edit_elements, DEFVAL(-1), DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("has_element", "id"), &AccessibilitySystem::has_element);
	ClassRegistry::bind_method(D_METHOD("free_element", "id"), &AccessibilitySystem::free_element);
	ClassRegistry::bind_method(D_METHOD("element_set_meta", "id", "meta"), &AccessibilitySystem::element_set_meta);
	ClassRegistry::bind_method(D_METHOD("element_get_meta", "id"), &AccessibilitySystem::element_get_meta);

	ClassRegistry::bind_method(D_METHOD("_update_if_active", "callback"), &AccessibilitySystem::update_if_active);

	ClassRegistry::bind_method(D_METHOD("set_window_rect", "window_id", "rect_out", "rect_in"), &AccessibilitySystem::set_window_rect);
	ClassRegistry::bind_method(D_METHOD("set_window_focused", "window_id", "focused"), &AccessibilitySystem::set_window_focused);

	ClassRegistry::bind_method(D_METHOD("update_set_focus", "id"), &AccessibilitySystem::update_set_focus);
	ClassRegistry::bind_method(D_METHOD("get_window_root", "window_id"), &AccessibilitySystem::get_window_root);
	ClassRegistry::bind_method(D_METHOD("update_set_role", "id", "role"), &AccessibilitySystem::update_set_role);
	ClassRegistry::bind_method(D_METHOD("update_set_name", "id", "name"), &AccessibilitySystem::update_set_name);
	ClassRegistry::bind_method(D_METHOD("update_set_braille_label", "id", "name"), &AccessibilitySystem::update_set_braille_label);
	ClassRegistry::bind_method(D_METHOD("update_set_braille_role_description", "id", "description"), &AccessibilitySystem::update_set_braille_role_description);
	ClassRegistry::bind_method(D_METHOD("update_set_extra_info", "id", "name"), &AccessibilitySystem::update_set_extra_info);
	ClassRegistry::bind_method(D_METHOD("update_set_description", "id", "description"), &AccessibilitySystem::update_set_description);
	ClassRegistry::bind_method(D_METHOD("update_set_value", "id", "value"), &AccessibilitySystem::update_set_value);
	ClassRegistry::bind_method(D_METHOD("update_set_tooltip", "id", "tooltip"), &AccessibilitySystem::update_set_tooltip);
	ClassRegistry::bind_method(D_METHOD("update_set_bounds", "id", "rect"), &AccessibilitySystem::update_set_bounds);
	ClassRegistry::bind_method(D_METHOD("update_set_transform", "id", "transform"), &AccessibilitySystem::update_set_transform);
	ClassRegistry::bind_method(D_METHOD("update_add_child", "id", "child_id"), &AccessibilitySystem::update_add_child);
	ClassRegistry::bind_method(D_METHOD("update_add_related_controls", "id", "related_id"), &AccessibilitySystem::update_add_related_controls);
	ClassRegistry::bind_method(D_METHOD("update_add_related_details", "id", "related_id"), &AccessibilitySystem::update_add_related_details);
	ClassRegistry::bind_method(D_METHOD("update_add_related_described_by", "id", "related_id"), &AccessibilitySystem::update_add_related_described_by);
	ClassRegistry::bind_method(D_METHOD("update_add_related_flow_to", "id", "related_id"), &AccessibilitySystem::update_add_related_flow_to);
	ClassRegistry::bind_method(D_METHOD("update_add_related_labeled_by", "id", "related_id"), &AccessibilitySystem::update_add_related_labeled_by);
	ClassRegistry::bind_method(D_METHOD("update_add_related_radio_group", "id", "related_id"), &AccessibilitySystem::update_add_related_radio_group);
	ClassRegistry::bind_method(D_METHOD("update_set_active_descendant", "id", "other_id"), &AccessibilitySystem::update_set_active_descendant);
	ClassRegistry::bind_method(D_METHOD("update_set_next_on_line", "id", "other_id"), &AccessibilitySystem::update_set_next_on_line);
	ClassRegistry::bind_method(D_METHOD("update_set_previous_on_line", "id", "other_id"), &AccessibilitySystem::update_set_previous_on_line);
	ClassRegistry::bind_method(D_METHOD("update_set_member_of", "id", "group_id"), &AccessibilitySystem::update_set_member_of);
	ClassRegistry::bind_method(D_METHOD("update_set_in_page_link_target", "id", "other_id"), &AccessibilitySystem::update_set_in_page_link_target);
	ClassRegistry::bind_method(D_METHOD("update_set_error_message", "id", "other_id"), &AccessibilitySystem::update_set_error_message);
	ClassRegistry::bind_method(D_METHOD("update_set_live", "id", "live"), &AccessibilitySystem::update_set_live);
	ClassRegistry::bind_method(D_METHOD("update_add_action", "id", "action", "callable"), &AccessibilitySystem::update_add_action);
	ClassRegistry::bind_method(D_METHOD("update_add_custom_action", "id", "action_id", "action_description"), &AccessibilitySystem::update_add_custom_action);
	ClassRegistry::bind_method(D_METHOD("update_set_table_row_count", "id", "count"), &AccessibilitySystem::update_set_table_row_count);
	ClassRegistry::bind_method(D_METHOD("update_set_table_column_count", "id", "count"), &AccessibilitySystem::update_set_table_column_count);
	ClassRegistry::bind_method(D_METHOD("update_set_table_row_index", "id", "index"), &AccessibilitySystem::update_set_table_row_index);
	ClassRegistry::bind_method(D_METHOD("update_set_table_column_index", "id", "index"), &AccessibilitySystem::update_set_table_column_index);
	ClassRegistry::bind_method(D_METHOD("update_set_table_cell_position", "id", "row_index", "column_index"), &AccessibilitySystem::update_set_table_cell_position);
	ClassRegistry::bind_method(D_METHOD("update_set_table_cell_span", "id", "row_span", "column_span"), &AccessibilitySystem::update_set_table_cell_span);
	ClassRegistry::bind_method(D_METHOD("update_set_list_item_count", "id", "size"), &AccessibilitySystem::update_set_list_item_count);
	ClassRegistry::bind_method(D_METHOD("update_set_list_item_index", "id", "index"), &AccessibilitySystem::update_set_list_item_index);
	ClassRegistry::bind_method(D_METHOD("update_set_list_item_level", "id", "level"), &AccessibilitySystem::update_set_list_item_level);
	ClassRegistry::bind_method(D_METHOD("update_set_list_item_selected", "id", "selected"), &AccessibilitySystem::update_set_list_item_selected);
	ClassRegistry::bind_method(D_METHOD("update_set_list_item_expanded", "id", "expanded"), &AccessibilitySystem::update_set_list_item_expanded);
	ClassRegistry::bind_method(D_METHOD("update_set_popup_type", "id", "popup"), &AccessibilitySystem::update_set_popup_type);
	ClassRegistry::bind_method(D_METHOD("update_set_checked", "id", "checekd"), &AccessibilitySystem::update_set_checked);
	ClassRegistry::bind_method(D_METHOD("update_set_num_value", "id", "position"), &AccessibilitySystem::update_set_num_value);
	ClassRegistry::bind_method(D_METHOD("update_set_num_range", "id", "min", "max"), &AccessibilitySystem::update_set_num_range);
	ClassRegistry::bind_method(D_METHOD("update_set_num_step", "id", "step"), &AccessibilitySystem::update_set_num_step);
	ClassRegistry::bind_method(D_METHOD("update_set_num_jump", "id", "jump"), &AccessibilitySystem::update_set_num_jump);
	ClassRegistry::bind_method(D_METHOD("update_set_scroll_x", "id", "position"), &AccessibilitySystem::update_set_scroll_x);
	ClassRegistry::bind_method(D_METHOD("update_set_scroll_x_range", "id", "min", "max"), &AccessibilitySystem::update_set_scroll_x_range);
	ClassRegistry::bind_method(D_METHOD("update_set_scroll_y", "id", "position"), &AccessibilitySystem::update_set_scroll_y);
	ClassRegistry::bind_method(D_METHOD("update_set_scroll_y_range", "id", "min", "max"), &AccessibilitySystem::update_set_scroll_y_range);
	ClassRegistry::bind_method(D_METHOD("update_set_text_decorations", "id", "underline", "strikethrough", "overline", "color"), &AccessibilitySystem::update_set_text_decorations, DEFVAL(Color(0, 0, 0, 1)));
	ClassRegistry::bind_method(D_METHOD("update_set_text_align", "id", "align"), &AccessibilitySystem::update_set_text_align);
	ClassRegistry::bind_method(D_METHOD("update_set_text_selection", "id", "text_start_id", "start_char", "text_end_id", "end_char"), &AccessibilitySystem::update_set_text_selection);
	ClassRegistry::bind_method(D_METHOD("update_set_flag", "id", "flag", "value"), &AccessibilitySystem::update_set_flag);
	ClassRegistry::bind_method(D_METHOD("update_set_classname", "id", "classname"), &AccessibilitySystem::update_set_classname);
	ClassRegistry::bind_method(D_METHOD("update_set_placeholder", "id", "placeholder"), &AccessibilitySystem::update_set_placeholder);
	ClassRegistry::bind_method(D_METHOD("update_set_language", "id", "language"), &AccessibilitySystem::update_set_language);
	ClassRegistry::bind_method(D_METHOD("update_set_text_orientation", "id", "vertical"), &AccessibilitySystem::update_set_text_orientation);
	ClassRegistry::bind_method(D_METHOD("update_set_list_orientation", "id", "vertical"), &AccessibilitySystem::update_set_list_orientation);
	ClassRegistry::bind_method(D_METHOD("update_set_shortcut", "id", "shortcut"), &AccessibilitySystem::update_set_shortcut);
	ClassRegistry::bind_method(D_METHOD("update_set_url", "id", "url"), &AccessibilitySystem::update_set_url);
	ClassRegistry::bind_method(D_METHOD("update_set_role_description", "id", "description"), &AccessibilitySystem::update_set_role_description);
	ClassRegistry::bind_method(D_METHOD("update_set_state_description", "id", "description"), &AccessibilitySystem::update_set_state_description);
	ClassRegistry::bind_method(D_METHOD("update_set_color_value", "id", "color"), &AccessibilitySystem::update_set_color_value);
	ClassRegistry::bind_method(D_METHOD("update_set_background_color", "id", "color"), &AccessibilitySystem::update_set_background_color);
	ClassRegistry::bind_method(D_METHOD("update_set_foreground_color", "id", "color"), &AccessibilitySystem::update_set_foreground_color);

	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_UNKNOWN);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_DEFAULT_BUTTON);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_AUDIO);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_VIDEO);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_STATIC_TEXT);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_CONTAINER);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_PANEL);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_BUTTON);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_LINK);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_CHECK_BOX);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_RADIO_BUTTON);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_CHECK_BUTTON);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_SCROLL_BAR);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_SCROLL_VIEW);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_SPLITTER);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_SLIDER);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_SPIN_BUTTON);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_PROGRESS_INDICATOR);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_TEXT_FIELD);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_MULTILINE_TEXT_FIELD);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_COLOR_PICKER);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_TABLE);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_CELL);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_ROW);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_ROW_GROUP);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_ROW_HEADER);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_COLUMN_HEADER);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_TREE);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_TREE_ITEM);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_LIST);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_LIST_ITEM);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_LIST_BOX);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_LIST_BOX_OPTION);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_TAB_BAR);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_TAB);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_TAB_PANEL);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_MENU_BAR);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_MENU);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_MENU_ITEM);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_MENU_ITEM_CHECK_BOX);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_MENU_ITEM_RADIO);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_IMAGE);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_WINDOW);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_TITLE_BAR);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_DIALOG);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_TOOLTIP);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_REGION);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ROLE_TEXT_RUN);

	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::POPUP_MENU);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::POPUP_LIST);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::POPUP_TREE);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::POPUP_DIALOG);

	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::FLAG_HIDDEN);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::FLAG_MULTISELECTABLE);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::FLAG_REQUIRED);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::FLAG_VISITED);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::FLAG_BUSY);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::FLAG_MODAL);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::FLAG_TOUCH_PASSTHROUGH);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::FLAG_READONLY);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::FLAG_DISABLED);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::FLAG_CLIPS_CHILDREN);

	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_CLICK);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_FOCUS);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_BLUR);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_COLLAPSE);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_EXPAND);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_DECREMENT);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_INCREMENT);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_HIDE_TOOLTIP);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SHOW_TOOLTIP);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SET_TEXT_SELECTION);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_REPLACE_SELECTED_TEXT);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SCROLL_BACKWARD);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SCROLL_DOWN);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SCROLL_FORWARD);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SCROLL_LEFT);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SCROLL_RIGHT);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SCROLL_UP);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SCROLL_INTO_VIEW);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SCROLL_TO_POINT);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SET_SCROLL_OFFSET);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SET_VALUE);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_SHOW_CONTEXT_MENU);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::ACTION_CUSTOM);

	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::LIVE_OFF);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::LIVE_POLITE);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::LIVE_ASSERTIVE);

	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::SCROLL_UNIT_ITEM);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::SCROLL_UNIT_PAGE);

	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::SCROLL_HINT_TOP_LEFT);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::SCROLL_HINT_BOTTOM_RIGHT);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::SCROLL_HINT_TOP_EDGE);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::SCROLL_HINT_BOTTOM_EDGE);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::SCROLL_HINT_LEFT_EDGE);
	BIND_ENUM_CONSTANT(AccessibilitySystemEnums::SCROLL_HINT_RIGHT_EDGE);
}

AccessibilitySystem *AccessibilitySystem::create(int p_index, Error &r_error) {
	ERR_FAIL_INDEX_V(p_index, system_create_count, nullptr);
	return system_create_functions[p_index].create_function(r_error);
}

void AccessibilitySystem::register_create_function(const char *p_name, CreateFunction p_function) {
	ERR_FAIL_COND(system_create_count == MAX_SYSTEMS);
	// Dummy server is always last
	system_create_functions[system_create_count] = system_create_functions[system_create_count - 1];
	system_create_functions[system_create_count - 1].name = p_name;
	system_create_functions[system_create_count - 1].create_function = p_function;
	system_create_count++;
}

int AccessibilitySystem::get_create_function_count() {
	return system_create_count;
}

const char *AccessibilitySystem::get_create_function_name(int p_index) {
	ERR_FAIL_INDEX_V(p_index, system_create_count, nullptr);
	return system_create_functions[p_index].name;
}

AccessibilitySystem::AccessibilitySystem() {
	singleton = this;
}

AccessibilitySystem::~AccessibilitySystem() {
	singleton = nullptr;
}
