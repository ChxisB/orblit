/**************************************************************************/
/*  text_paragraph.h                                                      */

#pragma once

#include "core/containers/local_vector.h"
#include "core/os/thread_safe.h"
#include "scene/resources/font.h"
#include "systems/text/text_system.h"

/*************************************************************************/

class TextParagraph : public RefCounted {
	ORBLIT_CLASS(TextParagraph, RefCounted);
	_THREAD_SAFE_CLASS_

private:
	mutable RID dropcap_rid;
	mutable int dropcap_lines = 0;
	Rect2 dropcap_margins;

	RID rid;
	mutable LocalVector<RID> lines_rid;

	mutable bool lines_dirty = true;

	float line_spacing = 0.0;
	float width = -1.0;
	int max_lines_visible = -1;

	BitField<TextSystem::LineBreakFlag> brk_flags = TextSystem::BREAK_MANDATORY | TextSystem::BREAK_WORD_BOUND;
	BitField<TextSystem::JustificationFlag> jst_flags = TextSystem::JUSTIFICATION_WORD_BOUND | TextSystem::JUSTIFICATION_KASHIDA | TextSystem::JUSTIFICATION_SKIP_LAST_LINE | TextSystem::JUSTIFICATION_DO_NOT_SKIP_SINGLE_LINE;
	String el_char = U"…";
	TextSystem::OverrunBehavior overrun_behavior = TextSystem::OVERRUN_NO_TRIMMING;

	HorizontalAlignment alignment = HORIZONTAL_ALIGNMENT_LEFT;

	Vector<float> tab_stops;

protected:
	static void register_members();

	void _shape_lines() const;

public:
	RID get_rid() const;
	RID get_line_rid(int p_line) const;
	RID get_dropcap_rid() const;

	void clear();

	Ref<TextParagraph> duplicate() const;

	void set_direction(TextSystem::Direction p_direction);
	TextSystem::Direction get_direction() const;
	TextSystem::Direction get_inferred_direction() const;

	void set_orientation(TextSystem::Orientation p_orientation);
	TextSystem::Orientation get_orientation() const;

	void set_preserve_invalid(bool p_enabled);
	bool get_preserve_invalid() const;

	void set_preserve_control(bool p_enabled);
	bool get_preserve_control() const;

	void set_bidi_override(const Array &p_override);

	void set_custom_punctuation(const String &p_punct);
	String get_custom_punctuation() const;

	bool set_dropcap(const String &p_text, const Ref<Font> &p_font, int p_font_size, const Rect2 &p_dropcap_margins = Rect2(), const String &p_language = "");
	void clear_dropcap();

	bool add_string(const String &p_text, const Ref<Font> &p_font, int p_font_size, const String &p_language = "", const Variant &p_meta = Variant());
	bool add_object(Variant p_key, const Size2 &p_size, InlineAlignment p_inline_align = INLINE_ALIGNMENT_CENTER, int p_length = 1, float p_baseline = 0.0);
	bool resize_object(Variant p_key, const Size2 &p_size, InlineAlignment p_inline_align = INLINE_ALIGNMENT_CENTER, float p_baseline = 0.0);
	bool has_object(Variant p_key) const;

	void set_alignment(HorizontalAlignment p_alignment);
	HorizontalAlignment get_alignment() const;

	void tab_align(const Vector<float> &p_tab_stops);

	void set_justification_flags(BitField<TextSystem::JustificationFlag> p_flags);
	BitField<TextSystem::JustificationFlag> get_justification_flags() const;

	void set_break_flags(BitField<TextSystem::LineBreakFlag> p_flags);
	BitField<TextSystem::LineBreakFlag> get_break_flags() const;

	void set_text_overrun_behavior(TextSystem::OverrunBehavior p_behavior);
	TextSystem::OverrunBehavior get_text_overrun_behavior() const;

	void set_ellipsis_char(const String &p_char);
	String get_ellipsis_char() const;

	void set_width(float p_width);
	float get_width() const;

	void set_max_lines_visible(int p_lines);
	int get_max_lines_visible() const;

	void set_line_spacing(float p_spacing);
	float get_line_spacing() const;

	Size2 get_non_wrapped_size() const;

	Size2 get_size() const;

	Vector2i get_range() const;

	int get_line_count() const;

	Array get_line_objects(int p_line) const;
	Rect2 get_line_object_rect(int p_line, Variant p_key) const;
	Size2 get_line_size(int p_line) const;
	float get_line_ascent(int p_line) const;
	float get_line_descent(int p_line) const;
	float get_line_width(int p_line) const;
	Vector2i get_line_range(int p_line) const;
	float get_line_underline_position(int p_line) const;
	float get_line_underline_thickness(int p_line) const;

	Size2 get_dropcap_size() const;
	int get_dropcap_lines() const;

	void draw(RID p_canvas, const Vector2 &p_pos, const Color &p_color = Color(1, 1, 1), const Color &p_dc_color = Color(1, 1, 1), float p_oversampling = 0.0) const;
	void draw_outline(RID p_canvas, const Vector2 &p_pos, int p_outline_size = 1, const Color &p_color = Color(1, 1, 1), const Color &p_dc_color = Color(1, 1, 1), float p_oversampling = 0.0) const;

	void draw_line(RID p_canvas, const Vector2 &p_pos, int p_line, const Color &p_color = Color(1, 1, 1), float p_oversampling = 0.0) const;
	void draw_line_outline(RID p_canvas, const Vector2 &p_pos, int p_line, int p_outline_size = 1, const Color &p_color = Color(1, 1, 1), float p_oversampling = 0.0) const;

	void draw_dropcap(RID p_canvas, const Vector2 &p_pos, const Color &p_color = Color(1, 1, 1), float p_oversampling = 0.0) const;
	void draw_dropcap_outline(RID p_canvas, const Vector2 &p_pos, int p_outline_size = 1, const Color &p_color = Color(1, 1, 1), float p_oversampling = 0.0) const;

	int hit_test(const Point2 &p_coords) const;

	bool is_dirty();

	Mutex &get_mutex() const { return _thread_safe_; }

	TextParagraph(const String &p_text, const Ref<Font> &p_font, int p_font_size, const String &p_language = "", float p_width = -1.f, TextSystem::Direction p_direction = TextSystem::DIRECTION_AUTO, TextSystem::Orientation p_orientation = TextSystem::ORIENTATION_HORIZONTAL);
	TextParagraph();
	~TextParagraph();
};
