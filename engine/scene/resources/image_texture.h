/**************************************************************************/
/*  image_texture.h                                                       */

#pragma once

#include "scene/resources/texture.h"

class BitMap;

class ImageTexture : public Texture2D {
	ORBLIT_CLASS(ImageTexture, Texture2D);
	RES_BASE_EXTENSION("tex");

	mutable RID texture;
	Image::Format format = Image::FORMAT_L8;
	bool mipmaps = false;
	int w = 0;
	int h = 0;
	Size2 size_override;
	mutable Ref<BitMap> alpha_cache;
	bool image_stored = false;

protected:
	virtual void reload_from_file() override;
	static void register_members();

public:
	void set_image(const Ref<Image> &p_image);
	static Ref<ImageTexture> create_from_image(const Ref<Image> &p_image);

	virtual Image::Format get_format() const override;

	void update(const Ref<Image> &p_image);
	Ref<Image> get_image() const override;

	int get_width() const override;
	int get_height() const override;

	virtual RID get_rid() const override;

	bool has_alpha() const override;
	virtual void draw(RID p_canvas_item, const Point2 &p_pos, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false) const override;
	virtual void draw_rect(RID p_canvas_item, const Rect2 &p_rect, bool p_tile = false, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false) const override;
	virtual void draw_rect_region(RID p_canvas_item, const Rect2 &p_rect, const Rect2 &p_src_rect, const Color &p_modulate = Color(1, 1, 1), bool p_transpose = false, bool p_clip_uv = true) const override;

	void draw_msdf_rect_region(RID p_canvas_item, const Rect2 &p_rect, const Rect2 &p_src_rect, const Color &p_modulate = Color(1, 1, 1), int p_outline_size = 0, float p_px_range = 1.0, float p_scale = 1.0) const;
	void draw_lcd_rect_region(RID p_canvas_item, const Rect2 &p_rect, const Rect2 &p_src_rect, const Color &p_modulate = Color(1, 1, 1)) const;

	bool is_pixel_opaque(int p_x, int p_y) const override;

	void set_size_override(const Size2i &p_size);

	virtual void set_path(const String &p_path, bool p_take_over = false) override;

	~ImageTexture();
};

class ImageTextureLayered : public TextureLayered {
	ORBLIT_CLASS(ImageTextureLayered, TextureLayered);

	LayeredType layered_type;

	mutable RID texture;
	Image::Format format = Image::FORMAT_L8;

	int width = 0;
	int height = 0;
	int layers = 0;
	bool mipmaps = false;

	Error _create_from_images(const TypedArray<Image> &p_images);

	TypedArray<Image> _get_images() const;
	void _set_images(const TypedArray<Image> &p_images);

protected:
	static void register_members();

public:
	virtual Image::Format get_format() const override;
	virtual int get_width() const override;
	virtual int get_height() const override;
	virtual int get_layers() const override;
	virtual bool has_mipmaps() const override;
	virtual LayeredType get_layered_type() const override;

	Error create_from_images(Vector<Ref<Image>> p_images);
	void update_layer(const Ref<Image> &p_image, int p_layer);
	virtual Ref<Image> get_layer_data(int p_layer) const override;

	virtual RID get_rid() const override;
	virtual void set_path(const String &p_path, bool p_take_over = false) override;

	ImageTextureLayered(LayeredType p_layered_type);
	~ImageTextureLayered();
};

class ImageTexture3D : public Texture3D {
	ORBLIT_CLASS(ImageTexture3D, Texture3D);

	mutable RID texture;

	Image::Format format = Image::FORMAT_L8;
	int width = 1;
	int height = 1;
	int depth = 1;
	bool mipmaps = false;
	bool images_stored = false;

	TypedArray<Image> _get_images() const;
	void _set_images(const TypedArray<Image> &p_images);

protected:
	static void register_members();

	Error _create(Image::Format p_format, int p_width, int p_height, int p_depth, bool p_mipmaps, const TypedArray<Image> &p_data);
	void _update(const TypedArray<Image> &p_data);

public:
	virtual Image::Format get_format() const override;
	virtual int get_width() const override;
	virtual int get_height() const override;
	virtual int get_depth() const override;
	virtual bool has_mipmaps() const override;

	Error create(Image::Format p_format, int p_width, int p_height, int p_depth, bool p_mipmaps, const Vector<Ref<Image>> &p_data);
	void update(const Vector<Ref<Image>> &p_data);
	virtual Vector<Ref<Image>> get_data() const override;

	virtual RID get_rid() const override;
	virtual void set_path(const String &p_path, bool p_take_over = false) override;

	ImageTexture3D();
	~ImageTexture3D();
};

class Texture2DArray : public ImageTextureLayered {
	ORBLIT_CLASS(Texture2DArray, ImageTextureLayered)

protected:
	static void register_members();

public:
	Texture2DArray() :
			ImageTextureLayered(LAYERED_TYPE_2D_ARRAY) {}

	virtual Ref<Resource> create_placeholder() const;
};

class Cubemap : public ImageTextureLayered {
	ORBLIT_CLASS(Cubemap, ImageTextureLayered);

protected:
	static void register_members();

public:
	Cubemap() :
			ImageTextureLayered(LAYERED_TYPE_CUBEMAP) {}

	virtual Ref<Resource> create_placeholder() const;
};

class CubemapArray : public ImageTextureLayered {
	ORBLIT_CLASS(CubemapArray, ImageTextureLayered);

protected:
	static void register_members();

public:
	CubemapArray() :
			ImageTextureLayered(LAYERED_TYPE_CUBEMAP_ARRAY) {}

	virtual Ref<Resource> create_placeholder() const;
};
