/**************************************************************************/
/*  nine_patch_rect.h                                                     */

#pragma once

#include "scene/ui/control.h"

class NinePatchRect : public Control {
	ORBLIT_CLASS(NinePatchRect, Control);

public:
	enum AxisStretchMode {
		AXIS_STRETCH_MODE_STRETCH,
		AXIS_STRETCH_MODE_TILE,
		AXIS_STRETCH_MODE_TILE_FIT,
	};

	bool draw_center = true;
	int margin[4] = {};
	Rect2 region_rect;
	Ref<Texture2D> texture;

	AxisStretchMode axis_h = AXIS_STRETCH_MODE_STRETCH;
	AxisStretchMode axis_v = AXIS_STRETCH_MODE_STRETCH;

	void _texture_changed();

protected:
	void _notification(int p_what);
	static void register_members();

public:
	virtual Size2 get_minimum_size() const override;

	void set_texture(const Ref<Texture2D> &p_tex);
	Ref<Texture2D> get_texture() const;

	void set_patch_margin(Side p_side, int p_size);
	int get_patch_margin(Side p_side) const;

	void set_region_rect(const Rect2 &p_region_rect);
	Rect2 get_region_rect() const;

	void set_draw_center(bool p_enabled);
	bool is_draw_center_enabled() const;

	void set_h_axis_stretch_mode(AxisStretchMode p_mode);
	AxisStretchMode get_h_axis_stretch_mode() const;

	void set_v_axis_stretch_mode(AxisStretchMode p_mode);
	AxisStretchMode get_v_axis_stretch_mode() const;

	NinePatchRect();
	~NinePatchRect();
};

VARIANT_ENUM_CAST(NinePatchRect::AxisStretchMode)
