/**************************************************************************/
/*  separator.h                                                           */

#pragma once

#include "scene/ui/control.h"
class Separator : public Control {
	ORBLIT_CLASS(Separator, Control);

	struct ThemeCache {
		int separation = 0;
		Ref<StyleBox> separator_style;
	} theme_cache;

protected:
	Orientation orientation = Orientation::HORIZONTAL;

	void _notification(int p_what);
	static void register_members();

public:
	virtual Size2 get_minimum_size() const override;

	Separator();
	~Separator();
};

class VSeparator : public Separator {
	ORBLIT_CLASS(VSeparator, Separator);

public:
	VSeparator();
};

class HSeparator : public Separator {
	ORBLIT_CLASS(HSeparator, Separator);

public:
	HSeparator();
};
