/**************************************************************************/
/*  editor_title_bar.h                                                    */

#pragma once

#include "scene/ui/box_container.h"

class EditorTitleBar : public HBoxContainer {
	ORBLIT_CLASS(EditorTitleBar, HBoxContainer);

	Point2i click_pos;
	bool moving = false;
	bool can_move = false;
	Control *center_control = nullptr;

protected:
	void _notification(int p_what);

	virtual void gui_input(const Ref<InputEvent> &p_event) override;
	static void register_members() {}

public:
	void set_center_control(Control *p_center_control);
	Control *get_center_control() const;

	void set_can_move_window(bool p_enabled);
	bool get_can_move_window() const;
};
