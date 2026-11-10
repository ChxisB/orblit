/**************************************************************************/
/*  atlas_texture.h                                                       */

#pragma once

#include "scene/resources/texture.h"

class AtlasTexture : public Texture2D {
	ORBLIT_CLASS(AtlasTexture, Texture2D);
	RES_BASE_EXTENSION("atlastex");

	Rect2 _get_region_rect() const;

protected:
	Ref<Texture2D> atlas;
	Rect2 region; // Only for property value.
	Rect2 rounded_region; // Region with rounded size (image size is always integer).
	Rect2 margin;
	bool filter_clip = false;

	static void register_members();

public:
	virtual int get_width() const override;
	virtual int get_height() const override;
	virtual RID get_rid() const override;

	virtual bool has_alpha() const override;

	void set_atlas(const Ref<Texture2D> &p_atlas);
	Ref<Texture2D> get_atlas() const;

	void set_region(const Rect2 &p_region);
	Rect2 get_region() const;

	void set_margin(const Rect2 &p_margin);
	Rect2 get_margin() const;

	void set_filter_clip(const bool p_enable);
	bool has_filter_clip() const;

	virtual void draw(RID p_canvas_item, const Point2 &p_pos, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false) const override;
	virtual void draw_rect(RID p_canvas_item, const Rect2 &p_rect, bool p_tile = false, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false) const override;
	virtual void draw_rect_region(RID p_canvas_item, const Rect2 &p_rect, const Rect2 &p_src_rect, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false, bool p_clip_uv = true) const override;
	virtual bool get_rect_region(const Rect2 &p_rect, const Rect2 &p_src_rect, Rect2 &r_rect, Rect2 &r_src_rect) const override;

	bool is_pixel_opaque(int p_x, int p_y) const override;

	virtual Ref<Image> get_image() const override;
};
