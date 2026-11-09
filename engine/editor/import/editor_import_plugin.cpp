/**************************************************************************/
/*  editor_import_plugin.cpp                                              */

#include "editor_import_plugin.h"

#include "core/object/class_registry.h"
#include "core/object/script_language.h"
#include "editor/file_system/editor_file_system.h"

String EditorImportPlugin::get_importer_name() const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_importer_name, ret);
	return ret;
}

String EditorImportPlugin::get_visible_name() const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_visible_name, ret);
	return ret;
}

void EditorImportPlugin::get_recognized_extensions(List<String> *p_extensions) const {
	Vector<String> extensions;
	ORBLIT_VIRTUAL_CALL(_get_recognized_extensions, extensions);
	for (int i = 0; i < extensions.size(); i++) {
		p_extensions->push_back(extensions[i]);
	}
}

String EditorImportPlugin::get_preset_name(int p_idx) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_preset_name, p_idx, ret);
	return ret;
}

int EditorImportPlugin::get_preset_count() const {
	int ret;
	if (ORBLIT_VIRTUAL_CALL(_get_preset_count, ret)) {
		return ret;
	}
	return 0;
}

String EditorImportPlugin::get_save_extension() const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_save_extension, ret);
	return ret;
}

String EditorImportPlugin::get_resource_type() const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_resource_type, ret);
	return ret;
}

float EditorImportPlugin::get_priority() const {
	float ret;
	if (ORBLIT_VIRTUAL_CALL(_get_priority, ret)) {
		return ret;
	}
	return 1.0;
}

int EditorImportPlugin::get_import_order() const {
	int ret;
	if (ORBLIT_VIRTUAL_CALL(_get_import_order, ret)) {
		return ret;
	}
	return IMPORT_ORDER_DEFAULT;
}

int EditorImportPlugin::get_format_version() const {
	int ret = 0;
	if (ORBLIT_VIRTUAL_CALL(_get_format_version, ret)) {
		return ret;
	}
	return 0;
}

void EditorImportPlugin::get_import_options(const String &p_path, List<ResourceImporter::ImportOption> *r_options, int p_preset) const {
	Array needed = { "name", "default_value" };
	TypedArray<Dictionary> options;
	ORBLIT_VIRTUAL_CALL(_get_import_options, p_path, p_preset, options);
	for (int i = 0; i < options.size(); i++) {
		Dictionary d = options[i];
		ERR_FAIL_COND(!d.has_all(needed));
		String name = d["name"];
		Variant default_value = d["default_value"];

		PropertyHint hint = PROPERTY_HINT_NONE;
		if (d.has("property_hint")) {
			hint = (PropertyHint)d["property_hint"].operator int64_t();
		}

		String hint_string;
		if (d.has("hint_string")) {
			hint_string = d["hint_string"];
		}

		uint32_t usage = PROPERTY_USAGE_DEFAULT;
		if (d.has("usage")) {
			usage = d["usage"];
		}

		ImportOption option(PropertyInfo(default_value.get_type(), name, hint, hint_string, usage), default_value);
		r_options->push_back(option);
	}
}

bool EditorImportPlugin::get_option_visibility(const String &p_path, const String &p_option, const HashMap<StringName, Variant> &p_options) const {
	Dictionary d;
	HashMap<StringName, Variant>::ConstIterator E = p_options.begin();
	while (E) {
		d[E->key] = E->value;
		++E;
	}
	bool visible = false;
	if (ORBLIT_VIRTUAL_CALL(_get_option_visibility, p_path, p_option, d, visible)) {
		return visible;
	}
	return true;
}

Error EditorImportPlugin::import(ResourceUID::ID p_source_id, const String &p_source_file, const String &p_save_path, const HashMap<StringName, Variant> &p_options, List<String> *r_platform_variants, List<String> *r_gen_files, Variant *r_metadata) {
	Dictionary options;
	TypedArray<String> platform_variants, gen_files;

	HashMap<StringName, Variant>::ConstIterator E = p_options.begin();
	while (E) {
		options[E->key] = E->value;
		++E;
	}

	Error err = OK;
	ORBLIT_VIRTUAL_CALL(_import, p_source_file, p_save_path, options, platform_variants, gen_files, err);
	for (int i = 0; i < platform_variants.size(); i++) {
		r_platform_variants->push_back(platform_variants[i]);
	}
	for (int i = 0; i < gen_files.size(); i++) {
		r_gen_files->push_back(gen_files[i]);
	}
	return err;
}

bool EditorImportPlugin::can_import_threaded() const {
	bool ret = false;
	if (ORBLIT_VIRTUAL_CALL(_can_import_threaded, ret)) {
		return ret;
	} else {
		return ResourceImporter::can_import_threaded();
	}
}

Error EditorImportPlugin::_append_import_external_resource(const String &p_file, const Dictionary &p_custom_options, const String &p_custom_importer, Variant p_generator_parameters) {
	HashMap<StringName, Variant> options;
	for (const KeyValue<Variant, Variant> &kv : p_custom_options) {
		options.insert(kv.key, kv.value);
	}
	return append_import_external_resource(p_file, options, p_custom_importer, p_generator_parameters);
}

Error EditorImportPlugin::append_import_external_resource(const String &p_file, const HashMap<StringName, Variant> &p_custom_options, const String &p_custom_importer, Variant p_generator_parameters) {
	ERR_FAIL_COND_V_MSG(!EditorFileSystem::get_singleton()->is_importing(), ERR_INVALID_PARAMETER, "Can only append files to import during a current reimport process.");
	return EditorFileSystem::get_singleton()->reimport_append(p_file, p_custom_options, p_custom_importer, p_generator_parameters);
}

void EditorImportPlugin::register_members() {
	ORBLIT_VIRTUAL_BIND(_get_importer_name)
	ORBLIT_VIRTUAL_BIND(_get_visible_name)
	ORBLIT_VIRTUAL_BIND(_get_preset_count)
	ORBLIT_VIRTUAL_BIND(_get_preset_name, "preset_index")
	ORBLIT_VIRTUAL_BIND(_get_recognized_extensions)
	ORBLIT_VIRTUAL_BIND(_get_import_options, "path", "preset_index")
	ORBLIT_VIRTUAL_BIND(_get_save_extension)
	ORBLIT_VIRTUAL_BIND(_get_resource_type)
	ORBLIT_VIRTUAL_BIND(_get_priority)
	ORBLIT_VIRTUAL_BIND(_get_import_order)
	ORBLIT_VIRTUAL_BIND(_get_format_version)
	ORBLIT_VIRTUAL_BIND(_get_option_visibility, "path", "option_name", "options")
	ORBLIT_VIRTUAL_BIND(_import, "source_file", "save_path", "options", "platform_variants", "gen_files");
	ORBLIT_VIRTUAL_BIND(_can_import_threaded);
	ClassRegistry::bind_method(D_METHOD("append_import_external_resource", "path", "custom_options", "custom_importer", "generator_parameters"), &EditorImportPlugin::_append_import_external_resource, DEFVAL(Dictionary()), DEFVAL(String()), DEFVAL(Variant()));
}
