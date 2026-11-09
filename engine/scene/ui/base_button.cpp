/**************************************************************************/
/*  base_button.cpp                                                       */

#include "base_button.h"

#include "core/config/project_settings.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "scene/ui/label.h"
#include "scene/stage/timer.h"
#include "scene/theme/theme_db.h"
#include "systems/display/accessibility_system.h"

void BaseButton::_unpress_group() {
	if (button_group.is_null()) {
		return;
	}

	if (toggle_mode && !button_group->is_allow_unpress()) {
		status.pressed = true;
		queue_accessibility_update();
	}

	for (BaseButton *E : button_group->buttons) {
		if (E == this) {
			continue;
		}

		E->set_pressed(false);
	}
}

void BaseButton::gui_input(const Ref<InputEvent> &p_event) {
	ERR_FAIL_COND(p_event.is_null());

	if (p_event->get_device() == InputEvent::DEVICE_ID_EMULATION) {
		return;
	}

	Ref<InputEventScreenTouch> touch = p_event;
	if (touch.is_valid()) {
		if (status.touch_index == -1) {
			if (touch->is_pressed()) {
				status.touch_index = touch->get_index();
				status.press_attempt = true;
				status.pressing_inside = has_point(touch->get_position()) && get_viewport_transform().xform(get_viewport_rect()).has_point(touch->get_position());
				on_action_event(p_event);
			}
		} else if (touch->get_index() == status.touch_index) {
			if (!touch->is_pressed()) {
				status.touch_index = -1;
				on_action_event(p_event);
			}
		}
	}

	Ref<InputEventScreenDrag> drag = p_event;
	if (drag.is_valid() && drag->get_index() == status.touch_index && status.press_attempt) {
		bool last_press_inside = status.pressing_inside;
		status.pressing_inside = has_point(drag->get_position()) && get_viewport_transform().xform(get_viewport_rect()).has_point(drag->get_position());

		if (last_press_inside != status.pressing_inside) {
			queue_redraw();
		}
	}

	Ref<InputEventMouseButton> mouse_button = p_event;
	bool ui_accept = p_event->is_action("ui_accept", true) && !p_event->is_echo();

	bool button_masked = mouse_button.is_valid() && button_mask.has_flag(mouse_button_to_mask(mouse_button->get_button_index()));
	if (button_masked || ui_accept) {
		was_mouse_pressed = button_masked;
		on_action_event(p_event);
		was_mouse_pressed = false;
	} else {
		Ref<InputEventMouseMotion> mouse_motion = p_event;
		if (mouse_motion.is_valid()) {
			if (status.press_attempt) {
				bool last_press_inside = status.pressing_inside;
				status.pressing_inside = has_point(mouse_motion->get_position());
				if (last_press_inside != status.pressing_inside) {
					queue_redraw();
				}
			}
		}
	}
}

void BaseButton::_accessibility_action_click(const Variant &p_data) {
	press();
}

void BaseButton::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ACCESSIBILITY_UPDATE: {
			RID ae = get_accessibility_element();
			ERR_FAIL_COND(ae.is_null());

			AccessibilitySystem::get_singleton()->update_set_role(ae, AccessibilitySystemEnums::AccessibilityRole::ROLE_BUTTON);

			AccessibilitySystem::get_singleton()->update_add_action(ae, AccessibilitySystemEnums::AccessibilityAction::ACTION_CLICK, callable_mp(this, &BaseButton::_accessibility_action_click));
			AccessibilitySystem::get_singleton()->update_set_flag(ae, AccessibilitySystemEnums::AccessibilityFlags::FLAG_DISABLED, status.disabled);
			if (toggle_mode) {
				AccessibilitySystem::get_singleton()->update_set_checked(ae, status.pressed);
			}
			if (button_group.is_valid()) {
				for (const BaseButton *btn : button_group->buttons) {
					if (btn->is_part_of_edited_scene()) {
						continue;
					}
					AccessibilitySystem::get_singleton()->update_add_related_radio_group(ae, btn->get_accessibility_element());
				}
			}
			if (shortcut_in_tooltip && shortcut.is_valid() && shortcut->has_valid_event()) {
				String text = atr(shortcut->get_name()) + " (" + shortcut->get_as_text() + ")";
				String tooltip = get_tooltip_text();
				if (!tooltip.is_empty() && shortcut->get_name().nocasecmp_to(tooltip) != 0) {
					text += "\n" + atr(tooltip);
				}
				AccessibilitySystem::get_singleton()->update_set_tooltip(ae, text);
			}
		} break;

		case NOTIFICATION_MOUSE_ENTER: {
			status.hovering = true;
			if (!status.disabled) {
				play_theme_sound(theme_cache.hover_sound);
			}
			queue_accessibility_update();
			queue_redraw();
		} break;

		case NOTIFICATION_MOUSE_EXIT: {
			status.hovering = false;
			queue_accessibility_update();
			queue_redraw();
		} break;

		case NOTIFICATION_DRAG_BEGIN:
		case NOTIFICATION_SCROLL_BEGIN: {
			if (status.press_attempt) {
				status.press_attempt = false;
				status.touch_index = -1;
				queue_redraw();
			}
		} break;

		case NOTIFICATION_FOCUS_ENTER: {
			queue_redraw();
		} break;

		case NOTIFICATION_FOCUS_EXIT: {
			if (status.press_attempt) {
				status.press_attempt = false;
				status.touch_index = -1;
				queue_redraw();
			} else if (status.hovering) {
				queue_redraw();
			}

			if (status.pressed_down_with_focus) {
				status.pressed_down_with_focus = false;
				emit_signal(SNAME("button_up"));
			}
		} break;

		case NOTIFICATION_VISIBILITY_CHANGED:
		case NOTIFICATION_EXIT_STAGE: {
			if (p_what == NOTIFICATION_VISIBILITY_CHANGED && is_visible_in_stage()) {
				break;
			}
			if (!toggle_mode) {
				status.pressed = false;
			}
			status.hovering = false;
			status.press_attempt = false;
			status.pressing_inside = false;
			status.touch_index = -1;
		} break;
	}
}

void BaseButton::_pressed() {
	if (status.disabled) {
		play_theme_sound(theme_cache.pressed_disabled_sound);
		return;
	}

	play_theme_sound(theme_cache.pressed_sound);

	ORBLIT_VIRTUAL_CALL(_pressed);
	pressed();
	emit_signal(SceneStringName(pressed));
}

void BaseButton::_toggled(bool p_pressed) {
	if (status.disabled) {
		return;
	}

	ORBLIT_VIRTUAL_CALL(_toggled, p_pressed);
	toggled(p_pressed);
	emit_signal(SceneStringName(toggled), p_pressed);
}

void BaseButton::on_action_event(Ref<InputEvent> p_event) {
	Ref<InputEventMouseButton> mouse_button = p_event;
	Ref<InputEventScreenTouch> screen_touch = p_event;
	Ref<InputEventScreenDrag> screen_drag = p_event;

	bool is_accept_event = mouse_button.is_null() && screen_touch.is_null() && screen_drag.is_null();
	bool is_touch_press_inside = screen_touch.is_valid() && screen_touch->is_pressed() && status.pressing_inside;
	if (p_event->is_pressed() && (is_accept_event || status.hovering || is_touch_press_inside)) {
		status.press_attempt = true;
		status.pressing_inside = true;
		if (!status.pressed_down_with_focus) {
			status.pressed_down_with_focus = true;
			if (!status.disabled) {
				emit_signal(SNAME("button_down"));
			}
		}
	}

	if (status.press_attempt && status.pressing_inside) {
		if (toggle_mode) {
			if ((p_event->is_pressed() && action_mode == ACTION_MODE_BUTTON_PRESS) || (p_event->is_released() && action_mode == ACTION_MODE_BUTTON_RELEASE)) {
				if (action_mode == ACTION_MODE_BUTTON_PRESS) {
					status.press_attempt = false;
					status.pressing_inside = false;
					status.touch_index = -1; // Action completed, release matching touch so later taps aren't dropped if a modal consumes the release.
				}

				if (!status.disabled) {
					status.pressed = !status.pressed;

					_unpress_group();
					if (button_group.is_valid()) {
						button_group->emit_signal(SceneStringName(pressed), this);
					}
				}

				_toggled(status.pressed);
				_pressed();
			}
		} else {
			if ((p_event->is_pressed() && action_mode == ACTION_MODE_BUTTON_PRESS) || (p_event->is_released() && action_mode == ACTION_MODE_BUTTON_RELEASE)) {
				_pressed();
			}
		}
	}

	if (!p_event->is_pressed()) {
		status.press_attempt = false;
		status.pressing_inside = false;
		if (status.pressed_down_with_focus) {
			status.pressed_down_with_focus = false;
			if (!status.disabled) {
				emit_signal(SNAME("button_up"));
			}
		}
	}

	queue_accessibility_update();
	queue_redraw();
}

void BaseButton::pressed() {
}

void BaseButton::toggled(bool p_pressed) {
}

void BaseButton::set_disabled(bool p_disabled) {
	if (status.disabled == p_disabled) {
		return;
	}

	status.disabled = p_disabled;
	if (p_disabled) {
		if (!toggle_mode) {
			status.pressed = false;
		}
		status.press_attempt = false;
		status.pressing_inside = false;
		if (status.pressed_down_with_focus) {
			status.pressed_down_with_focus = false;
			emit_signal(SNAME("button_up"));
		}
	}
	queue_accessibility_update();
	queue_redraw();
	update_minimum_size();
}

bool BaseButton::is_disabled() const {
	return status.disabled;
}

void BaseButton::press() {
	if (is_disabled()) {
		return;
	}

	if (toggle_mode) {
		status.pressed = !status.pressed;

		_unpress_group();
		if (button_group.is_valid()) {
			button_group->emit_signal(SceneStringName(pressed), this);
		}

		_toggled(status.pressed);
		_pressed();
	} else {
		_pressed();
	}

	queue_accessibility_update();
	queue_redraw();
}

void BaseButton::set_pressed(bool p_pressed) {
	bool prev_pressed = status.pressed;
	set_pressed_no_signal(p_pressed);

	if (status.pressed == prev_pressed) {
		return;
	}

	if (p_pressed) {
		_unpress_group();
		if (button_group.is_valid()) {
			button_group->emit_signal(SceneStringName(pressed), this);
		}
	}
	_toggled(status.pressed);
}

void BaseButton::set_pressed_no_signal(bool p_pressed) {
	if (!toggle_mode) {
		return;
	}
	if (status.pressed == p_pressed) {
		return;
	}
	status.pressed = p_pressed;
	queue_accessibility_update();
	queue_redraw();
}

bool BaseButton::is_pressing() const {
	return status.press_attempt;
}

bool BaseButton::is_pressed() const {
	return toggle_mode ? status.pressed : status.press_attempt;
}

bool BaseButton::is_hovered() const {
	return status.hovering;
}

BaseButton::DrawMode BaseButton::get_draw_mode() const {
	if (status.disabled) {
		return DRAW_DISABLED;
	}

	if (in_shortcut_feedback) {
		return DRAW_HOVER_PRESSED;
	}

	if (!status.press_attempt && status.hovering) {
		if (status.pressed) {
			return DRAW_HOVER_PRESSED;
		}

		return DRAW_HOVER;
	} else {
		// Determine if pressed or not.
		bool pressing;
		if (status.press_attempt) {
			pressing = (status.pressing_inside || keep_pressed_outside);
			if (status.pressed) {
				pressing = !pressing;
			}
		} else {
			pressing = status.pressed;
		}

		if (pressing) {
			return DRAW_PRESSED;
		} else {
			return DRAW_NORMAL;
		}
	}
}

bool BaseButton::has_point(const Point2 &p_point) const {
	ERR_READ_THREAD_GUARD_V(false);
	bool ret;
	if (ORBLIT_VIRTUAL_CALL(_has_point, p_point, ret)) {
		return ret;
	}
	Rect2 rect = Rect2(Point2(), get_size()).grow(theme_cache.click_margin);
	return rect.has_area() && rect.has_point(p_point);
}

void BaseButton::set_toggle_mode(bool p_on) {
	// Make sure to set 'pressed' to false if we are not in toggle mode
	if (!p_on) {
		set_pressed(false);
	}
	queue_accessibility_update();

	toggle_mode = p_on;
	update_configuration_warnings();
}

bool BaseButton::is_toggle_mode() const {
	return toggle_mode;
}

void BaseButton::set_shortcut_in_tooltip(bool p_on) {
	if (shortcut_in_tooltip != p_on) {
		shortcut_in_tooltip = p_on;
		queue_accessibility_update();
	}
}

bool BaseButton::is_shortcut_in_tooltip_enabled() const {
	return shortcut_in_tooltip;
}

void BaseButton::set_action_mode(ActionMode p_mode) {
	action_mode = p_mode;
}

BaseButton::ActionMode BaseButton::get_action_mode() const {
	return action_mode;
}

void BaseButton::set_button_mask(BitField<MouseButtonMask> p_mask) {
	button_mask = p_mask;
}

BitField<MouseButtonMask> BaseButton::get_button_mask() const {
	return button_mask;
}

void BaseButton::set_keep_pressed_outside(bool p_on) {
	keep_pressed_outside = p_on;
}

bool BaseButton::is_keep_pressed_outside() const {
	return keep_pressed_outside;
}

void BaseButton::set_shortcut_feedback(bool p_enable) {
	shortcut_feedback = p_enable;
}

bool BaseButton::is_shortcut_feedback() const {
	return shortcut_feedback;
}

void BaseButton::set_shortcut(const Ref<Shortcut> &p_shortcut) {
	if (shortcut != p_shortcut) {
		shortcut = p_shortcut;
		set_shortcut_input_enabled(shortcut.is_valid());
		queue_accessibility_update();
	}
}

Ref<Shortcut> BaseButton::get_shortcut() const {
	return shortcut;
}

void BaseButton::_shortcut_feedback_timeout() {
	in_shortcut_feedback = false;
	queue_redraw();
}

void BaseButton::shortcut_input(const Ref<InputEvent> &p_event) {
	ERR_FAIL_COND(p_event.is_null());

	if (p_event->is_pressed() && is_visible_in_stage() && !p_event->is_echo() && shortcut.is_valid() && shortcut->matches_event(p_event)) {
		if (is_disabled()) {
			// Play the disabled sound here, as `press()` early returns if the button is disabled
			// (which causes the `play_theme_sound()` call in `_pressed()` to not be called).
			// This is not needed for the non-disabled press sound.
			play_theme_sound(theme_cache.pressed_disabled_sound);
		}

		press();
		accept_event();

		if (shortcut_feedback && is_inside_stage()) {
			if (shortcut_feedback_timer == nullptr) {
				shortcut_feedback_timer = memnew(Timer);
				shortcut_feedback_timer->set_one_shot(true);
				shortcut_feedback_timer->set_ignore_time_scale(true);
				add_child(shortcut_feedback_timer, false, INTERNAL_MODE_BACK);
				shortcut_feedback_timer->set_wait_time(GLOBAL_GET_CACHED(double, "gui/timers/button_shortcut_feedback_highlight_time"));
				shortcut_feedback_timer->connect("timeout", callable_mp(this, &BaseButton::_shortcut_feedback_timeout));
			}

			in_shortcut_feedback = true;
			shortcut_feedback_timer->start();
		}
	}
}

Control *BaseButton::make_custom_tooltip(const String &p_text) const {
	Control *control = Control::make_custom_tooltip(p_text);
	if (control) {
		return control;
	}

	if (!shortcut_in_tooltip || shortcut.is_null()) {
		return nullptr; // Use the default tooltip label.
	}

	bool shortcut_has_events = shortcut->has_valid_event();
	if (!shortcut_has_events && shortcut->get_name().is_empty()) {
		return nullptr;
	}

	String text = atr(shortcut->get_name());
	if (shortcut_has_events) {
		text += " (" + shortcut->get_as_text() + ")";
	}

	if (!p_text.is_empty() && shortcut->get_name().nocasecmp_to(p_text) != 0) {
		text += "\n" + atr(p_text);
	}

	// Make a label similar to the default tooltip label.
	// Auto translation is disabled because we already did that manually above.
	//
	// We can't customize the tooltip text by overriding `get_tooltip()`
	// because otherwise user-defined `_make_custom_tooltip()` would receive
	// the translated and annotated text.
	Label *label = memnew(Label(text));
	label->set_auto_translate_mode(AUTO_TRANSLATE_MODE_DISABLED);
	label->set_theme_type_variation(SNAME("TooltipLabel"));

	return label;
}

void BaseButton::set_button_group(const Ref<ButtonGroup> &p_group) {
	if (button_group.is_valid()) {
		button_group->buttons.erase(this);
	}

	button_group = p_group;

	if (button_group.is_valid()) {
		button_group->buttons.insert(this);
	}

	queue_accessibility_update();
	queue_redraw(); //checkbox changes to radio if set a buttongroup
	update_configuration_warnings();
}

Ref<ButtonGroup> BaseButton::get_button_group() const {
	return button_group;
}

bool BaseButton::_was_pressed_by_mouse() const {
	return was_mouse_pressed;
}

PackedStringArray BaseButton::get_configuration_warnings() const {
	PackedStringArray warnings = Control::get_configuration_warnings();

	if (get_button_group().is_valid() && !is_toggle_mode()) {
		warnings.push_back(RTR("ButtonGroup is intended to be used only with buttons that have toggle_mode set to true."));
	}

	return warnings;
}

void BaseButton::register_members() {
	ClassRegistry::bind_method(D_METHOD("press"), &BaseButton::press);
	ClassRegistry::bind_method(D_METHOD("set_pressed", "pressed"), &BaseButton::set_pressed);
	ClassRegistry::bind_method(D_METHOD("set_pressed_no_signal", "pressed"), &BaseButton::set_pressed_no_signal);

	ClassRegistry::bind_method(D_METHOD("is_pressed"), &BaseButton::is_pressed);
	ClassRegistry::bind_method(D_METHOD("is_hovered"), &BaseButton::is_hovered);

	ClassRegistry::bind_method(D_METHOD("set_toggle_mode", "enabled"), &BaseButton::set_toggle_mode);
	ClassRegistry::bind_method(D_METHOD("is_toggle_mode"), &BaseButton::is_toggle_mode);

	ClassRegistry::bind_method(D_METHOD("set_shortcut_in_tooltip", "enabled"), &BaseButton::set_shortcut_in_tooltip);
	ClassRegistry::bind_method(D_METHOD("is_shortcut_in_tooltip_enabled"), &BaseButton::is_shortcut_in_tooltip_enabled);

	ClassRegistry::bind_method(D_METHOD("set_disabled", "disabled"), &BaseButton::set_disabled);
	ClassRegistry::bind_method(D_METHOD("is_disabled"), &BaseButton::is_disabled);

	ClassRegistry::bind_method(D_METHOD("set_action_mode", "mode"), &BaseButton::set_action_mode);
	ClassRegistry::bind_method(D_METHOD("get_action_mode"), &BaseButton::get_action_mode);

	ClassRegistry::bind_method(D_METHOD("set_button_mask", "mask"), &BaseButton::set_button_mask);
	ClassRegistry::bind_method(D_METHOD("get_button_mask"), &BaseButton::get_button_mask);

	ClassRegistry::bind_method(D_METHOD("get_draw_mode"), &BaseButton::get_draw_mode);

	ClassRegistry::bind_method(D_METHOD("set_keep_pressed_outside", "enabled"), &BaseButton::set_keep_pressed_outside);
	ClassRegistry::bind_method(D_METHOD("is_keep_pressed_outside"), &BaseButton::is_keep_pressed_outside);

	ClassRegistry::bind_method(D_METHOD("set_shortcut_feedback", "enabled"), &BaseButton::set_shortcut_feedback);
	ClassRegistry::bind_method(D_METHOD("is_shortcut_feedback"), &BaseButton::is_shortcut_feedback);

	ClassRegistry::bind_method(D_METHOD("set_shortcut", "shortcut"), &BaseButton::set_shortcut);
	ClassRegistry::bind_method(D_METHOD("get_shortcut"), &BaseButton::get_shortcut);

	ClassRegistry::bind_method(D_METHOD("set_button_group", "button_group"), &BaseButton::set_button_group);
	ClassRegistry::bind_method(D_METHOD("get_button_group"), &BaseButton::get_button_group);

	ORBLIT_VIRTUAL_BIND(_pressed);
	ORBLIT_VIRTUAL_BIND(_toggled, "toggled_on");

	ADD_SIGNAL(MethodInfo("pressed"));
	ADD_SIGNAL(MethodInfo("button_up"));
	ADD_SIGNAL(MethodInfo("button_down"));
	ADD_SIGNAL(MethodInfo("toggled", PropertyInfo(Variant::BOOL, "toggled_on")));

	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "disabled"), "set_disabled", "is_disabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "toggle_mode"), "set_toggle_mode", "is_toggle_mode");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "button_pressed"), "set_pressed", "is_pressed");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "action_mode", PROPERTY_HINT_ENUM, "Button Press,Button Release"), "set_action_mode", "get_action_mode");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "button_mask", PROPERTY_HINT_FLAGS, "Mouse Left,Mouse Right,Mouse Middle"), "set_button_mask", "get_button_mask");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "keep_pressed_outside"), "set_keep_pressed_outside", "is_keep_pressed_outside");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "button_group", PROPERTY_HINT_RESOURCE_TYPE, ButtonGroup::get_static_class_name()), "set_button_group", "get_button_group");

	ADD_GROUP("Shortcut", "");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "shortcut", PROPERTY_HINT_RESOURCE_TYPE, Shortcut::get_static_class_name()), "set_shortcut", "get_shortcut");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "shortcut_feedback"), "set_shortcut_feedback", "is_shortcut_feedback");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "shortcut_in_tooltip"), "set_shortcut_in_tooltip", "is_shortcut_in_tooltip_enabled");

	BIND_THEME_ITEM(Theme::DATA_TYPE_CONSTANT, BaseButton, click_margin);

	BIND_ENUM_CONSTANT(DRAW_NORMAL);
	BIND_ENUM_CONSTANT(DRAW_PRESSED);
	BIND_ENUM_CONSTANT(DRAW_HOVER);
	BIND_ENUM_CONSTANT(DRAW_DISABLED);
	BIND_ENUM_CONSTANT(DRAW_HOVER_PRESSED);

	BIND_ENUM_CONSTANT(ACTION_MODE_BUTTON_PRESS);
	BIND_ENUM_CONSTANT(ACTION_MODE_BUTTON_RELEASE);

	BIND_THEME_ITEM(Theme::DATA_TYPE_SOUND, BaseButton, focus_sound);
	BIND_THEME_ITEM(Theme::DATA_TYPE_SOUND, BaseButton, hover_sound);
	BIND_THEME_ITEM(Theme::DATA_TYPE_SOUND, BaseButton, pressed_sound);
	BIND_THEME_ITEM(Theme::DATA_TYPE_SOUND, BaseButton, pressed_disabled_sound);

	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "gui/timers/button_shortcut_feedback_highlight_time", PROPERTY_HINT_RANGE, "0.01,10,0.01,suffix:s"), 0.2);
}

BaseButton::BaseButton() {
	set_focus_mode(FOCUS_ALL);
}

BaseButton::~BaseButton() {
	if (button_group.is_valid()) {
		button_group->buttons.erase(this);
	}
}

void ButtonGroup::get_buttons(List<BaseButton *> *r_buttons) {
	for (BaseButton *E : buttons) {
		r_buttons->push_back(E);
	}
}

TypedArray<BaseButton> ButtonGroup::_get_buttons() {
	TypedArray<BaseButton> btns;
	for (const BaseButton *E : buttons) {
		btns.push_back(E);
	}

	return btns;
}

BaseButton *ButtonGroup::get_pressed_button() {
	for (BaseButton *E : buttons) {
		if (E->is_pressed()) {
			return E;
		}
	}

	return nullptr;
}

void ButtonGroup::set_allow_unpress(bool p_enabled) {
	allow_unpress = p_enabled;
}
bool ButtonGroup::is_allow_unpress() {
	return allow_unpress;
}

void ButtonGroup::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_pressed_button"), &ButtonGroup::get_pressed_button);
	ClassRegistry::bind_method(D_METHOD("get_buttons"), &ButtonGroup::_get_buttons);
	ClassRegistry::bind_method(D_METHOD("set_allow_unpress", "enabled"), &ButtonGroup::set_allow_unpress);
	ClassRegistry::bind_method(D_METHOD("is_allow_unpress"), &ButtonGroup::is_allow_unpress);

	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "allow_unpress"), "set_allow_unpress", "is_allow_unpress");

	ADD_SIGNAL(MethodInfo("pressed", PropertyInfo(Variant::OBJECT, "button", PROPERTY_HINT_RESOURCE_TYPE, BaseButton::get_static_class_name())));
}

ButtonGroup::ButtonGroup() {
	set_local_to_scene(true);
}
