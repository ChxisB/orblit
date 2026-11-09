/**************************************************************************/
/*  window_wrapper.h                                                      */

#pragma once

#include "core/math/rect2.h"
#include "scene/ui/button.h"
#include "scene/ui/margin_container.h"

class HBoxContainer;
class Popup;
class Window;

class WindowWrapper : public MarginContainer {
	ORBLIT_CLASS(WindowWrapper, MarginContainer);

	Control *wrapped_control = nullptr;
	MarginContainer *margins = nullptr;
	Window *window = nullptr;
	ObjectID window_id;

	Panel *window_background = nullptr;

	Ref<Shortcut> enable_shortcut;

	bool override_close_request = false;

	Rect2 _get_default_window_rect() const;
	Entity *_get_wrapped_control_parent() const;

	void _set_window_enabled_with_rect(bool p_visible, const Rect2 p_rect);
	void _set_window_rect(const Rect2 p_rect);
	void _window_size_changed();
	void _window_close_request();

protected:
	static void register_members();
	void _notification(int p_what);

	virtual void shortcut_input(const Ref<InputEvent> &p_event) override;

public:
	void set_wrapped_control(Control *p_control, const Ref<Shortcut> &p_enable_shortcut = Ref<Shortcut>());
	Control *get_wrapped_control() const;
	Control *release_wrapped_control();

	bool is_window_available() const;

	bool get_window_enabled() const;
	void set_window_enabled(bool p_enabled);

	Rect2i get_window_rect() const;
	int get_window_screen() const;

	void restore_window(const Rect2i &p_rect, int p_screen = -1);
	void restore_window_from_saved_position(const Rect2 p_window_rect, int p_screen, const Rect2 p_screen_rect);
	void enable_window_on_screen(int p_screen = -1, bool p_auto_scale = false);

	void set_window_title(const String &p_title);
	void set_margins_enabled(bool p_enabled);
	Size2 get_margins_size();
	Size2 get_margins_top_left();
	void grab_window_focus();

	void set_override_close_request(bool p_enabled);

	WindowWrapper();
	~WindowWrapper();
};

class ScreenSelect : public Button {
	ORBLIT_CLASS(ScreenSelect, Button);

	Popup *popup = nullptr;
	HBoxContainer *screen_list = nullptr;

	void _build_advanced_menu();

	void _emit_screen_signal(int p_screen_idx);
	void _handle_mouse_shortcut(const Ref<InputEvent> &p_event);
	void _show_popup();

protected:
	virtual void pressed() override;
	static void register_members();

	void _notification(int p_what);

public:
	ScreenSelect();
};
