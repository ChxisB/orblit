/**************************************************************************/
/*  text_line.h                                                           */

#pragma once

#include "scene/resources/font.h"
#include "systems/text/text_system.h"

/*************************************************************************/

class TextLine : public RefCounted {
	ORBLIT_CLASS(TextLine, RefCounted);

private:
	RID rid;

	mutable bool dirty = true;

	float width = -1.0;
	BitField<TextSystem::JustificationFlag> flags = TextSystem::JUSTIFICATION_WORD_BOUND | TextSystem::JUSTIFICATION_KASHIDA;
	HorizontalAlignment alignment = HORIZONTAL_ALIGNMENT_LEFT;
	String el_char = U"…";
	TextSystem::OverrunBehavior overrun_behavior = TextSystem::OVERRUN_TRIM_ELLIPSIS;

	Vector<float> tab_stops;

protected:

	static void register_members();

	void _shape() const;

public:
	RID get_rid() const;

	void clear();
	Ref<TextLine> duplicate() const;

	void set_direction(TextSystem::Direction p_direction);
	TextSystem::Direction get_direction() const;
	TextSystem::Direction get_inferred_direction() const;

	void set_bidi_override(const Array &p_override);

	void set_orientation(TextSystem::Orientation p_orientation);
	TextSystem::Orientation get_orientation() const;

	void set_preserve_invalid(bool p_enabled);
	bool get_preserve_invalid() const;

	void set_preserve_control(bool p_enabled);
	bool get_preserve_control() const;

	bool add_string(const String &p_text, const Ref<Font> &p_font, int p_font_size, const String &p_language = "", const Variant &p_meta = Variant());
	bool add_object(Variant p_key, const Size2 &p_size, InlineAlignment p_inline_align = INLINE_ALIGNMENT_CENTER, int p_length = 1, float p_baseline = 0.0);
	bool resize_object(Variant p_key, const Size2 &p_size, InlineAlignment p_inline_align = INLINE_ALIGNMENT_CENTER, float p_baseline = 0.0);
	bool has_object(Variant p_key) const;

	void set_horizontal_alignment(HorizontalAlignment p_alignment);
	HorizontalAlignment get_horizontal_alignment() const;

	void tab_align(const Vector<float> &p_tab_stops);

	void set_flags(BitField<TextSystem::JustificationFlag> p_flags);
	BitField<TextSystem::JustificationFlag> get_flags() const;

	void set_text_overrun_behavior(TextSystem::OverrunBehavior p_behavior);
	TextSystem::OverrunBehavior get_text_overrun_behavior() const;

	void set_ellipsis_char(const String &p_char);
	String get_ellipsis_char() const;

	void set_width(float p_width);
	float get_width() const;

	Array get_objects() const;
	Rect2 get_object_rect(Variant p_key) const;

	Size2 get_size() const;

	float get_line_ascent() const;
	float get_line_descent() const;
	float get_line_width() const;
	float get_line_underline_position() const;
	float get_line_underline_thickness() const;

	void draw(RID p_canvas, const Vector2 &p_pos, const Color &p_color = Color(1, 1, 1), float p_oversampling = 0.0) const;
	void draw_outline(RID p_canvas, const Vector2 &p_pos, int p_outline_size = 1, const Color &p_color = Color(1, 1, 1), float p_oversampling = 0.0) const;

	int hit_test(float p_coords) const;

	TextLine(const String &p_text, const Ref<Font> &p_font, int p_font_size, const String &p_language = "", TextSystem::Direction p_direction = TextSystem::DIRECTION_AUTO, TextSystem::Orientation p_orientation = TextSystem::ORIENTATION_HORIZONTAL);
	TextLine();
	~TextLine();
};
