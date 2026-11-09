/**************************************************************************/
/*  curve_texture.h                                                       */

#pragma once

#include "scene/resources/curve.h"
#include "scene/resources/texture.h"

class CurveTexture : public Texture2D {
	ORBLIT_CLASS(CurveTexture, Texture2D);
	RES_BASE_EXTENSION("curvetex")
public:
	enum TextureMode {
		TEXTURE_MODE_RGB,
		TEXTURE_MODE_RED,
	};

private:
	mutable RID _texture;
	Ref<Curve> _curve;
	int _width = 256;
	int _current_width = 0;
	TextureMode texture_mode = TEXTURE_MODE_RGB;
	TextureMode _current_texture_mode = TEXTURE_MODE_RGB;

	void _update();

protected:
	static void register_members();

public:
	void set_width(int p_width);
	int get_width() const override;

	void set_texture_mode(TextureMode p_mode);
	TextureMode get_texture_mode() const;

	void ensure_default_setup(float p_min = 0, float p_max = 1);

	void set_curve(Ref<Curve> p_curve);
	Ref<Curve> get_curve() const;

	virtual RID get_rid() const override;

	virtual int get_height() const override { return 1; }
	virtual bool has_alpha() const override { return false; }

	~CurveTexture();
};

VARIANT_ENUM_CAST(CurveTexture::TextureMode)

class CurveXYZTexture : public Texture2D {
	ORBLIT_CLASS(CurveXYZTexture, Texture2D);
	RES_BASE_EXTENSION("curvetex")

private:
	mutable RID _texture;
	Ref<Curve> _curve_x;
	Ref<Curve> _curve_y;
	Ref<Curve> _curve_z;
	int _width = 256;
	int _current_width = 0;

	void _update();

protected:
	static void register_members();

public:
	void set_width(int p_width);
	int get_width() const override;

	void ensure_default_setup(float p_min = 0, float p_max = 1);

	void set_curve_x(Ref<Curve> p_curve);
	Ref<Curve> get_curve_x() const;

	void set_curve_y(Ref<Curve> p_curve);
	Ref<Curve> get_curve_y() const;

	void set_curve_z(Ref<Curve> p_curve);
	Ref<Curve> get_curve_z() const;

	virtual RID get_rid() const override;

	virtual int get_height() const override { return 1; }
	virtual bool has_alpha() const override { return false; }

	~CurveXYZTexture();
};
