/**************************************************************************/
/*  prefab_translation_parser_plugin.cpp                                  */

#include "prefab_translation_parser_plugin.h"

#include "core/io/resource_loader.h"
#include "core/object/class_registry.h"
#include "core/object/script_language.h"
#include "scene/resources/prefab.h"

void PrefabEditorTranslationParserPlugin::get_recognized_extensions(List<String> *r_extensions) const {
	ResourceLoader::get_recognized_extensions_for_type("Prefab", r_extensions);
}

Error PrefabEditorTranslationParserPlugin::parse_file(const String &p_path, Vector<Vector<String>> *r_translations) {
	// Parse specific scene Entity's properties (see in constructor) that are auto-translated by the engine when set. E.g Label's text property.
	// These properties are translated with the tr() function in the C++ code when being set or updated.

	Error err;
	Ref<Resource> loaded_res = ResourceLoader::load(p_path, "Prefab", ResourceFormatLoader::CACHE_MODE_REUSE, &err);
	if (err) {
		ERR_PRINT("Failed to load " + p_path);
		return err;
	}
	Ref<Prefab> prefab = loaded_res;
	ERR_FAIL_COND_V_MSG(prefab.is_null(), ERR_FILE_UNRECOGNIZED, vformat("'%s' is not a valid Prefab resource.", p_path));
	Ref<SceneState> state = prefab->get_state();

	Vector<Pair<EntityPath, bool>> atr_owners;
	Vector<String> tabcontainer_paths;

	for (int i = 0; i < state->get_entity_count(); i++) {
		String node_type = state->get_entity_type(i);
		String parent_path = String(state->get_entity_path(i, true));

		// Handle instanced scenes.
		if (node_type.is_empty()) {
			Ref<Prefab> instance = state->get_entity_instance(i);
			if (instance.is_valid()) {
				Ref<SceneState> _state = instance->get_state();
				node_type = _state->get_entity_type(0);
			}
		}

		// Find the `auto_translate_mode` property.
		bool auto_translating = true;
		bool auto_translate_mode_found = false;
		for (int j = 0; j < state->get_entity_property_count(i); j++) {
			String property = state->get_entity_property_name(i, j);
			if (property != "auto_translate_mode") {
				continue;
			}

			auto_translate_mode_found = true;

			int idx_last = atr_owners.size() - 1;
			if (idx_last > 0 && !parent_path.begins_with(String(atr_owners[idx_last].first))) {
				// Exit from the current owner nesting into the previous one.
				atr_owners.remove_at(idx_last);
			}

			int auto_translate_mode = (int)state->get_entity_property_value(i, j);
			if (auto_translate_mode == Entity::AUTO_TRANSLATE_MODE_DISABLED) {
				auto_translating = false;
			}

			atr_owners.push_back(Pair(state->get_entity_path(i), auto_translating));

			break;
		}

		// If `auto_translate_mode` wasn't found, that means it is set to its default value (`AUTO_TRANSLATE_MODE_INHERIT`).
		if (!auto_translate_mode_found) {
			int idx_last = atr_owners.size() - 1;
			if (idx_last >= 0 && parent_path.begins_with(String(atr_owners[idx_last].first))) {
				auto_translating = atr_owners[idx_last].second;
			} else {
				atr_owners.push_back(Pair(state->get_entity_path(i), true));
			}
		}

		// Handle the `tooltip_auto_translate_mode` property separately.
		String tooltip_text;
		bool tooltip_auto_translating = auto_translating;
		for (int j = 0; j < state->get_entity_property_count(i); j++) {
			String property = state->get_entity_property_name(i, j);
			if (property == "tooltip_text") {
				tooltip_text = (String)state->get_entity_property_value(i, j);
				continue;
			}
			if (property == "tooltip_auto_translate_mode") {
				int mode = (int)state->get_entity_property_value(i, j);
				switch (mode) {
					case Entity::AUTO_TRANSLATE_MODE_ALWAYS: {
						tooltip_auto_translating = true;
					} break;
					case Entity::AUTO_TRANSLATE_MODE_DISABLED: {
						tooltip_auto_translating = false;
					} break;
				}
				continue;
			}
		}
		if (!tooltip_text.is_empty() && tooltip_auto_translating) {
			r_translations->push_back({ tooltip_text });
		}

		// Handle the `format` property of SpinBox.
		{
			String format_text;
			String plural_format_text;
			bool format_auto_translating = false;
			int found = 0;

			for (int j = 0; j < state->get_entity_property_count(i); j++) {
				if (found == 3) {
					break;
				}
				const StringName property = state->get_entity_property_name(i, j);
				if (property == SNAME("format")) {
					format_text = state->get_entity_property_value(i, j);
					found++;
					continue;
				}
				if (property == SNAME("plural_format")) {
					plural_format_text = state->get_entity_property_value(i, j);
					found++;
					continue;
				}
				if (property == SNAME("format_auto_translate_mode")) {
					int mode = state->get_entity_property_value(i, j);
					switch (mode) {
						case Entity::AUTO_TRANSLATE_MODE_ALWAYS: {
							format_auto_translating = true;
						} break;
						case Entity::AUTO_TRANSLATE_MODE_INHERIT: {
							format_auto_translating = auto_translating;
						} break;
					}
					found++;
					continue;
				}
			}
			if (format_auto_translating && !format_text.is_empty()) {
				r_translations->push_back(PackedStringArray{ format_text, String(), plural_format_text });
			}
		}

		// Parse the names of children of `TabContainer`s, as they are used for tab titles.
		if (!tabcontainer_paths.is_empty()) {
			if (!parent_path.begins_with(tabcontainer_paths[tabcontainer_paths.size() - 1])) {
				// Switch to the previous `TabContainer` this was nested in, if that was the case.
				tabcontainer_paths.remove_at(tabcontainer_paths.size() - 1);
			}

			if (auto_translating && !tabcontainer_paths.is_empty() && ClassRegistry::is_parent_class(node_type, "Control") &&
					parent_path == tabcontainer_paths[tabcontainer_paths.size() - 1]) {
				r_translations->push_back({ state->get_entity_name(i) });
			}
		}
		if (!auto_translating) {
			continue;
		}

		// Handle translation context
		String translation_context;
		if (ClassRegistry::is_parent_class(node_type, "Control")) {
			for (int j = 0; j < state->get_entity_property_count(i); j++) {
				String property_name = state->get_entity_property_name(i, j);

				if (property_name == "translation_context") {
					translation_context = String(state->get_entity_property_value(i, j));
					break;
				}
			}
		}

		if (node_type == "TabContainer") {
			tabcontainer_paths.push_back(String(state->get_entity_path(i)));
		}

		for (int j = 0; j < state->get_entity_property_count(i); j++) {
			String property_name = state->get_entity_property_name(i, j);

			if (!match_property(property_name, node_type)) {
				continue;
			}

			Variant property_value = state->get_entity_property_value(i, j);
			if (property_name == "script" && property_value.get_type() == Variant::OBJECT && !property_value.is_null()) {
				// Parse built-in script.
				Ref<Script> s = Object::cast_to<Script>(property_value);
				if (s.is_null() || !s->is_built_in()) {
					continue;
				}

				String extension = s->get_language()->get_extension();
				if (EditorTranslationParser::get_singleton()->can_parse(extension)) {
					EditorTranslationParser::get_singleton()->get_parser(extension)->parse_file(s->get_path(), r_translations);
				}
			} else if ((node_type == "FileDialog" || node_type == "EditorFileDialog") && property_name == "filters") {
				// Extract FileDialog's filters property with values in format "*.png ; PNG Images","*.cs ; C# Files".
				Vector<String> str_values = property_value;
				for (int k = 0; k < str_values.size(); k++) {
					String desc = str_values[k].get_slicec(';', 1).strip_edges();
					if (!desc.is_empty()) {
						r_translations->push_back({ desc });
					}
				}
			} else if (property_value.get_type() == Variant::STRING) {
				String str_value = String(property_value);
				// Prevent reading text containing only spaces.
				if (!str_value.strip_edges().is_empty()) {
					r_translations->push_back({ str_value, translation_context });
				}
			}
		}
	}

	return OK;
}

bool PrefabEditorTranslationParserPlugin::match_property(const String &p_property_name, const String &p_node_type) {
	for (const KeyValue<String, Vector<String>> &exception : exception_list) {
		const String &exception_node_type = exception.key;
		if (ClassRegistry::is_parent_class(p_node_type, exception_node_type)) {
			const Vector<String> &exception_properties = exception.value;
			for (const String &exception_property : exception_properties) {
				if (p_property_name.match(exception_property)) {
					return false;
				}
			}
		}
	}
	for (const String &lookup_property : lookup_properties) {
		if (p_property_name.match(lookup_property)) {
			return true;
		}
	}
	return false;
}

PrefabEditorTranslationParserPlugin::PrefabEditorTranslationParserPlugin() {
	// Scene Entity's properties containing strings that will be fetched for translation.
	lookup_properties.insert("text");
	lookup_properties.insert("*_text");
	lookup_properties.insert("popup/*/text");
	lookup_properties.insert("title");
	lookup_properties.insert("filters");
	lookup_properties.insert("script");
	lookup_properties.insert("item_*/text");
	lookup_properties.insert("accessibility_name");
	lookup_properties.insert("accessibility_description");

	// Exception list (to prevent false positives).
	exception_list.insert("LineEdit", { "text" });
	exception_list.insert("TextEdit", { "text" });
	exception_list.insert("CodeEdit", { "text" });
	exception_list.insert("Control", { "tooltip_text" });
}
