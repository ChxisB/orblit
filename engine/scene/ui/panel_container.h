/**************************************************************************/
/*  panel_container.h                                                     */

#pragma once

#include "scene/ui/container.h"

class PanelContainer : public Container {
	ORBLIT_CLASS(PanelContainer, Container);

	struct ThemeCache {
		Ref<StyleBox> panel_style;
	} theme_cache;

protected:
	void _notification(int p_what);
	static void register_members();

public:
	virtual Size2 get_minimum_size() const override;
	virtual Size2 get_desired_size() const override;
	virtual Size2 get_inner_combined_maximum_size() const override;

	virtual Vector<int> get_allowed_size_flags_horizontal() const override;
	virtual Vector<int> get_allowed_size_flags_vertical() const override;

	PanelContainer();
};
