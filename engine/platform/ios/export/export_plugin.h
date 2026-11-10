/**************************************************************************/
/*  export_plugin.h                                                       */

#pragma once

#include "editor/export/editor_export_platform_apple_embedded.h"

class EditorExportPlatformIOS : public EditorExportPlatformAppleEmbedded {
	ORBLIT_CLASS(EditorExportPlatformIOS, EditorExportPlatformAppleEmbedded);

	static Vector<String> device_types;

	virtual String get_platform_name() const override { return "ios"; }
	virtual String get_sdk_name() const override { return "iphoneos"; }
	virtual const Vector<String> get_device_types() const override { return device_types; }

	virtual String get_minimum_deployment_target() const override { return "15.0"; }

	virtual Vector<IconInfo> get_icon_infos() const override;
	virtual void get_required_device_capabilities(const Ref<EditorExportPreset> &p_preset, Vector<String> &r_capabilities) const override;

	virtual void get_export_options(List<ExportOption> *r_options) const override;
	virtual bool has_valid_export_configuration(const Ref<EditorExportPreset> &p_preset, String &r_error, bool &r_missing_templates, bool p_debug = false) const override;
	virtual bool get_export_option_visibility(const EditorExportPreset *p_preset, const String &p_option) const override;

	virtual Error _export_loading_screen_file(const Ref<EditorExportPreset> &p_preset, const String &p_dest_dir) override;
	virtual HashMap<String, Variant> get_custom_project_settings(const Ref<EditorExportPreset> &p_preset) const override;

	virtual String _process_config_file_line(const Ref<EditorExportPreset> &p_preset, const String &p_line, const AppleEmbeddedConfigData &p_config, bool p_debug, const CodeSigningDetails &p_code_signing) override;

public:
	virtual String get_name() const override { return "iOS"; }
	virtual String get_os_name() const override { return "iOS"; }

	virtual void get_platform_features(List<String> *r_features) const override {
		EditorExportPlatformAppleEmbedded::get_platform_features(r_features);
		r_features->push_back("ios");
	}

	virtual void initialize() override;
	~EditorExportPlatformIOS();
};
