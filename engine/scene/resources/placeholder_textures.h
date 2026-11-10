/**************************************************************************/
/*  placeholder_textures.h                                                */

#pragma once

#include "scene/resources/texture.h"

class PlaceholderTexture2D : public Texture2D {
	ORBLIT_CLASS(PlaceholderTexture2D, Texture2D)

	mutable RID rid;
	Size2 size = Size2(1, 1);

protected:
	static void register_members();

public:
	void set_size(Size2 p_size);

	virtual int get_width() const override;
	virtual int get_height() const override;
	virtual RID get_rid() const override;
	virtual bool has_alpha() const override;

	virtual Ref<Image> get_image() const override;

	PlaceholderTexture2D();
	~PlaceholderTexture2D();
};

class PlaceholderTexture3D : public Texture3D {
	ORBLIT_CLASS(PlaceholderTexture3D, Texture3D)

	mutable RID rid;
	Vector3i size = Vector3i(1, 1, 1);

protected:
	static void register_members();

public:
	void set_size(const Vector3i &p_size);
	Vector3i get_size() const;
	virtual Image::Format get_format() const override;
	virtual int get_width() const override;
	virtual int get_height() const override;
	virtual int get_depth() const override;
	virtual bool has_mipmaps() const override;
	virtual Vector<Ref<Image>> get_data() const override;
	virtual RID get_rid() const override;

	PlaceholderTexture3D();
	~PlaceholderTexture3D();
};

class PlaceholderTextureLayered : public TextureLayered {
	ORBLIT_CLASS(PlaceholderTextureLayered, TextureLayered)

	mutable RID rid;
	Size2i size = Size2i(1, 1);
	int layers = 1;
	LayeredType layered_type = LAYERED_TYPE_2D_ARRAY;

protected:
	static void register_members();

public:
	void set_size(const Size2i &p_size);
	Size2i get_size() const;
	void set_layers(int p_layers);
	virtual Image::Format get_format() const override;
	virtual LayeredType get_layered_type() const override;
	virtual int get_width() const override;
	virtual int get_height() const override;
	virtual int get_layers() const override;
	virtual bool has_mipmaps() const override;
	virtual Ref<Image> get_layer_data(int p_layer) const override;
	virtual RID get_rid() const override;

	PlaceholderTextureLayered(LayeredType p_type);
	~PlaceholderTextureLayered();
};

class PlaceholderTexture2DArray : public PlaceholderTextureLayered {
	ORBLIT_CLASS(PlaceholderTexture2DArray, PlaceholderTextureLayered)
public:
	PlaceholderTexture2DArray() :
			PlaceholderTextureLayered(LAYERED_TYPE_2D_ARRAY) {}
};

class PlaceholderCubemap : public PlaceholderTextureLayered {
	ORBLIT_CLASS(PlaceholderCubemap, PlaceholderTextureLayered)
public:
	PlaceholderCubemap() :
			PlaceholderTextureLayered(LAYERED_TYPE_CUBEMAP) {}
};

class PlaceholderCubemapArray : public PlaceholderTextureLayered {
	ORBLIT_CLASS(PlaceholderCubemapArray, PlaceholderTextureLayered)
public:
	PlaceholderCubemapArray() :
			PlaceholderTextureLayered(LAYERED_TYPE_CUBEMAP_ARRAY) {}
};
