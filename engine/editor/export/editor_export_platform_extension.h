/**************************************************************************/
/*  editor_export_platform_extension.h                                    */

#pragma once

#include "core/object/orblit_virtual.gen.h"
#include "core/variant/typed_array.h"
#include "editor/export/editor_export_platform.h"
#include "editor/export/editor_export_preset.h"

class ImageTexture;

class EditorExportPlatformExtension : public EditorExportPlatform {
	ORBLIT_CLASS(EditorExportPlatformExtension, EditorExportPlatform);

	mutable String config_error;
	mutable bool config_missing_templates = false;

protected:
	static void register_members();

public:
	virtual void get_preset_features(const Ref<EditorExportPreset> &p_preset, List<String> *r_features) const override;
	ORBLIT_VIRTUAL1RC_REQUIRED(Vector<String>, _get_preset_features, Ref<EditorExportPreset>);

	virtual bool is_executable(const String &p_path) const override;
	ORBLIT_VIRTUAL1RC(bool, _is_executable, const String &);

	virtual void get_export_options(List<ExportOption> *r_options) const override;
	ORBLIT_VIRTUAL0RC(TypedArray<Dictionary>, _get_export_options);

	virtual bool should_update_export_options() override;
	ORBLIT_VIRTUAL0R(bool, _should_update_export_options);

	virtual bool get_export_option_visibility(const EditorExportPreset *p_preset, const String &p_option) const override;
	ORBLIT_VIRTUAL2RC(bool, _get_export_option_visibility, Ref<EditorExportPreset>, const String &);

	virtual String get_export_option_warning(const EditorExportPreset *p_preset, const StringName &p_name) const override;
	ORBLIT_VIRTUAL2RC(String, _get_export_option_warning, Ref<EditorExportPreset>, const StringName &);

	virtual String get_os_name() const override;
	ORBLIT_VIRTUAL0RC_REQUIRED(String, _get_os_name);

	virtual String get_name() const override;
	ORBLIT_VIRTUAL0RC_REQUIRED(String, _get_name);

	virtual Ref<Texture2D> get_logo() const override;
	ORBLIT_VIRTUAL0RC_REQUIRED(Ref<Texture2D>, _get_logo);

	virtual bool poll_export() override;
	ORBLIT_VIRTUAL0R(bool, _poll_export);

	virtual int get_options_count() const override;
	ORBLIT_VIRTUAL0RC(int, _get_options_count);

	virtual String get_options_tooltip() const override;
	ORBLIT_VIRTUAL0RC(String, _get_options_tooltip);

	virtual Ref<Texture2D> get_option_icon(int p_index) const override;
	ORBLIT_VIRTUAL1RC(Ref<Texture2D>, _get_option_icon, int);

	virtual String get_option_label(int p_device) const override;
	ORBLIT_VIRTUAL1RC(String, _get_option_label, int);

	virtual String get_option_tooltip(int p_device) const override;
	ORBLIT_VIRTUAL1RC(String, _get_option_tooltip, int);

	virtual String get_device_architecture(int p_device) const override;
	ORBLIT_VIRTUAL1RC(String, _get_device_architecture, int);

	virtual void cleanup() override;
	ORBLIT_VIRTUAL0(_cleanup);

	virtual Error run(const Ref<EditorExportPreset> &p_preset, int p_device, BitField<EditorExportPlatform::DebugFlags> p_debug_flags) override;
	ORBLIT_VIRTUAL3R(Error, _run, Ref<EditorExportPreset>, int, BitField<EditorExportPlatform::DebugFlags>);

	virtual Ref<Texture2D> get_run_icon() const override;
	ORBLIT_VIRTUAL0RC(Ref<Texture2D>, _get_run_icon);

	void set_config_error(const String &p_error) const {
		config_error = p_error;
	}
	String get_config_error() const {
		return config_error;
	}

	void set_config_missing_templates(bool p_missing_templates) const {
		config_missing_templates = p_missing_templates;
	}
	bool get_config_missing_templates() const {
		return config_missing_templates;
	}

	virtual bool can_export(const Ref<EditorExportPreset> &p_preset, String &r_error, bool &r_missing_templates, bool p_debug = false) const override;
	ORBLIT_VIRTUAL2RC(bool, _can_export, Ref<EditorExportPreset>, bool);

	virtual bool has_valid_export_configuration(const Ref<EditorExportPreset> &p_preset, String &r_error, bool &r_missing_templates, bool p_debug = false) const override;
	ORBLIT_VIRTUAL2RC_REQUIRED(bool, _has_valid_export_configuration, Ref<EditorExportPreset>, bool);

	virtual bool has_valid_project_configuration(const Ref<EditorExportPreset> &p_preset, String &r_error) const override;
	ORBLIT_VIRTUAL1RC_REQUIRED(bool, _has_valid_project_configuration, Ref<EditorExportPreset>);

	virtual List<String> get_binary_extensions(const Ref<EditorExportPreset> &p_preset) const override;
	ORBLIT_VIRTUAL1RC_REQUIRED(Vector<String>, _get_binary_extensions, Ref<EditorExportPreset>);

	virtual Error export_project(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, BitField<EditorExportPlatform::DebugFlags> p_flags = 0, bool p_notify = true) override;
	ORBLIT_VIRTUAL4R_REQUIRED(Error, _export_project, Ref<EditorExportPreset>, bool, const String &, BitField<EditorExportPlatform::DebugFlags>);

	virtual Error export_pack(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, BitField<EditorExportPlatform::DebugFlags> p_flags = 0) override;
	ORBLIT_VIRTUAL4R(Error, _export_pack, Ref<EditorExportPreset>, bool, const String &, BitField<EditorExportPlatform::DebugFlags>);

	virtual Error export_zip(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, BitField<EditorExportPlatform::DebugFlags> p_flags = 0) override;
	ORBLIT_VIRTUAL4R(Error, _export_zip, Ref<EditorExportPreset>, bool, const String &, BitField<EditorExportPlatform::DebugFlags>);

	virtual Error export_pack_patch(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, const Vector<String> &p_patches = Vector<String>(), BitField<EditorExportPlatform::DebugFlags> p_flags = 0) override;
	ORBLIT_VIRTUAL5R(Error, _export_pack_patch, Ref<EditorExportPreset>, bool, const String &, const Vector<String> &, BitField<EditorExportPlatform::DebugFlags>);

	virtual Error export_zip_patch(const Ref<EditorExportPreset> &p_preset, bool p_debug, const String &p_path, const Vector<String> &p_patches = Vector<String>(), BitField<EditorExportPlatform::DebugFlags> p_flags = 0) override;
	ORBLIT_VIRTUAL5R(Error, _export_zip_patch, Ref<EditorExportPreset>, bool, const String &, const Vector<String> &, BitField<EditorExportPlatform::DebugFlags>);

	virtual void get_platform_features(List<String> *r_features) const override;
	ORBLIT_VIRTUAL0RC_REQUIRED(Vector<String>, _get_platform_features);

	virtual String get_debug_protocol() const override;
	ORBLIT_VIRTUAL0RC(String, _get_debug_protocol);

	virtual void initialize() override;
	ORBLIT_VIRTUAL0(_initialize);

	EditorExportPlatformExtension();
	~EditorExportPlatformExtension();
};
