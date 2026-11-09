/**************************************************************************/
/*  dedicated_server_export_plugin.h                                      */

#pragma once

#include "editor/export/editor_export_plugin.h"

class DedicatedServerExportPlugin : public EditorExportPlugin {
	ORBLIT_SOFTCLASS(DedicatedServerExportPlugin, EditorExportPlugin);

private:
	EditorExportPreset::FileExportMode current_export_mode;

	EditorExportPreset::FileExportMode _get_export_mode_for_path(const String &p_path);

protected:
	String get_name() const override { return "DedicatedServer"; }

	PackedStringArray _get_export_features(const Ref<EditorExportPlatform> &p_platform, bool p_debug) const override;
	uint64_t _get_customization_configuration_hash() const override;

	bool _begin_customize_scenes(const Ref<EditorExportPlatform> &p_platform, const Vector<String> &p_features) override;
	bool _begin_customize_resources(const Ref<EditorExportPlatform> &p_platform, const Vector<String> &p_features) override;

	Entity *_customize_scene(Entity *p_root, const String &p_path) override;
	Ref<Resource> _customize_resource(const Ref<Resource> &p_resource, const String &p_path) override;

	void _end_customize_scenes() override;
	void _end_customize_resources() override;
};
