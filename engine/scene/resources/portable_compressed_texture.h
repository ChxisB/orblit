/**************************************************************************/
/*  portable_compressed_texture.h                                         */

#pragma once

#include "scene/resources/texture.h"

class BitMap;

class PortableCompressedTexture2D : public Texture2D {
	ORBLIT_CLASS(PortableCompressedTexture2D, Texture2D);

public:
	enum DataFormat {
		DATA_FORMAT_UNDEFINED,
		DATA_FORMAT_IMAGE,
		DATA_FORMAT_PNG,
		DATA_FORMAT_WEBP,
		DATA_FORMAT_BASIS_UNIVERSAL,
	};

	enum CompressionMode {
		COMPRESSION_MODE_LOSSLESS,
		COMPRESSION_MODE_LOSSY,
		COMPRESSION_MODE_BASIS_UNIVERSAL,
		COMPRESSION_MODE_S3TC,
		COMPRESSION_MODE_ETC2,
		COMPRESSION_MODE_BPTC,
		COMPRESSION_MODE_ASTC,
	};

private:
	CompressionMode compression_mode = COMPRESSION_MODE_LOSSLESS;
	static bool keep_all_compressed_buffers;
	bool keep_compressed_buffer = false;
	Vector<uint8_t> compressed_buffer;
	Size2 size;
	Size2 size_override;
	bool mipmaps = false;
	Image::Format format = Image::FORMAT_L8;

	mutable RID texture;
	mutable Ref<BitMap> alpha_cache;

	bool image_stored = false;

	Image::BasisUniversalPackerParams basisu_params;

protected:
	Vector<uint8_t> _get_data() const;
	void _set_data(const Vector<uint8_t> &p_data);

	static void register_members();

public:
	CompressionMode get_compression_mode() const;
	void create_from_image(const Ref<Image> &p_image, CompressionMode p_compression_mode, bool p_normal_map = false, float p_lossy_quality = 0.8);

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

	bool is_pixel_opaque(int p_x, int p_y) const override;

	virtual void set_path(const String &p_path, bool p_take_over = false) override;

	void set_size_override(const Size2 &p_size);
	Size2 get_size_override() const;

	void set_keep_compressed_buffer(bool p_keep);
	bool is_keeping_compressed_buffer() const;

	void set_basisu_compressor_params(int p_uastc_level, float p_rdo_quality_loss);

	static void set_keep_all_compressed_buffers(bool p_keep);
	static bool is_keeping_all_compressed_buffers();

	~PortableCompressedTexture2D();
};

VARIANT_ENUM_CAST(PortableCompressedTexture2D::CompressionMode)
