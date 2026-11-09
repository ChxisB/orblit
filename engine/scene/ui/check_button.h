/**************************************************************************/
/*  check_button.h                                                        */

#pragma once

#include "scene/ui/button.h"

class CheckButton : public Button {
	ORBLIT_CLASS(CheckButton, Button);

	struct ThemeCache {
		int h_separation = 0;
		int check_v_offset = 0;
		Ref<StyleBox> normal_style;

		Ref<Texture2D> checked;
		Ref<Texture2D> unchecked;
		Ref<Texture2D> checked_disabled;
		Ref<Texture2D> unchecked_disabled;
		Ref<Texture2D> checked_mirrored;
		Ref<Texture2D> unchecked_mirrored;
		Ref<Texture2D> checked_disabled_mirrored;
		Ref<Texture2D> unchecked_disabled_mirrored;

		Color button_checked_color;
		Color button_unchecked_color;
	} theme_cache;

protected:
	Size2 get_icon_size() const;
	virtual Size2 get_minimum_size() const override;

	void _notification(int p_what);
	static void register_members();

public:
	CheckButton(const String &p_text = String());
	~CheckButton();
};
