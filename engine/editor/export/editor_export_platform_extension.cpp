/**************************************************************************/
/*  editor_export_platform_extension.cpp                                  */

#include "editor_export_platform_extension.h"

#include "core/object/class_registry.h"
#include "scene/resources/image_texture.h"

void EditorExportPlatformExtension::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_config_error", "error_text"), &EditorExportPlatformExtension::set_config_error);
	ClassRegistry::bind_method(D_METHOD("get_config_error"), &EditorExportPlatformExtension::get_config_error);

	ClassRegistry::bind_method(D_METHOD("set_config_missing_templates", "missing_templates"), &EditorExportPlatformExtension::set_config_missing_templates);
	ClassRegistry::bind_method(D_METHOD("get_config_missing_templates"), &EditorExportPlatformExtension::get_config_missing_templates);

	ORBLIT_VIRTUAL_BIND(_get_preset_features, "preset");
	ORBLIT_VIRTUAL_BIND(_is_executable, "path");
	ORBLIT_VIRTUAL_BIND(_get_export_options);
	ORBLIT_VIRTUAL_BIND(_should_update_export_options);
	ORBLIT_VIRTUAL_BIND(_get_export_option_visibility, "preset", "option");
	ORBLIT_VIRTUAL_BIND(_get_export_option_warning, "preset", "option");

	ORBLIT_VIRTUAL_BIND(_get_os_name);
	ORBLIT_VIRTUAL_BIND(_get_name);
	ORBLIT_VIRTUAL_BIND(_get_logo);

	ORBLIT_VIRTUAL_BIND(_poll_export);
	ORBLIT_VIRTUAL_BIND(_get_options_count);
	ORBLIT_VIRTUAL_BIND(_get_options_tooltip);

	ORBLIT_VIRTUAL_BIND(_get_option_icon, "device");

	ORBLIT_VIRTUAL_BIND(_get_option_label, "device");
	ORBLIT_VIRTUAL_BIND(_get_option_tooltip, "device");
	ORBLIT_VIRTUAL_BIND(_get_device_architecture, "device");

	ORBLIT_VIRTUAL_BIND(_cleanup);

	ORBLIT_VIRTUAL_BIND(_run, "preset", "device", "debug_flags");
	ORBLIT_VIRTUAL_BIND(_get_run_icon);

	ORBLIT_VIRTUAL_BIND(_can_export, "preset", "debug");
	ORBLIT_VIRTUAL_BIND(_has_valid_export_configuration, "preset", "debug");
	ORBLIT_VIRTUAL_BIND(_has_valid_project_configuration, "preset");

	ORBLIT_VIRTUAL_BIND(_get_binary_extensions, "preset");

	ORBLIT_VIRTUAL_BIND(_export_project, "preset", "debug", "path", "flags");
	ORBLIT_VIRTUAL_BIND(_export_pack, "preset", "debug", "path", "flags");
	ORBLIT_VIRTUAL_BIND(_export_zip, "preset", "debug", "path", "flags");
	ORBLIT_VIRTUAL_BIND(_export_pack_patch, "preset", "debug", "path", "patches", "flags");
	ORBLIT_VIRTUAL_BIND(_export_zip_patch, "preset", "debug", "path", "patches", "flags");

	ORBLIT_VIRTUAL_BIND(_get_platform_features);

	ORBLIT_VIRTUAL_BIND(_get_debug_protocol);

	ORBLIT_VIRTUAL_BIND(_initialize);
}

void EditorExportPlatformExtension::get_preset_features(const Ref<EditorExportPreset> &p_preset, List<String> *r_features) const {
	Vector<String> ret;
	if (ORBLIT_VIRTUAL_CALL(_get_preset_features, p_preset, ret) && r_features) {
		for (const String &E : ret) {
			r_features->push_back(E);
		}
	}
}

bool EditorExportPlatformExtension::is_executable(const String &p_path) const {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_is_executable, p_path, ret);
	return ret;
}

void EditorExportPlatformExtension::get_export_options(List<ExportOption> *r_options) const {
	TypedArray<Dictionary> ret;
	if (ORBLIT_VIRTUAL_CALL(_get_export_options, ret) && r_options) {
		for (const Variant &var : ret) {
			const Dictionary &d = var;
			ERR_CONTINUE(!d.has("name"));
			ERR_CONTINUE(!d.has("type"));

			PropertyInfo pinfo = PropertyInfo::from_dict(d);
			ERR_CONTINUE(pinfo.name.is_empty() && (pinfo.usage & PROPERTY_USAGE_STORAGE));
			ERR_CONTINUE(pinfo.type < 0 || pinfo.type >= Variant::VARIANT_MAX);

			Variant default_value;
			if (d.has("default_value")) {
				default_value = d["default_value"];
			}
			bool update_visibility = false;
			if (d.has("update_visibility")) {
				update_visibility = d["update_visibility"];
			}
			bool required = false;
			if (d.has("required")) {
				required = d["required"];
			}

			r_options->push_back(ExportOption(pinfo, default_value, update_visibility, required));
		}
	}
}

bool EditorExportPlatformExtension::should_update_export_options() {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_should_update_export_options, ret);
	return ret;
}

bool EditorExportPlatformExtension::get_export_option_visibility(const EditorExportPreset *p_preset, const String &p_option) const {
	bool ret = true;
	ORBLIT_VIRTUAL_CALL(_get_export_option_visibility, Ref<EditorExportPreset>(p_preset), p_option, ret);
	return ret;
}

String EditorExportPlatformExtension::get_export_option_warning(const EditorExportPreset *p_preset, const StringName &p_name) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_export_option_warning, Ref<EditorExportPreset>(p_preset), p_name, ret);
	return ret;
}

String EditorExportPlatformExtension::get_os_name() const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_os_name, ret);
	return ret;
}

String EditorExportPlatformExtension::get_name() const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_name, ret);
	return ret;
}

Ref<Texture2D> EditorExportPlatformExtension::get_logo() const {
	Ref<Texture2D> ret;
	ORBLIT_VIRTUAL_CALL(_get_logo, ret);
	return ret;
}

bool EditorExportPlatformExtension::poll_export() {
	bool ret = false;
	ORBLIT_VIRTUAL_CALL(_poll_export, ret);
	return ret;
}

int EditorExportPlatformExtension::get_options_count() const {
	int ret = 0;
	ORBLIT_VIRTUAL_CALL(_get_options_count, ret);
	return ret;
}

String EditorExportPlatformExtension::get_options_tooltip() const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_options_tooltip, ret);
	return ret;
}

Ref<Texture2D> EditorExportPlatformExtension::get_option_icon(int p_index) const {
	Ref<Texture2D> ret;
	if (ORBLIT_VIRTUAL_CALL(_get_option_icon, p_index, ret)) {
		return ret;
	}
	return EditorExportPlatform::get_option_icon(p_index);
}

String EditorExportPlatformExtension::get_option_label(int p_device) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_option_label, p_device, ret);
	return ret;
}

String EditorExportPlatformExtension::get_option_tooltip(int p_device) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_option_tooltip, p_device, ret);
	return ret;
}

String EditorExportPlatformExtension::get_device_architecture(int p_device) const {
	String ret;
	ORBLIT_VIRTUAL_CALL(_get_device_architecture, p_device, ret);
	return ret;
}

void EditorExportPlatformExtension::cleanup() {
	ORBLIT_VIRTUAL_CALL(_cleanup);
}

Error EditorExportPlatformExtension::run(const Ref<EditorExportPreset> &p_preset, int p_device, BitField<EditorExportPlatform::DebugFlags> p_debug_flags) {
	Error ret = OK;
	ORBLIT_VIRTUAL_CALL(_run, p_preset, p_device, p_debug_flags, ret);
	return ret;
}

Ref<Texture2D> EditorExportPlatformExtension::get_run_icon() const {
	Ref<Texture2D> ret;
	if (ORBLIT_VIRTUAL_CALL(_get_run_icon, ret)) {
		return ret;
	}
	return EditorExportPlatform::get_run_icon();
}

bool EditorExportPlatformExtension::can_export(const Ref<EditorExportPreset> &p_preset, String &r_error, bool &r_missing_templates, bool p_debug) const {
	bool ret = false;
	config_error = r_error;
	config_missing_templates = r_missing_templates;
	if (ORBLIT_VIRTUAL_CALL(_can_export, p_preset, p_debug, ret)) {
		r_error = config_error;
		r_missing_templates = config_missing_templates;
		return ret;
	}
	return EditorExportPlatform::can_export(p_preset, r_error, r_missing_templates, p_debug);
}

bool EditorExportPlatformExtension::has_valid_export_configuration(const Ref<EditorExportPreset> &p_preset, String &r_error, bool &r_missing_templates, bool p_debug) const {
	bool ret = false;
	config_error = r_error;
	config_missing_templates = r_missing_templates;
	if (ORBLIT_VIRTUAL_CALL(_has_valid_export_configuration, p_preset, p_debug, ret)) {
		r_error = config_error;
		r_missing_templates = config_missing_templates;
	}
	return ret;
}

bool EditorExportPlatformExtension::has_valid_project_configuration(const Ref<EditorExportPreset> &p_preset, String &r_error) const {
	bool ret = false;
	config_error = r_error;
	if (ORBLIT_VIRTUAL_CALL(_has_valid_project_configuration, p_preset, ret)) {
		r_error = config_error;
	}
	return ret;
}

List<String> EditorExportPlatformExtension::get_binary_extensions(const Ref<EditorExportPreset> &p_preset) const {
	List<String> ret_list;
	Vector<String> ret;
	if (ORBLIT_VIRTUAL_CALL(_get_binary_extensions, p_preset, ret)) {
		for (const String &E : ret) {
			ret_list.push_back(E);
		}
	}
	return ret_list;
}

Error EditorExportPlatformExtension::export_project(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, BitField<EditorExportPlatform::DebugFlags> p_flags, bool p_notify) {
	ExportNotifier notifier(*this, p_preset, p_debug, p_path, p_flags, p_notify);

	Error ret = FAILED;
	ORBLIT_VIRTUAL_CALL(_export_project, p_preset, p_debug, p_path, p_flags, ret);
	return ret;
}

Error EditorExportPlatformExtension::export_pack(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, BitField<EditorExportPlatform::DebugFlags> p_flags) {
	ExportNotifier notifier(*this, p_preset, p_debug, p_path, p_flags);

	Error ret = FAILED;
	if (ORBLIT_VIRTUAL_CALL(_export_pack, p_preset, p_debug, p_path, p_flags, ret)) {
		return ret;
	}
	return save_pack(p_preset, p_debug, p_path);
}

Error EditorExportPlatformExtension::export_zip(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, BitField<EditorExportPlatform::DebugFlags> p_flags) {
	ExportNotifier notifier(*this, p_preset, p_debug, p_path, p_flags);

	Error ret = FAILED;
	if (ORBLIT_VIRTUAL_CALL(_export_zip, p_preset, p_debug, p_path, p_flags, ret)) {
		return ret;
	}
	return save_zip(p_preset, p_debug, p_path);
}

Error EditorExportPlatformExtension::export_pack_patch(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, const Vector<String> &p_patches, BitField<EditorExportPlatform::DebugFlags> p_flags) {
	ExportNotifier notifier(*this, p_preset, p_debug, p_path, p_flags);

	RETURN_IF_ERROR(_load_patches(p_preset, p_patches.is_empty() ? p_preset->get_patches() : p_patches));

	Error ret = FAILED;
	if (ORBLIT_VIRTUAL_CALL(_export_pack_patch, p_preset, p_debug, p_path, p_patches, p_flags, ret)) {
		_unload_patches();
		return ret;
	}

	Error err = save_pack_patch(p_preset, p_debug, p_path);
	_unload_patches();
	return err;
}

Error EditorExportPlatformExtension::export_zip_patch(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, const Vector<String> &p_patches, BitField<EditorExportPlatform::DebugFlags> p_flags) {
	ExportNotifier notifier(*this, p_preset, p_debug, p_path, p_flags);

	RETURN_IF_ERROR(_load_patches(p_preset, p_patches.is_empty() ? p_preset->get_patches() : p_patches));

	Error ret = FAILED;
	if (ORBLIT_VIRTUAL_CALL(_export_zip_patch, p_preset, p_debug, p_path, p_patches, p_flags, ret)) {
		_unload_patches();
		return ret;
	}

	Error err = save_zip_patch(p_preset, p_debug, p_path);
	_unload_patches();
	return err;
}

void EditorExportPlatformExtension::get_platform_features(List<String> *r_features) const {
	Vector<String> ret;
	if (ORBLIT_VIRTUAL_CALL(_get_platform_features, ret) && r_features) {
		for (const String &E : ret) {
			r_features->push_back(E);
		}
	}
}

String EditorExportPlatformExtension::get_debug_protocol() const {
	String ret;
	if (ORBLIT_VIRTUAL_CALL(_get_debug_protocol, ret)) {
		return ret;
	}
	return EditorExportPlatform::get_debug_protocol();
}

void EditorExportPlatformExtension::initialize() {
	ORBLIT_VIRTUAL_CALL(_initialize);
}

EditorExportPlatformExtension::EditorExportPlatformExtension() {
	//NOP
}

EditorExportPlatformExtension::~EditorExportPlatformExtension() {
	//NOP
}
