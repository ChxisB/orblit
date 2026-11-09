/**************************************************************************/
/*  editor_export_platform_pc.h                                           */

#pragma once

#include "editor/export/editor_export_platform.h"

class ImageTexture;

class EditorExportPlatformPC : public EditorExportPlatform {
	ORBLIT_CLASS(EditorExportPlatformPC, EditorExportPlatform);

private:
	Ref<ImageTexture> logo;
	String name;
	String os_name;

	int chmod_flags = -1;

	Error _unzip_debugsymbols(Ref<DirAccess> &p_da, const String &p_path, const String &p_symbols_path, const String &p_zip_path);
	bool _copy_debugsymbols(Ref<DirAccess> &p_da, const String &p_path, const String &p_symbols_path, Error &r_err);

public:
	virtual void get_preset_features(const Ref<EditorExportPreset> &p_preset, List<String> *r_features) const override;
	virtual void get_export_options(List<ExportOption> *r_options) const override;
	virtual String get_export_option_warning(const EditorExportPreset *p_preset, const StringName &p_name) const override;

	virtual String get_name() const override;
	virtual String get_os_name() const override;
	virtual Ref<Texture2D> get_logo() const override;

	virtual bool has_valid_export_configuration(const Ref<EditorExportPreset> &p_preset, String &r_error, bool &r_missing_templates, bool p_debug = false) const override;
	virtual bool has_valid_project_configuration(const Ref<EditorExportPreset> &p_preset, String &r_error) const override;
	virtual Error export_project(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, BitField<EditorExportPlatform::DebugFlags> p_flags = 0, bool p_notify = true) override;
	virtual Error sign_shared_object(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path);
	virtual String get_template_file_name(const String &p_target, const String &p_arch) const = 0;

	virtual Error prepare_template(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, BitField<EditorExportPlatform::DebugFlags> p_flags);
	virtual Error modify_template(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, BitField<EditorExportPlatform::DebugFlags> p_flags) { return OK; }
	virtual Error export_project_data(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, BitField<EditorExportPlatform::DebugFlags> p_flags);

	void set_name(const String &p_name);
	void set_os_name(const String &p_name);

	void set_logo(const Ref<Texture2D> &p_logo);

	void add_platform_feature(const String &p_feature);
	virtual void get_platform_features(List<String> *r_features) const override;
	virtual void resolve_platform_feature_priorities(const Ref<EditorExportPreset> &p_preset, HashSet<String> &p_features) override;

	int get_chmod_flags() const;
	void set_chmod_flags(int p_flags);

	virtual Error fixup_embedded_pck(const String &p_path, int64_t p_embedded_start, int64_t p_embedded_size) {
		return Error::OK;
	}
	virtual Error fixup_debug_symbol_link(const String &p_path, const String &p_symbol_path) {
		return Error::OK;
	}
};
