/**************************************************************************/
/*  check_box.h                                                           */

#pragma once

#include "scene/ui/button.h"

class CheckBox : public Button {
	ORBLIT_CLASS(CheckBox, Button);

	struct ThemeCache {
		int h_separation = 0;
		int check_v_offset = 0;
		Ref<StyleBox> normal_style;

		Ref<Texture2D> checked;
		Ref<Texture2D> unchecked;
		Ref<Texture2D> radio_checked;
		Ref<Texture2D> radio_unchecked;
		Ref<Texture2D> checked_disabled;
		Ref<Texture2D> unchecked_disabled;
		Ref<Texture2D> radio_checked_disabled;
		Ref<Texture2D> radio_unchecked_disabled;

		Color checkbox_checked_color;
		Color checkbox_unchecked_color;
	} theme_cache;

protected:
	Size2 get_icon_size() const;
	Size2 get_minimum_size() const override;

	void _notification(int p_what);
	static void register_members();

	bool is_radio() const;

public:
	CheckBox(const String &p_text = String());
	~CheckBox();
};
