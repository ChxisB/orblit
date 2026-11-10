/**************************************************************************/
/*  editor_shader_language_plugin.cpp                                     */

#include "editor_shader_language_plugin.h"

Vector<Ref<EditorShaderLanguagePlugin>> EditorShaderLanguagePlugin::shader_languages;
Vector<Vector2i> EditorShaderLanguagePlugin::language_variation_map;

void EditorShaderLanguagePlugin::register_shader_language(const Ref<EditorShaderLanguagePlugin> &p_shader_language) {
	// Allows one ShaderLanguageEditorPlugin to provide multiple dropdown options in
	// the language selection menu. For example, ShaderInclude is handled this way.
	// X is the plugin index, and Y is the language variation index.
	for (int i = 0; i < p_shader_language->get_language_variations().size(); i++) {
		language_variation_map.push_back(Vector2i(shader_languages.size(), i));
	}
	shader_languages.push_back(p_shader_language);
}

void EditorShaderLanguagePlugin::clear_registered_shader_languages() {
	shader_languages.clear();
	language_variation_map.clear();
}

const Vector<Ref<EditorShaderLanguagePlugin>> EditorShaderLanguagePlugin::get_shader_languages_read_only() {
	return shader_languages;
}

int EditorShaderLanguagePlugin::get_shader_language_variation_count() {
	return language_variation_map.size();
}

Ref<EditorShaderLanguagePlugin> EditorShaderLanguagePlugin::get_shader_language_for_index(int p_index) {
	ERR_FAIL_INDEX_V(p_index, language_variation_map.size(), nullptr);
	Vector2i lang_var_indices = language_variation_map[p_index];
	return shader_languages[lang_var_indices.x];
}

String EditorShaderLanguagePlugin::get_file_extension_for_index(int p_index) {
	ERR_FAIL_INDEX_V(p_index, language_variation_map.size(), "ores");
	Vector2i lang_var_indices = language_variation_map[p_index];
	EditorShaderLanguagePlugin *lang = shader_languages[lang_var_indices.x].ptr();
	return lang->get_file_extension(lang_var_indices.y);
}
