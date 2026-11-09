/**************************************************************************/
/*  editor_toolbar_group.h                                                */

#pragma once

#include "scene/ui/panel_container.h"

class HBoxContainer;

class EditorToolbarGroup : public PanelContainer {
	ORBLIT_CLASS(EditorToolbarGroup, PanelContainer);

	HBoxContainer *hbox = nullptr;

public:
	EditorToolbarGroup();

	static HBoxContainer *create(Control *p_parent);

	HBoxContainer *get_hbox() const { return hbox; }

protected:
	void _notification(int p_what);
};
