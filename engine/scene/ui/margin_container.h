/**************************************************************************/
/*  margin_container.h                                                    */

#pragma once

#include "scene/ui/container.h"

class MarginContainer : public Container {
	ORBLIT_CLASS(MarginContainer, Container);

	struct ThemeCache {
		int margin_left = 0;
		int margin_top = 0;
		int margin_right = 0;
		int margin_bottom = 0;
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

	int get_margin_size(Side p_side) const;

	MarginContainer();
};
