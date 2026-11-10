/**************************************************************************/
/*  editor_main_screen.h                                                  */

#pragma once

#include "editor/docks/dock_tab_container.h"
#include "scene/ui/box_container.h"

class ConfigFile;
class EditorDock;
class EditorPlugin;

class EditorMainScreen : public DockTabContainer {
	ORBLIT_CLASS(EditorMainScreen, DockTabContainer);

private:
	void _on_tab_changed(int p_tab);

protected:
	void _notification(int p_what);

	virtual void update_visibility() override { show(); } // Never hide main screen.
	virtual TabStyle get_tab_style() const override;
	virtual Rect2 get_drag_hint_rect() const override;
	virtual bool can_switch_dock() const override;

public:
	void select_next();
	void select_prev();

	EditorMainScreen();
};
