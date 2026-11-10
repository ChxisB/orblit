/**************************************************************************/
/*  editor_theme.h                                                        */

#pragma once

#include "scene/resources/theme.h"

class EditorTheme : public Theme {
	ORBLIT_CLASS(EditorTheme, Theme);

	static Vector<StringName> editor_theme_types;

	uint32_t generated_hash = 0;
	uint32_t generated_fonts_hash = 0;
	uint32_t generated_icons_hash = 0;

public:
	virtual Color get_color(const StringName &p_name, const StringName &p_theme_type) const override;
	virtual int get_constant(const StringName &p_name, const StringName &p_theme_type) const override;
	virtual Ref<Font> get_font(const StringName &p_name, const StringName &p_theme_type) const override;
	virtual int get_font_size(const StringName &p_name, const StringName &p_theme_type) const override;
	virtual Ref<Texture2D> get_icon(const StringName &p_name, const StringName &p_theme_type) const override;
	virtual Ref<StyleBox> get_stylebox(const StringName &p_name, const StringName &p_theme_type) const override;

	void set_generated_hash(uint32_t p_hash) { generated_hash = p_hash; }
	uint32_t get_generated_hash() const { return generated_hash; }

	void set_generated_fonts_hash(uint32_t p_hash) { generated_fonts_hash = p_hash; }
	uint32_t get_generated_fonts_hash() const { return generated_fonts_hash; }

	void set_generated_icons_hash(uint32_t p_hash) { generated_icons_hash = p_hash; }
	uint32_t get_generated_icons_hash() const { return generated_icons_hash; }

	static void initialize();
	static void finalize();
};
