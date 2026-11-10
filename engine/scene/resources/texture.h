/**************************************************************************/
/*  texture.h                                                             */

#pragma once

#include "core/io/image.h"
#include "core/io/resource.h"
#include "core/variant/typed_array.h"

class Texture : public Resource {
	ORBLIT_CLASS(Texture, Resource);
};

class Texture2D : public Texture {
	ORBLIT_CLASS(Texture2D, Texture);
	OBJ_SAVE_TYPE(Texture2D); // Saves derived classes with common type so they can be interchanged.

protected:
	static void register_members();

	ORBLIT_VIRTUAL0RC(Image::Format, _get_format)
	ORBLIT_VIRTUAL0RC(Ref<Image>, _get_image)
	ORBLIT_VIRTUAL0RC(int, _get_mipmap_count)
	ORBLIT_VIRTUAL0RC(bool, _has_mipmaps)
	ORBLIT_VIRTUAL0RC_REQUIRED(int, _get_width)
	ORBLIT_VIRTUAL0RC_REQUIRED(int, _get_height)
	ORBLIT_VIRTUAL2RC(bool, _is_pixel_opaque, int, int)
	ORBLIT_VIRTUAL0RC(bool, _has_alpha)

	ORBLIT_VIRTUAL4C(_draw, RID, Point2, Color, bool)
	ORBLIT_VIRTUAL5C(_draw_rect, RID, Rect2, bool, Color, bool)
	ORBLIT_VIRTUAL6C(_draw_rect_region, RID, Rect2, Rect2, Color, bool, bool)

public:
	virtual Image::Format get_format() const;
	virtual int get_mipmap_count() const;
	virtual int get_width() const;
	virtual int get_height() const;
	virtual Size2 get_size() const;

	virtual bool is_pixel_opaque(int p_x, int p_y) const;

	virtual bool has_alpha() const;
	virtual bool has_mipmaps() const;

	virtual RID get_scaled_rid() const { return get_rid(); }
	virtual void draw(RID p_canvas_item, const Point2 &p_pos, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false) const;
	virtual void draw_rect(RID p_canvas_item, const Rect2 &p_rect, bool p_tile = false, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false) const;
	virtual void draw_rect_region(RID p_canvas_item, const Rect2 &p_rect, const Rect2 &p_src_rect, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false, bool p_clip_uv = true) const;
	virtual bool get_rect_region(const Rect2 &p_rect, const Rect2 &p_src_rect, Rect2 &r_rect, Rect2 &r_src_rect) const;

	virtual Ref<Image> get_image() const;

	virtual Ref<Resource> create_placeholder() const;

	Texture2D();
};

class TextureLayered : public Texture {
	ORBLIT_CLASS(TextureLayered, Texture);

protected:
	static void register_members();

	ORBLIT_VIRTUAL0RC_REQUIRED(Image::Format, _get_format)
	ORBLIT_VIRTUAL0RC_REQUIRED(uint32_t, _get_layered_type)
	ORBLIT_VIRTUAL0RC_REQUIRED(int, _get_width)
	ORBLIT_VIRTUAL0RC_REQUIRED(int, _get_height)
	ORBLIT_VIRTUAL0RC_REQUIRED(int, _get_layers)
	ORBLIT_VIRTUAL0RC_REQUIRED(bool, _has_mipmaps)
	ORBLIT_VIRTUAL1RC_REQUIRED(Ref<Image>, _get_layer_data, int)
public:
	enum LayeredType {
		LAYERED_TYPE_2D_ARRAY,
		LAYERED_TYPE_CUBEMAP,
		LAYERED_TYPE_CUBEMAP_ARRAY
	};

	virtual Image::Format get_format() const;
	virtual LayeredType get_layered_type() const;
	virtual int get_width() const;
	virtual int get_height() const;
	virtual int get_layers() const;
	virtual bool has_mipmaps() const;
	virtual Ref<Image> get_layer_data(int p_layer) const;
};

VARIANT_ENUM_CAST(TextureLayered::LayeredType)

class Texture3D : public Texture {
	ORBLIT_CLASS(Texture3D, Texture);

protected:
	static void register_members();

	TypedArray<Image> _get_datai() const;

	ORBLIT_VIRTUAL0RC_REQUIRED(Image::Format, _get_format)
	ORBLIT_VIRTUAL0RC_REQUIRED(int, _get_width)
	ORBLIT_VIRTUAL0RC_REQUIRED(int, _get_height)
	ORBLIT_VIRTUAL0RC_REQUIRED(int, _get_depth)
	ORBLIT_VIRTUAL0RC_REQUIRED(bool, _has_mipmaps)
	ORBLIT_VIRTUAL0RC_REQUIRED(TypedArray<Image>, _get_data)
public:
	virtual Image::Format get_format() const;
	virtual int get_width() const;
	virtual int get_height() const;
	virtual int get_depth() const;
	virtual bool has_mipmaps() const;
	virtual Vector<Ref<Image>> get_data() const;
	virtual Ref<Resource> create_placeholder() const;
};
