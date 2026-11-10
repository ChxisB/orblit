/**************************************************************************/
/*  link_button.h                                                         */

#pragma once

#include "scene/resources/text_line.h"
#include "scene/ui/base_button.h"

class LinkButton : public BaseButton {
	ORBLIT_CLASS(LinkButton, BaseButton);

public:
	enum UnderlineMode {
		UNDERLINE_MODE_ALWAYS,
		UNDERLINE_MODE_ON_HOVER,
		UNDERLINE_MODE_NEVER
	};

private:
	String text;
	String xl_text;
	Ref<TextLine> text_buf;
	UnderlineMode underline_mode = UNDERLINE_MODE_ALWAYS;
	String uri;

	String language;
	TextDirection text_direction = TEXT_DIRECTION_AUTO;
	TextSystem::StructuredTextParser st_parser = TextSystem::STRUCTURED_TEXT_DEFAULT;
	Array st_args;
	TextSystem::OverrunBehavior overrun_behavior = TextSystem::OVERRUN_NO_TRIMMING;
	String el_char = U"…";

	struct ThemeCache {
		Ref<StyleBox> focus;

		Color font_color;
		Color font_focus_color;
		Color font_pressed_color;
		Color font_hover_color;
		Color font_hover_pressed_color;
		Color font_disabled_color;

		Ref<Font> font;
		int font_size = 0;
		int outline_size = 0;
		Color font_outline_color;

		int underline_spacing = 0;
	} theme_cache;

	void _shape();

protected:
	virtual void pressed() override;

	void _notification(int p_what);
	static void register_members();

	virtual String _get_accessibility_name() const override;

public:
	virtual Size2 get_minimum_size() const override;
	virtual CursorShape get_cursor_shape(const Point2 &p_pos) const override;

	void set_text(const String &p_text);
	String get_text() const;
	void set_uri(const String &p_uri);
	String get_uri() const;

	void set_text_overrun_behavior(TextSystem::OverrunBehavior p_behavior);
	TextSystem::OverrunBehavior get_text_overrun_behavior() const;

	void set_ellipsis_char(const String &p_char);
	String get_ellipsis_char() const;

	void set_structured_text_bidi_override(TextSystem::StructuredTextParser p_parser);
	TextSystem::StructuredTextParser get_structured_text_bidi_override() const;

	void set_structured_text_bidi_override_options(const Array &p_args);
	Array get_structured_text_bidi_override_options() const;

	void set_text_direction(TextDirection p_text_direction);
	TextDirection get_text_direction() const;

	void set_language(const String &p_language);
	String get_language() const;

	void set_underline_mode(UnderlineMode p_underline_mode);
	UnderlineMode get_underline_mode() const;

	Ref<Font> get_button_font() const;
	int get_button_font_size() const;

	LinkButton(const String &p_text = String());
};

VARIANT_ENUM_CAST(LinkButton::UnderlineMode);
