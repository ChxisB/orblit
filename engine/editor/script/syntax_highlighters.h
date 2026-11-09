/**************************************************************************/
/*  syntax_highlighters.h                                                 */

#pragma once

#include "scene/resources/syntax_highlighter.h"

class EditorSyntaxHighlighter : public SyntaxHighlighter {
	ORBLIT_CLASS(EditorSyntaxHighlighter, SyntaxHighlighter)

private:
	Ref<RefCounted> edited_resource;

protected:
	static void register_members();

	ORBLIT_VIRTUAL0RC(String, _get_name)
	ORBLIT_VIRTUAL0RC(PackedStringArray, _get_supported_languages)
	ORBLIT_VIRTUAL0RC(Ref<EditorSyntaxHighlighter>, _create)

public:
	virtual String _get_name() const;
	virtual PackedStringArray _get_supported_languages() const;

	void _set_edited_resource(const Ref<Resource> &p_res) { edited_resource = p_res; }
	Ref<RefCounted> _get_edited_resource() { return edited_resource; }

	virtual Ref<EditorSyntaxHighlighter> _create() const;
};

class EditorStandardSyntaxHighlighter : public EditorSyntaxHighlighter {
	ORBLIT_CLASS(EditorStandardSyntaxHighlighter, EditorSyntaxHighlighter)

private:
	Ref<CodeHighlighter> highlighter;
	ScriptLanguage *script_language = nullptr; // See GH-89610.

public:
	virtual void _update_cache() override;
	virtual Dictionary _get_line_syntax_highlighting_impl(int p_line) override { return highlighter->get_line_syntax_highlighting(p_line); }

	virtual String _get_name() const override { return TTR("Standard"); }

	virtual Ref<EditorSyntaxHighlighter> _create() const override;

	void _set_script_language(ScriptLanguage *p_script_language) { script_language = p_script_language; }

	EditorStandardSyntaxHighlighter() { highlighter.instantiate(); }
};

class EditorPlainTextSyntaxHighlighter : public EditorSyntaxHighlighter {
	ORBLIT_CLASS(EditorPlainTextSyntaxHighlighter, EditorSyntaxHighlighter)

public:
	virtual String _get_name() const override { return TTR("Plain Text"); }

	virtual Ref<EditorSyntaxHighlighter> _create() const override;
};

class EditorJSONSyntaxHighlighter : public EditorSyntaxHighlighter {
	ORBLIT_CLASS(EditorJSONSyntaxHighlighter, EditorSyntaxHighlighter)

private:
	Ref<CodeHighlighter> highlighter;

public:
	virtual void _update_cache() override;
	virtual Dictionary _get_line_syntax_highlighting_impl(int p_line) override { return highlighter->get_line_syntax_highlighting(p_line); }

	virtual PackedStringArray _get_supported_languages() const override { return PackedStringArray{ "json" }; }
	virtual String _get_name() const override { return TTR("JSON"); }

	virtual Ref<EditorSyntaxHighlighter> _create() const override;

	EditorJSONSyntaxHighlighter() { highlighter.instantiate(); }
};

class EditorMarkdownSyntaxHighlighter : public EditorSyntaxHighlighter {
	ORBLIT_CLASS(EditorMarkdownSyntaxHighlighter, EditorSyntaxHighlighter)

private:
	Ref<CodeHighlighter> highlighter;

public:
	virtual void _update_cache() override;
	virtual Dictionary _get_line_syntax_highlighting_impl(int p_line) override { return highlighter->get_line_syntax_highlighting(p_line); }

	virtual PackedStringArray _get_supported_languages() const override { return PackedStringArray{ "md", "markdown" }; }
	virtual String _get_name() const override { return TTR("Markdown"); }

	virtual Ref<EditorSyntaxHighlighter> _create() const override;

	EditorMarkdownSyntaxHighlighter() { highlighter.instantiate(); }
};

class EditorConfigFileSyntaxHighlighter : public EditorSyntaxHighlighter {
	ORBLIT_CLASS(EditorConfigFileSyntaxHighlighter, EditorSyntaxHighlighter)

private:
	Ref<CodeHighlighter> highlighter;

public:
	virtual void _update_cache() override;
	virtual Dictionary _get_line_syntax_highlighting_impl(int p_line) override { return highlighter->get_line_syntax_highlighting(p_line); }

	// While not explicitly designed for those formats, this highlighter happens
	// to handle OSCN, ORES and `project.orblit` well. We can expose it in case the
	// user opens one of these using the script editor (which can be done using
	// the All Files filter).
	virtual PackedStringArray _get_supported_languages() const override { return PackedStringArray{ "ini", "cfg", "oscn", "ores", "orblit" }; }
	virtual String _get_name() const override { return TTR("ConfigFile"); }

	virtual Ref<EditorSyntaxHighlighter> _create() const override;

	EditorConfigFileSyntaxHighlighter() { highlighter.instantiate(); }
};

class GDShaderSyntaxHighlighter : public EditorSyntaxHighlighter {
	ORBLIT_CLASS(GDShaderSyntaxHighlighter, EditorSyntaxHighlighter)

private:
	Ref<CodeHighlighter> highlighter;

	Vector<Point2i> disabled_branch_regions;
	Color disabled_branch_color;

public:
	virtual void _update_cache() override;
	virtual Dictionary _get_line_syntax_highlighting_impl(int p_line) override;

	virtual PackedStringArray _get_supported_languages() const override { return PackedStringArray{ "oshader", "oshaderinc" }; }
	virtual String _get_name() const override { return TTR("GDShader"); }

	virtual Ref<EditorSyntaxHighlighter> _create() const override;

	GDShaderSyntaxHighlighter() { highlighter.instantiate(); }

	void add_disabled_branch_region(const Point2i &p_region);
	void clear_disabled_branch_regions();
	void set_disabled_branch_color(const Color &p_color);
};
