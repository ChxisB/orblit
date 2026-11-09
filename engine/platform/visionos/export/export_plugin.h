/**************************************************************************/
/*  export_plugin.h                                                       */

#pragma once

#include "editor/export/editor_export_platform_apple_embedded.h"

class EditorExportPlatformVisionOS : public EditorExportPlatformAppleEmbedded {
	ORBLIT_CLASS(EditorExportPlatformVisionOS, EditorExportPlatformAppleEmbedded);

	static Vector<String> device_types;

	virtual String get_platform_name() const override { return "visionos"; }
	virtual String get_sdk_name() const override { return "xros"; }
	virtual const Vector<String> get_device_types() const override { return device_types; }

	virtual String get_minimum_deployment_target() const override { return "26.0"; }

	virtual Vector<EditorExportPlatformAppleEmbedded::IconInfo> get_icon_infos() const override;

	// visionOS uses a layered .solidimagestack with three .solidimagestacklayer dirs.
	virtual String _get_iconset_dir_name() const override { return "AppIcon.solidimagestack"; }
	virtual Error _export_icons(const Ref<EditorExportPreset> &p_preset, const String &p_iconset_dir) override;

	virtual void get_export_options(List<ExportOption> *r_options) const override;

	virtual String _process_config_file_line(const Ref<EditorExportPreset> &p_preset, const String &p_line, const AppleEmbeddedConfigData &p_config, bool p_debug, const CodeSigningDetails &p_code_signing) override;

	virtual void get_usage_descriptions(List<UsageDescription> *r_descriptions) const override;

public:
	virtual String get_name() const override { return "visionOS"; }
	virtual String get_os_name() const override { return "visionOS"; }

	virtual void get_platform_features(List<String> *r_features) const override {
		EditorExportPlatformAppleEmbedded::get_platform_features(r_features);
		r_features->push_back("visionos");
	}

	virtual void initialize() override;
	~EditorExportPlatformVisionOS();
};
