/**************************************************************************/
/*  resource_importer_dynamic_font.h                                      */

#pragma once

#include "core/io/resource_importer.h"

class ResourceImporterDynamicFont : public ResourceImporter {
	ORBLIT_CLASS(ResourceImporterDynamicFont, ResourceImporter);

	enum Presets {
		PRESET_DYNAMIC,
		PRESET_MSDF,
		PRESET_MAX
	};

public:
	virtual String get_importer_name() const override;
	virtual String get_visible_name() const override;
	virtual void get_recognized_extensions(List<String> *p_extensions) const override;
	virtual String get_save_extension() const override;
	virtual String get_resource_type() const override;
	virtual void get_build_dependencies(const String &p_path, HashSet<String> *r_build_dependencies) override;

	virtual int get_preset_count() const override;
	virtual String get_preset_name(int p_idx) const override;

	virtual void get_import_options(const String &p_path, List<ImportOption> *r_options, int p_preset = 0) const override;
	virtual bool get_option_visibility(const String &p_path, const String &p_option, const HashMap<StringName, Variant> &p_options) const override;

	bool has_advanced_options() const override;
	void show_advanced_options(const String &p_path) override;

	virtual Error import(ResourceUID::ID p_source_id, const String &p_source_file, const String &p_save_path, const HashMap<StringName, Variant> &p_options, List<String> *r_platform_variants, List<String> *r_gen_files = nullptr, Variant *r_metadata = nullptr) override;

	virtual bool can_import_threaded() const override { return true; }
};
