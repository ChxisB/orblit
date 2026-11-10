/**************************************************************************/
/*  center_container.h                                                    */

#pragma once

#include "scene/ui/container.h"

class CenterContainer : public Container {
	ORBLIT_CLASS(CenterContainer, Container);

	bool use_top_left = false;

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void set_use_top_left(bool p_enable);
	bool is_using_top_left() const;

	virtual Size2 get_minimum_size() const override;
	virtual Size2 get_desired_size() const override;

	virtual Vector<int> get_allowed_size_flags_horizontal() const override;
	virtual Vector<int> get_allowed_size_flags_vertical() const override;
};
