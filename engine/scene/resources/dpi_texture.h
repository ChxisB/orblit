/**************************************************************************/
/*  dpi_texture.h                                                         */

#pragma once

#include "scene/resources/texture.h"

class BitMap;

class DPITexture : public Texture2D {
	ORBLIT_CLASS(DPITexture, Texture2D);
	RES_BASE_EXTENSION("dpitex");

	String source;
	float base_scale = 1.0;
	float saturation = 1.0;
	Dictionary color_map;
	Size2 size_override;
	bool fix_alpha_border = false;
	bool premult_alpha = false;

	struct ScalingLevel {
		HashSet<DPITexture *> textures;
		int32_t refcount = 1;
	};
	static Mutex mutex;
	static HashMap<double, ScalingLevel> scaling_levels;

	mutable RID base_texture;
	mutable HashMap<double, RID> texture_cache;
	mutable Ref<BitMap> alpha_cache;
	mutable HashMap<Color, Color> cmap;
	mutable Size2 base_size;
	mutable Size2 size;

	void _remove_scale(double p_scale);
	RID _ensure_scale(double p_scale) const;
	RID _load_at_scale(double p_scale, bool p_set_size) const;
	void _update_texture();
	void _clear();

protected:
	static void register_members();

public:
	static Ref<DPITexture> create_from_string(const String &p_source, float p_scale = 1.0, float p_saturation = 1.0, const Dictionary &p_color_map = Dictionary());

	void set_source(const String &p_source);
	String get_source() const;

	void set_fix_alpha_border(bool p_enabled);
	bool get_fix_alpha_border() const;

	void set_premult_alpha(bool p_enabled);
	bool get_premult_alpha() const;

	void set_base_scale(float p_scale);
	float get_base_scale() const;

	void set_color_map(const Dictionary &p_color_map);
	Dictionary get_color_map() const;

	void set_saturation(float p_saturation);
	float get_saturation() const;

	Ref<Image> get_image() const override;

	int get_width() const override;
	int get_height() const override;

	virtual RID get_rid() const override;

	bool has_alpha() const override;
	virtual RID get_scaled_rid() const override;
	virtual void draw(RID p_canvas_item, const Point2 &p_pos, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false) const override;
	virtual void draw_rect(RID p_canvas_item, const Rect2 &p_rect, bool p_tile = false, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false) const override;
	virtual void draw_rect_region(RID p_canvas_item, const Rect2 &p_rect, const Rect2 &p_src_rect, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false, bool p_clip_uv = true) const override;

	void set_size_override(const Size2i &p_size);
	bool is_pixel_opaque(int p_x, int p_y) const override;

	static void reference_scaling_level(double p_scale);
	static void unreference_scaling_level(double p_scale);

	~DPITexture();
};
