/**************************************************************************/
/*  view_panner.h                                                         */

#pragma once

#include "core/object/ref_counted.h"

class InputEvent;
class Entity;
class Shortcut;

class ViewPanner : public RefCounted {
	ORBLIT_CLASS(ViewPanner, RefCounted);

public:
	enum ControlScheme {
		SCROLL_ZOOMS,
		SCROLL_PANS,
	};

	enum PanAxis {
		PAN_AXIS_BOTH,
		PAN_AXIS_HORIZONTAL,
		PAN_AXIS_VERTICAL,
	};

	enum DragType {
		DRAG_TYPE_NONE,
		DRAG_TYPE_PAN,
		DRAG_TYPE_ZOOM,
	};

	enum ZoomStyle {
		ZOOM_VERTICAL,
		ZOOM_HORIZONTAL,
	};

private:
	int scroll_speed = 32;
	float scroll_zoom_factor = 1.1;
	PanAxis pan_axis = PAN_AXIS_BOTH;

	bool pan_key_pressed = false;
	bool force_drag = false;

	DragType drag_type = DragType::DRAG_TYPE_NONE;

	ZoomStyle zoom_style = ZoomStyle::ZOOM_VERTICAL;

	Vector2 drag_zoom_position;
	float drag_zoom_sensitivity_factor = -0.01f;

	bool enable_rmb = false;
	bool simple_panning_enabled = false;

	Ref<Shortcut> pan_view_shortcut;

	Callable pan_callback;
	Callable zoom_callback;

	ControlScheme control_scheme = SCROLL_ZOOMS;
	Entity *warped_panning_owner = nullptr;

public:
	void set_callbacks(Callable p_pan_callback, Callable p_zoom_callback);
	void set_control_scheme(ControlScheme p_scheme);
	void set_enable_rmb(bool p_enable);
	void set_pan_shortcut(Ref<Shortcut> p_shortcut);
	void set_simple_panning_enabled(bool p_enabled);
	void set_scroll_speed(int p_scroll_speed);
	void set_scroll_zoom_factor(float p_scroll_zoom_factor);
	void set_pan_axis(PanAxis p_pan_axis);
	void set_zoom_style(ZoomStyle p_zoom_style);

	void setup(ControlScheme p_scheme, Ref<Shortcut> p_shortcut, bool p_simple_panning);
	void setup_warped_panning(Entity *p_owner, bool p_allowed);

	bool is_panning() const;
	void set_force_drag(bool p_force);

	bool gui_input(const Ref<InputEvent> &p_ev, Rect2 p_canvas_rect = Rect2());
	void release_pan_key();

	ViewPanner();
};
