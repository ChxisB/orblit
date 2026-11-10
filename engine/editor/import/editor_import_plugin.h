/**************************************************************************/
/*  editor_import_plugin.h                                                */

#pragma once

#include "core/io/resource_importer.h"
#include "core/variant/typed_array.h"

class EditorImportPlugin : public ResourceImporter {
	ORBLIT_CLASS(EditorImportPlugin, ResourceImporter);

protected:
	static void register_members();

	ORBLIT_VIRTUAL0RC_REQUIRED(String, _get_importer_name)
	ORBLIT_VIRTUAL0RC_REQUIRED(String, _get_visible_name)
	ORBLIT_VIRTUAL0RC(int, _get_preset_count)
	ORBLIT_VIRTUAL1RC_REQUIRED(String, _get_preset_name, int)
	ORBLIT_VIRTUAL0RC_REQUIRED(Vector<String>, _get_recognized_extensions)
	ORBLIT_VIRTUAL2RC_REQUIRED(TypedArray<Dictionary>, _get_import_options, String, int)
	ORBLIT_VIRTUAL0RC_REQUIRED(String, _get_save_extension)
	ORBLIT_VIRTUAL0RC_REQUIRED(String, _get_resource_type)
	ORBLIT_VIRTUAL0RC(float, _get_priority)
	ORBLIT_VIRTUAL0RC(int, _get_import_order)
	ORBLIT_VIRTUAL0RC(int, _get_format_version)
	ORBLIT_VIRTUAL3RC(bool, _get_option_visibility, String, StringName, Dictionary)
	ORBLIT_VIRTUAL5RC_REQUIRED(Error, _import, String, String, Dictionary, TypedArray<String>, TypedArray<String>)
	ORBLIT_VIRTUAL0RC(bool, _can_import_threaded)

	Error _append_import_external_resource(const String &p_file, const Dictionary &p_custom_options = Dictionary(), const String &p_custom_importer = String(), Variant p_generator_parameters = Variant());

public:
	virtual String get_importer_name() const override;
	virtual String get_visible_name() const override;
	virtual void get_recognized_extensions(List<String> *p_extensions) const override;
	virtual String get_preset_name(int p_idx) const override;
	virtual int get_preset_count() const override;
	virtual String get_save_extension() const override;
	virtual String get_resource_type() const override;
	virtual float get_priority() const override;
	virtual int get_import_order() const override;
	virtual int get_format_version() const override;
	virtual void get_import_options(const String &p_path, List<ImportOption> *r_options, int p_preset) const override;
	virtual bool get_option_visibility(const String &p_path, const String &p_option, const HashMap<StringName, Variant> &p_options) const override;
	virtual Error import(ResourceUID::ID p_source_id, const String &p_source_file, const String &p_save_path, const HashMap<StringName, Variant> &p_options, List<String> *r_platform_variants, List<String> *r_gen_files, Variant *r_metadata = nullptr) override;
	virtual bool can_import_threaded() const override;
	Error append_import_external_resource(const String &p_file, const HashMap<StringName, Variant> &p_custom_options = HashMap<StringName, Variant>(), const String &p_custom_importer = String(), Variant p_generator_parameters = Variant());
};
