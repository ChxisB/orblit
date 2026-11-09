/**************************************************************************/
/*  event_listener_line_edit.h                                            */

#pragma once

#include "scene/ui/line_edit.h"

enum InputType {
	INPUT_KEY = 1,
	INPUT_MOUSE_BUTTON = 2,
	INPUT_JOY_BUTTON = 4,
	INPUT_JOY_MOTION = 8
};

class EventListenerLineEdit : public LineEdit {
	ORBLIT_CLASS(EventListenerLineEdit, LineEdit)

	uint64_t hold_next = 0;
	Ref<InputEvent> hold_event;

	int allowed_input_types = INPUT_KEY | INPUT_MOUSE_BUTTON | INPUT_JOY_BUTTON | INPUT_JOY_MOTION;
	bool ignore_next_event = true;
	Ref<InputEvent> event;

	bool _is_event_allowed(const Ref<InputEvent> &p_event) const;

	void gui_input(const Ref<InputEvent> &p_event) override;
	void _on_text_changed(const String &p_text);

protected:
	void _notification(int p_what);
	static void register_members();

public:
	static String get_event_text(const Ref<InputEvent> &p_event, bool p_include_device);
	static String get_device_string(int p_device);

	Ref<InputEvent> get_event() const;
	void clear_event();

	void set_allowed_input_types(int p_type_masks);
	int get_allowed_input_types() const;

	void grab_focus();

public:
	EventListenerLineEdit();
};
