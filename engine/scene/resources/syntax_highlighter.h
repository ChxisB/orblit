/**************************************************************************/
/*  syntax_highlighter.h                                                  */

#pragma once

#include "core/io/resource.h"
#include "core/object/orblit_virtual.gen.h"
#include "core/containers/rb_map.h"

class TextEdit;

class SyntaxHighlighter : public Resource {
	ORBLIT_CLASS(SyntaxHighlighter, Resource)

private:
	RBMap<int, Dictionary> highlighting_cache;
	void _lines_edited_from(int p_from_line, int p_to_line);

protected:
	ObjectID text_edit_instance_id; // For validity check
	TextEdit *text_edit = nullptr;

	static void register_members();

	ORBLIT_VIRTUAL1RC(Dictionary, _get_line_syntax_highlighting, int)
	ORBLIT_VIRTUAL0(_clear_highlighting_cache)
	ORBLIT_VIRTUAL0(_update_cache)
public:
	Dictionary get_line_syntax_highlighting(int p_line);
	virtual Dictionary _get_line_syntax_highlighting_impl(int p_line) { return Dictionary(); }

	void clear_highlighting_cache();
	virtual void _clear_highlighting_cache() {}

	void update_cache();
	virtual void _update_cache() {}

	void set_text_edit(TextEdit *p_text_edit);
	TextEdit *get_text_edit() const;

	virtual ~SyntaxHighlighter() {}
};

///////////////////////////////////////////////////////////////////////////////

class CodeHighlighter : public SyntaxHighlighter {
	ORBLIT_CLASS(CodeHighlighter, SyntaxHighlighter)

private:
	struct ColorRegion {
		Color color;
		String start_key;
		String end_key;
		bool line_only = false;
	};
	Vector<ColorRegion> color_regions;
	HashMap<int, int> color_region_cache;

	Dictionary keywords;
	Dictionary member_keywords;

	Color font_color;
	Color member_color;
	Color function_color;
	Color symbol_color;
	Color number_color;

	bool uint_suffix_enabled = false;

protected:
	static void register_members();

public:
	virtual Dictionary _get_line_syntax_highlighting_impl(int p_line) override;

	virtual void _clear_highlighting_cache() override;
	virtual void _update_cache() override;

	void add_keyword_color(const String &p_keyword, const Color &p_color);
	void remove_keyword_color(const String &p_keyword);
	bool has_keyword_color(const String &p_keyword) const;
	Color get_keyword_color(const String &p_keyword) const;

	void set_keyword_colors(const Dictionary p_keywords);
	void clear_keyword_colors();
	Dictionary get_keyword_colors() const;

	void add_member_keyword_color(const String &p_member_keyword, const Color &p_color);
	void remove_member_keyword_color(const String &p_member_keyword);
	bool has_member_keyword_color(const String &p_member_keyword) const;
	Color get_member_keyword_color(const String &p_member_keyword) const;

	void set_member_keyword_colors(const Dictionary &p_color_regions);
	void clear_member_keyword_colors();
	Dictionary get_member_keyword_colors() const;

	void add_color_region(const String &p_start_key, const String &p_end_key, const Color &p_color, bool p_line_only = false);
	void remove_color_region(const String &p_start_key);
	bool has_color_region(const String &p_start_key) const;

	void set_color_regions(const Dictionary &p_member_keyword);
	void clear_color_regions();
	Dictionary get_color_regions() const;

	void set_number_color(Color p_color);
	Color get_number_color() const;

	void set_symbol_color(Color p_color);
	Color get_symbol_color() const;

	void set_function_color(Color p_color);
	Color get_function_color() const;

	void set_member_variable_color(Color p_color);
	Color get_member_variable_color() const;

	void set_uint_suffix_enabled(bool p_enabled);
};
