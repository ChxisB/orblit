/**************************************************************************/
/*  scroll_bar.h                                                          */

#pragma once

#include "scene/ui/range.h"

class ScrollBar : public Range {
	ORBLIT_CLASS(ScrollBar, Range);

	enum HighlightStatus {
		HIGHLIGHT_NONE,
		HIGHLIGHT_DECR,
		HIGHLIGHT_RANGE,
		HIGHLIGHT_INCR,
	};

	static bool focus_by_default;

	Orientation orientation;
	Size2 size;
	float custom_step = -1.0;

	HighlightStatus highlight = HIGHLIGHT_NONE;

	bool incr_active = false;
	bool decr_active = false;

	struct Drag {
		bool active = false;
		float pos_at_click = 0.0;
		float value_at_click = 0.0;
	} drag;

	double get_grabber_size() const;
	double get_grabber_min_size() const;
	double get_area_size() const;
	double get_grabber_offset() const;

	static void set_can_focus_by_default(bool p_can_focus);

	Entity *drag_node = nullptr;
	EntityPath drag_node_path;
	bool drag_node_enabled = true;

	Vector2 drag_node_speed;
	Vector2 drag_node_accum;
	Vector2 drag_node_from;
	Vector2 last_drag_node_accum;
	float time_since_motion = 0.0;
	bool drag_node_touching = false;
	bool drag_node_touching_deaccel = false;

	bool scrolling = false;
	double target_scroll = 0.0;
	bool smooth_scroll_enabled = false;

	void _drag_node_exit();
	void _drag_node_input(const Ref<InputEvent> &p_input);
	Size2 _fit_icon_size(const Size2 &p_size) const;

	virtual void gui_input(const Ref<InputEvent> &p_event) override;

protected:
	struct ThemeCache {
		Ref<StyleBox> scroll_style;
		Ref<StyleBox> scroll_focus_style;
		Ref<StyleBox> scroll_offset_style;
		Ref<StyleBox> grabber_style;
		Ref<StyleBox> grabber_hl_style;
		Ref<StyleBox> grabber_pressed_style;

		Ref<Texture2D> increment_icon;
		Ref<Texture2D> increment_hl_icon;
		Ref<Texture2D> increment_pressed_icon;
		Ref<Texture2D> decrement_icon;
		Ref<Texture2D> decrement_hl_icon;
		Ref<Texture2D> decrement_pressed_icon;

		int padding_left = 0;
		int padding_top = 0;
		int padding_right = 0;
		int padding_bottom = 0;

		int icon_max_size = 0;

		Ref<AudioStream> drag_started_sound;
		Ref<AudioStream> drag_ended_sound;
		Ref<AudioStream> value_changed_sound;
		Ref<AudioStream> value_change_rejected_sound;
	} theme_cache;

	void _notification(int p_what);
	static void register_members();

public:
	static inline const int PAGE_DIVISOR = 8;

	void scroll(double p_amount);
	void scroll_to(double p_position);

	void set_custom_step(float p_custom_step);
	float get_custom_step() const;

	void set_drag_node(const EntityPath &p_path);
	EntityPath get_drag_node() const;
	void set_drag_node_enabled(bool p_enable);

	void set_smooth_scroll_enabled(bool p_enable);
	bool is_smooth_scroll_enabled() const;

	virtual Size2 get_minimum_size() const override;
	ScrollBar(Orientation p_orientation = VERTICAL);
	~ScrollBar();
};

class HScrollBar : public ScrollBar {
	ORBLIT_CLASS(HScrollBar, ScrollBar);

protected:
	static void register_members();

public:
	HScrollBar() :
			ScrollBar(HORIZONTAL) { set_v_size_flags(SIZE_SHRINK_BEGIN); }
};

class VScrollBar : public ScrollBar {
	ORBLIT_CLASS(VScrollBar, ScrollBar);

protected:
	static void register_members();

public:
	VScrollBar() :
			ScrollBar(VERTICAL) { set_h_size_flags(SIZE_SHRINK_BEGIN); }
};
