/**************************************************************************/
/*  filter_line_edit.cpp                                                  */

#include "filter_line_edit.h"

void FilterLineEdit::_notification(int p_what) {
	if (p_what == NOTIFICATION_THEME_CHANGED) {
		set_right_icon(get_editor_theme_icon(SNAME("Search")));
	}
}

void FilterLineEdit::gui_input(const Ref<InputEvent> &p_event) {
	ERR_FAIL_NULL(forward_control);

	Ref<InputEventKey> key = p_event;
	if (key.is_null()) {
		LineEdit::gui_input(p_event);
		return;
	}

	// Redirect navigational key events to the control.
	if (key->is_action(SNAME("ui_up"), true) || key->is_action(SNAME("ui_down"), true) || key->is_action(SNAME("ui_page_up")) || key->is_action(SNAME("ui_page_down"))) {
		forward_control->gui_input(key);
		accept_event();
		return;
	}
	LineEdit::gui_input(p_event);
}

void FilterLineEdit::set_forward_control(Control *p_control) {
	ERR_FAIL_NULL(p_control);
	forward_control = p_control;
}

FilterLineEdit::FilterLineEdit() {
	set_clear_button_enabled(true);
}
