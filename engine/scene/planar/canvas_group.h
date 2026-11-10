/**************************************************************************/
/*  canvas_group.h                                                        */

#pragma once

#include "scene/planar/entity_planar.h"

class CanvasGroup : public Entity2D {
	ORBLIT_CLASS(CanvasGroup, Entity2D)
	real_t fit_margin = 10.0;
	real_t clear_margin = 10.0;
	bool use_mipmaps = false;

protected:
	static void register_members();

public:
	void set_fit_margin(real_t p_fit_margin);
	real_t get_fit_margin() const;

	void set_clear_margin(real_t p_clear_margin);
	real_t get_clear_margin() const;

	void set_use_mipmaps(bool p_use_mipmaps);
	bool is_using_mipmaps() const;

	virtual PackedStringArray get_configuration_warnings() const override;

	CanvasGroup();
	~CanvasGroup();
};
