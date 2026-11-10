/**************************************************************************/
/*  editor_translation_parser.h                                           */

#pragma once

#include "core/object/orblit_virtual.gen.h"
#include "core/object/ref_counted.h"
#include "core/variant/typed_array.h"

class EditorTranslationParserPlugin : public RefCounted {
	ORBLIT_CLASS(EditorTranslationParserPlugin, RefCounted);

protected:
	static void register_members();

	ORBLIT_VIRTUAL1R(TypedArray<PackedStringArray>, _parse_file, String)
	ORBLIT_VIRTUAL0RC(Vector<String>, _get_recognized_extensions)
	ORBLIT_VIRTUAL1RC(TypedArray<PackedStringArray>, _customize_strings, TypedArray<PackedStringArray>)

public:
	virtual Error parse_file(const String &p_path, Vector<Vector<String>> *r_translations);
	virtual void get_recognized_extensions(List<String> *r_extensions) const;
	void customize_strings(Vector<Vector<String>> &r_strings) const;
};

class EditorTranslationParser {
	static EditorTranslationParser *singleton;

public:
	enum ParserType {
		STANDARD, // CSharp, ...
		CUSTOM // User-defined parser plugins. This will override standard parsers if the same extension type is defined.
	};

	static EditorTranslationParser *get_singleton();

	Vector<Ref<EditorTranslationParserPlugin>> standard_parsers;
	Vector<Ref<EditorTranslationParserPlugin>> custom_parsers;

	void get_recognized_extensions(List<String> *r_extensions) const;
	bool can_parse(const String &p_extension) const;
	Ref<EditorTranslationParserPlugin> get_parser(const String &p_extension) const;
	void customize_strings(Vector<Vector<String>> &r_strings) const;
	void add_parser(const Ref<EditorTranslationParserPlugin> &p_parser, ParserType p_type);
	void remove_parser(const Ref<EditorTranslationParserPlugin> &p_parser, ParserType p_type);
	void clean_parsers();

	~EditorTranslationParser();
};
