/**************************************************************************/
/*  editor_translation_parser.cpp                                         */

#include "editor_translation_parser.h"

#include "core/containers/hash_set.h"
#include "core/error/error_macros.h"
#include "core/object/class_registry.h" // IWYU pragma: keep. `ORBLIT_VIRTUAL_BIND` macro.
#include "core/object/script_language.h"

EditorTranslationParser *EditorTranslationParser::singleton = nullptr;

Error EditorTranslationParserPlugin::parse_file(const String &p_path, Vector<Vector<String>> *r_translations) {
	TypedArray<PackedStringArray> ret;

	if (ORBLIT_VIRTUAL_CALL(_parse_file, p_path, ret)) {
		// Copy over entries directly.
		for (const PackedStringArray translation : ret) {
			r_translations->push_back(translation);
		}

		return OK;
	}

	ERR_PRINT("Custom translation parser plugin's \"_parse_file\" is undefined.");
	return ERR_UNAVAILABLE;
}

void EditorTranslationParserPlugin::get_recognized_extensions(List<String> *r_extensions) const {
	Vector<String> extensions;
	if (ORBLIT_VIRTUAL_CALL(_get_recognized_extensions, extensions)) {
		for (int i = 0; i < extensions.size(); i++) {
			r_extensions->push_back(extensions[i]);
		}
	} else if (!ORBLIT_VIRTUAL_IS_OVERRIDDEN(_customize_strings)) {
		ERR_PRINT("Custom translation parser plugin's \"func get_recognized_extensions()\" is undefined.");
	}
}

void EditorTranslationParserPlugin::customize_strings(Vector<Vector<String>> &r_strings) const {
	if (!ORBLIT_VIRTUAL_IS_OVERRIDDEN(_customize_strings)) {
		return;
	}

	TypedArray<PackedStringArray> new_strings;
	new_strings.resize(r_strings.size());
	int i = 0;
	for (const PackedStringArray &translation : r_strings) {
		new_strings[i] = translation;
		i++;
	}

	ORBLIT_VIRTUAL_CALL(_customize_strings, new_strings, new_strings);

	r_strings.resize(new_strings.size());
	PackedStringArray *translation_write = r_strings.ptrw();
	i = 0;
	for (const Variant &translation : new_strings) {
		translation_write[i] = translation;
		i++;
	}
}

void EditorTranslationParserPlugin::register_members() {
	ORBLIT_VIRTUAL_BIND(_parse_file, "path");
	ORBLIT_VIRTUAL_BIND(_get_recognized_extensions);
	ORBLIT_VIRTUAL_BIND(_customize_strings, "strings");
}

/////////////////////////

void EditorTranslationParser::get_recognized_extensions(List<String> *r_extensions) const {
	HashSet<String> extensions;
	List<String> temp;
	for (int i = 0; i < standard_parsers.size(); i++) {
		standard_parsers[i]->get_recognized_extensions(&temp);
	}
	for (int i = 0; i < custom_parsers.size(); i++) {
		custom_parsers[i]->get_recognized_extensions(&temp);
	}
	// Remove duplicates.
	for (const String &E : temp) {
		extensions.insert(E);
	}
	for (const String &E : extensions) {
		r_extensions->push_back(E);
	}
}

bool EditorTranslationParser::can_parse(const String &p_extension) const {
	List<String> extensions;
	get_recognized_extensions(&extensions);
	for (const String &extension : extensions) {
		if (p_extension == extension) {
			return true;
		}
	}
	return false;
}

Ref<EditorTranslationParserPlugin> EditorTranslationParser::get_parser(const String &p_extension) const {
	// Consider user-defined parsers first.
	for (int i = 0; i < custom_parsers.size(); i++) {
		List<String> temp;
		custom_parsers[i]->get_recognized_extensions(&temp);
		for (const String &E : temp) {
			if (E == p_extension) {
				return custom_parsers[i];
			}
		}
	}

	for (int i = 0; i < standard_parsers.size(); i++) {
		List<String> temp;
		standard_parsers[i]->get_recognized_extensions(&temp);
		for (const String &E : temp) {
			if (E == p_extension) {
				return standard_parsers[i];
			}
		}
	}

	WARN_PRINT("No translation parser available for \"" + p_extension + "\" extension.");

	return nullptr;
}

void EditorTranslationParser::customize_strings(Vector<Vector<String>> &r_strings) const {
	for (const Ref<EditorTranslationParserPlugin> &parser : custom_parsers) {
		parser->customize_strings(r_strings);
	}
}

void EditorTranslationParser::add_parser(const Ref<EditorTranslationParserPlugin> &p_parser, ParserType p_type) {
	if (p_type == ParserType::STANDARD) {
		standard_parsers.push_back(p_parser);
	} else if (p_type == ParserType::CUSTOM) {
		custom_parsers.push_back(p_parser);
	}
}

void EditorTranslationParser::remove_parser(const Ref<EditorTranslationParserPlugin> &p_parser, ParserType p_type) {
	if (p_type == ParserType::STANDARD) {
		standard_parsers.erase(p_parser);
	} else if (p_type == ParserType::CUSTOM) {
		custom_parsers.erase(p_parser);
	}
}

void EditorTranslationParser::clean_parsers() {
	standard_parsers.clear();
	custom_parsers.clear();
}

EditorTranslationParser *EditorTranslationParser::get_singleton() {
	if (!singleton) {
		singleton = memnew(EditorTranslationParser);
	}
	return singleton;
}

EditorTranslationParser::~EditorTranslationParser() {
	memdelete(singleton);
	singleton = nullptr;
}
