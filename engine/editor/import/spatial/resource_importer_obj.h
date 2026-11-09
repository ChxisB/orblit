/**************************************************************************/
/*  resource_importer_obj.h                                               */

#pragma once

#include "editor/import/spatial/resource_importer_scene.h"

class EditorOBJImporter : public EditorSceneFormatImporter {
	ORBLIT_CLASS(EditorOBJImporter, EditorSceneFormatImporter);

public:
	virtual void get_extensions(List<String> *r_extensions) const override;
	virtual Entity *import_scene(const String &p_path, uint32_t p_flags, const HashMap<StringName, Variant> &p_options, List<String> *r_missing_deps, Error *r_err = nullptr) override;
};

class ResourceImporterOBJ : public ResourceImporter {
	ORBLIT_CLASS(ResourceImporterOBJ, ResourceImporter);

public:
	virtual String get_importer_name() const override;
	virtual String get_visible_name() const override;
	virtual void get_recognized_extensions(List<String> *p_extensions) const override;
	virtual String get_save_extension() const override;
	virtual String get_resource_type() const override;
	virtual int get_format_version() const override;

	virtual int get_preset_count() const override;
	virtual String get_preset_name(int p_idx) const override;

	virtual void get_import_options(const String &p_path, List<ImportOption> *r_options, int p_preset = 0) const override;
	virtual bool get_option_visibility(const String &p_path, const String &p_option, const HashMap<StringName, Variant> &p_options) const override;

	virtual Error import(ResourceUID::ID p_source_id, const String &p_source_file, const String &p_save_path, const HashMap<StringName, Variant> &p_options, List<String> *r_platform_variants, List<String> *r_gen_files = nullptr, Variant *r_metadata = nullptr) override;
};
