/**************************************************************************/
/*  spin_box.h                                                            */

#pragma once

#include "scene/ui/line_edit.h"
#include "scene/ui/range.h"
#include "scene/stage/timer.h"

class SpinBoxLineEdit : public LineEdit {
	ORBLIT_CLASS(SpinBoxLineEdit, LineEdit);

protected:
	void _notification(int p_what);

	void _accessibility_action_inc(const Variant &p_data);
	void _accessibility_action_dec(const Variant &p_data);

	virtual String _get_accessibility_name() const override;
};

class SpinBox : public Range {
	ORBLIT_CLASS(SpinBox, Range);

	SpinBoxLineEdit *line_edit = nullptr;
	bool update_on_text_changed = false;
	bool accepted = true;

	struct SizingCache {
		int buttons_block_width = 0;
		int buttons_width = 0;
		int buttons_vertical_separation = 0;
		int buttons_left = 0;
		int button_up_height = 0;
		int button_down_height = 0;
		int second_button_top = 0;
		int buttons_separator_top = 0;
		int field_and_buttons_separator_left = 0;
		int field_and_buttons_separator_width = 0;
	} sizing_cache;

	Timer *range_click_timer = nullptr;
	void _range_click_timeout();
	void _release_mouse_from_drag_mode();
	void _arrow_clicked(bool p_up);

	void _update_text(bool p_only_update_if_value_changed = false);
	void _set_error(const String &p_error);
	void _text_submitted(const String &p_string);
	void _text_changed(const String &p_string);

	String format;
	String plural_format;
	AutoTranslateMode format_auto_translate_mode = AUTO_TRANSLATE_MODE_DISABLED;
	String _get_xl_format() const;

	bool use_default_format = false;
	String last_text_value;
	double custom_arrow_step = 0.0;
	bool custom_arrow_round = false;

	void _line_edit_input(const Ref<InputEvent> &p_event);

	struct Drag {
		double base_val = 0.0;
		bool allowed = false;
		bool enabled = false;
		Vector2 capture_pos;
		double diff_y = 0.0;
	} drag;

	struct StateCache {
		bool up_button_hovered = false;
		bool up_button_pressed = false;
		bool up_button_disabled = false;
		bool down_button_hovered = false;
		bool down_button_pressed = false;
		bool down_button_disabled = false;
	} state_cache;

	void _line_edit_editing_toggled(bool p_toggled_on);

	inline void _compute_sizes();
	inline int _get_widest_button_icon_width();

	struct ThemeCache {
		Ref<Texture2D> up_icon;
		Ref<Texture2D> up_hover_icon;
		Ref<Texture2D> up_pressed_icon;
		Ref<Texture2D> up_disabled_icon;
		Ref<Texture2D> down_icon;
		Ref<Texture2D> down_hover_icon;
		Ref<Texture2D> down_pressed_icon;
		Ref<Texture2D> down_disabled_icon;

		Ref<StyleBox> up_base_stylebox;
		Ref<StyleBox> up_hover_stylebox;
		Ref<StyleBox> up_pressed_stylebox;
		Ref<StyleBox> up_disabled_stylebox;
		Ref<StyleBox> down_base_stylebox;
		Ref<StyleBox> down_hover_stylebox;
		Ref<StyleBox> down_pressed_stylebox;
		Ref<StyleBox> down_disabled_stylebox;

		Color up_icon_modulate;
		Color up_hover_icon_modulate;
		Color up_pressed_icon_modulate;
		Color up_disabled_icon_modulate;
		Color down_icon_modulate;
		Color down_hover_icon_modulate;
		Color down_pressed_icon_modulate;
		Color down_disabled_icon_modulate;

		Ref<StyleBox> field_and_buttons_separator;
		Ref<StyleBox> up_down_buttons_separator;

		int buttons_vertical_separation = 0;
		int field_and_buttons_separation = 0;
		int buttons_width = 0;
		int icon_max_width = 0;

		Ref<AudioStream> focus_sound;
		Ref<AudioStream> pressed_sound;
		Ref<AudioStream> pressed_disabled_sound;
		Ref<AudioStream> drag_started_sound;
		Ref<AudioStream> drag_ended_sound;
	} theme_cache;

	void _mouse_exited();
	void _update_buttons_state_for_current_value();

protected:
	virtual void gui_input(const Ref<InputEvent> &p_event) override;
	void _value_changed(double p_value) override;
	virtual PackedStringArray get_configuration_warnings() const override;

	void _validate_property(PropertyInfo &p_property) const;
	void _notification(int p_what);
	Size2 _fit_icon_size(const Size2 &p_size) const;
	static void register_members();

public:
	LineEdit *get_line_edit();

	virtual Size2 get_minimum_size() const override;

	void set_horizontal_alignment(HorizontalAlignment p_alignment);
	HorizontalAlignment get_horizontal_alignment() const;

	void set_editable(bool p_enabled);
	bool is_editable() const;

	void set_format(const String &p_format);
	String get_format() const;

	void set_plural_format(const String &p_format);
	String get_plural_format() const { return plural_format; }

	void set_format_with_plural(const String &p_format, const String &p_plural);

	void set_format_auto_translate_mode(AutoTranslateMode p_mode);
	AutoTranslateMode get_format_auto_translate_mode() const { return format_auto_translate_mode; }

	void set_update_on_text_changed(bool p_enabled);
	bool get_update_on_text_changed() const;

	void set_select_all_on_focus(bool p_enabled);
	bool is_select_all_on_focus() const;

	void apply();
	void set_custom_arrow_step(const double p_custom_arrow_step);
	double get_custom_arrow_step() const;

	void set_custom_arrow_round(bool p_round);
	bool is_custom_arrow_rounding() const;

	SpinBox();
};
