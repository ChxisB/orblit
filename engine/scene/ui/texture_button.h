/**************************************************************************/
/*  texture_button.h                                                      */

#pragma once

#include "scene/resources/bit_map.h"
#include "scene/ui/base_button.h"
class TextureButton : public BaseButton {
	ORBLIT_CLASS(TextureButton, BaseButton);

public:
	enum StretchMode {
		STRETCH_SCALE,
		STRETCH_TILE,
		STRETCH_KEEP,
		STRETCH_KEEP_CENTERED,
		STRETCH_KEEP_ASPECT,
		STRETCH_KEEP_ASPECT_CENTERED,
		STRETCH_KEEP_ASPECT_COVERED,
	};

private:
	Ref<Texture2D> normal;
	Ref<Texture2D> pressed;
	Ref<Texture2D> hover;
	Ref<Texture2D> disabled;
	Ref<Texture2D> focused;
	Ref<BitMap> click_mask;
	bool ignore_texture_size = false;
	StretchMode stretch_mode = STRETCH_KEEP;

	Rect2 _texture_region;
	Rect2 _position_rect;
	bool _tile = false;

	bool hflip = false;
	bool vflip = false;

	void _set_texture(Ref<Texture2D> *p_destination, const Ref<Texture2D> &p_texture);
	void _texture_changed();

protected:
	virtual bool has_point(const Point2 &p_point) const override;
	void _notification(int p_what);
	static void register_members();

public:
	virtual Size2 get_minimum_size() const override;

	void set_texture_normal(const Ref<Texture2D> &p_normal);
	void set_texture_pressed(const Ref<Texture2D> &p_pressed);
	void set_texture_hover(const Ref<Texture2D> &p_hover);
	void set_texture_disabled(const Ref<Texture2D> &p_disabled);
	void set_texture_focused(const Ref<Texture2D> &p_focused);
	void set_click_mask(const Ref<BitMap> &p_click_mask);

	Ref<Texture2D> get_texture_normal() const;
	Ref<Texture2D> get_texture_pressed() const;
	Ref<Texture2D> get_texture_hover() const;
	Ref<Texture2D> get_texture_disabled() const;
	Ref<Texture2D> get_texture_focused() const;
	Ref<BitMap> get_click_mask() const;

	bool get_ignore_texture_size() const;
	void set_ignore_texture_size(bool p_ignore);

	void set_stretch_mode(StretchMode p_stretch_mode);
	StretchMode get_stretch_mode() const;

	void set_flip_h(bool p_flip);
	bool is_flipped_h() const;

	void set_flip_v(bool p_flip);
	bool is_flipped_v() const;
};

VARIANT_ENUM_CAST(TextureButton::StretchMode);
