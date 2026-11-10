/**************************************************************************/
/*  filter_line_edit.h                                                    */

#pragma once

#include "scene/ui/line_edit.h"

class FilterLineEdit : public LineEdit {
	ORBLIT_CLASS(FilterLineEdit, LineEdit);

	Control *forward_control = nullptr;

protected:
	void _notification(int p_what);

	virtual void gui_input(const Ref<InputEvent> &p_event) override;

public:
	void set_forward_control(Control *p_control);

	FilterLineEdit();
};
