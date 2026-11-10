/**************************************************************************/
/*  editor_zoom_widget.h                                                  */

#pragma once

#include "scene/ui/box_container.h"
#include "scene/ui/button.h"

class EditorZoomWidget : public HBoxContainer {
	ORBLIT_CLASS(EditorZoomWidget, HBoxContainer);

	Button *zoom_minus = nullptr;
	Button *zoom_reset = nullptr;
	Button *zoom_plus = nullptr;

	float zoom = 1.0;
	float min_zoom = 1.0 / 128;
	float max_zoom = 128.0;
	void _update_zoom_label();
	void _button_zoom_by(int p_direction);
	void _button_zoom_minus();
	void _button_zoom_reset();
	void _button_zoom_plus();

protected:
	void _notification(int p_what);
	static void register_members();

public:
	EditorZoomWidget();

	float get_zoom();
	void set_zoom(float p_zoom);
	void set_zoom_by_increments(int p_increment_count, bool p_integer_only = false);

	float get_min_zoom();
	float get_max_zoom();
	// It's best to setup simultaneously, so min < max can be checked easily.
	void setup_zoom_limits(float p_min, float p_max);
	// Sets the shortcut context for the zoom buttons. By default their context is this EditorZoomWidget control.
	void set_shortcut_context(Entity *p_node) const;
};
