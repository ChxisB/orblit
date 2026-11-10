/**************************************************************************/
/*  color_mode.h                                                          */

#pragma once

#include "scene/ui/color_picker.h"

class GradientTexture2D;

class ColorMode {
public:
	ColorPicker *color_picker = nullptr;

	virtual String get_name() const = 0;

	virtual int get_slider_count() const { return 3; }
	virtual float get_slider_step() const = 0;
	virtual float get_spinbox_arrow_step() const { return get_slider_step(); }
	virtual String get_slider_label(int idx) const = 0;
	virtual float get_slider_max(int idx) const = 0;
	virtual bool get_allow_greater() const { return false; }
	virtual float get_slider_value(int idx) const = 0;

	virtual float get_alpha_slider_max() const { return 255.0; }
	virtual float get_alpha_slider_value() const { return color_picker->get_pick_color().a * 255.0; }

	virtual Color get_color() const = 0;

	virtual void _value_changed() {}
	virtual void _greater_value_inputted() {}

	virtual void slider_draw(int p_which) = 0;

	ColorMode(ColorPicker *p_color_picker);
	virtual ~ColorMode() {}
};

class ColorModeHSV : public ColorMode {
public:
	String labels[3] = { "H", "S", "V" };
	float slider_max[3] = { 359, 100, 100 };
	float cached_hue = 0.0;
	float cached_saturation = 0.0;

	virtual String get_name() const override { return "HSV"; }

	virtual float get_slider_step() const override { return 1.0; }
	virtual String get_slider_label(int idx) const override;
	virtual float get_slider_max(int idx) const override;
	virtual float get_slider_value(int idx) const override;

	virtual Color get_color() const override;

	virtual void _value_changed() override;

	virtual void slider_draw(int p_which) override;

	ColorModeHSV(ColorPicker *p_color_picker) :
			ColorMode(p_color_picker) {}
};

class ColorModeRGB : public ColorMode {
public:
	String labels[3] = { "R", "G", "B" };
	Ref<GradientTexture2D> rgb_texture[3];

	virtual String get_name() const override { return "RGB"; }

	virtual float get_slider_step() const override { return 1; }
	virtual String get_slider_label(int idx) const override;
	virtual float get_slider_max(int idx) const override { return 255; }
	virtual bool get_allow_greater() const override { return true; }
	virtual float get_slider_value(int idx) const override;

	virtual Color get_color() const override;

	virtual void _greater_value_inputted() override;

	virtual void slider_draw(int p_which) override;

	ColorModeRGB(ColorPicker *p_color_picker) :
			ColorMode(p_color_picker) {}
};

class ColorModeLinear : public ColorMode {
public:
	String labels[3] = { "R", "G", "B" };
	float slider_max[3] = { 1, 1, 1 };
	Ref<GradientTexture2D> rgb_texture[3];

	virtual String get_name() const override { return ETR("Linear"); }

	virtual float get_slider_step() const override { return 0.001; }
	virtual float get_spinbox_arrow_step() const override { return 0.01; }
	virtual String get_slider_label(int idx) const override;
	virtual float get_slider_max(int idx) const override;
	virtual bool get_allow_greater() const override { return true; }
	virtual float get_slider_value(int idx) const override;

	virtual float get_alpha_slider_max() const override;
	virtual float get_alpha_slider_value() const override;

	virtual Color get_color() const override;

	virtual void _greater_value_inputted() override;

	virtual void slider_draw(int p_which) override;

	ColorModeLinear(ColorPicker *p_color_picker) :
			ColorMode(p_color_picker) {}
};

class ColorModeOKHSL : public ColorMode {
public:
	String labels[3] = { "H", "S", "L" };
	float slider_max[3] = { 359, 100, 100 };
	float cached_hue = 0.0;
	float cached_saturation = 0.0;
	Ref<GradientTexture2D> hue_texture;

	virtual String get_name() const override { return "OKHSL"; }

	virtual float get_slider_step() const override { return 1.0; }
	virtual String get_slider_label(int idx) const override;
	virtual float get_slider_max(int idx) const override;
	virtual float get_slider_value(int idx) const override;

	virtual Color get_color() const override;

	virtual void _value_changed() override;

	virtual void slider_draw(int p_which) override;

	ColorModeOKHSL(ColorPicker *p_color_picker) :
			ColorMode(p_color_picker) {}
};
