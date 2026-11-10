/**************************************************************************/
/*  project_tag.h                                                         */

#pragma once

#include "scene/ui/box_container.h"

class Button;

class ProjectTag : public HBoxContainer {
	ORBLIT_CLASS(ProjectTag, HBoxContainer);

	String tag_string;
	bool display_close = false;

	Button *button = nullptr;

protected:
	void _notification(int p_what);

public:
	void connect_button_to(const Callable &p_callable);
	const String get_tag() const;

	ProjectTag(const String &p_text, bool p_display_close = false);
};
