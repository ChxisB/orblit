/**************************************************************************/
/*  panel.h                                                               */

#pragma once

#include "scene/ui/control.h"

class Panel : public Control {
	ORBLIT_CLASS(Panel, Control);

	struct ThemeCache {
		Ref<StyleBox> panel_style;
	} theme_cache;

protected:
	void _notification(int p_what);
	static void register_members();

public:
	Panel();
};
