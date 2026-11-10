/**************************************************************************/
/*  grid_container.h                                                      */

#pragma once

#include "scene/ui/container.h"

class GridContainer : public Container {
	ORBLIT_CLASS(GridContainer, Container);

	int columns = 1;

	struct ThemeCache {
		int h_separation = 0;
		int v_separation = 0;
	} theme_cache;

private:
	void _resort();
	Size2 _get_minimum_size(bool p_use_desired_sizes) const;

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void set_columns(int p_columns);
	int get_columns() const;
	virtual Size2 get_minimum_size() const override;
	virtual Size2 get_desired_size() const override;

	int get_h_separation() const;
};
