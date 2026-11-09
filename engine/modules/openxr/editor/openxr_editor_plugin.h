/**************************************************************************/
/*  openxr_editor_plugin.h                                                */

#pragma once

#include "openxr_action_map_editor.h"
#include "openxr_binding_modifier_editor.h"
#include "openxr_select_runtime.h"

#include "editor/export/editor_export_plugin.h"
#include "editor/plugins/editor_plugin.h"

class OpenXRExportPlugin : public EditorExportPlugin {
	ORBLIT_CLASS(OpenXRExportPlugin, EditorExportPlugin)

public:
	virtual String get_name() const override { return "OpenXRExportPlugin"; }
	virtual bool supports_platform(const Ref<EditorExportPlatform> &p_export_platform) const override;
	virtual PackedStringArray get_android_dependencies(const Ref<EditorExportPlatform> &p_export_platform, bool p_debug) const override;

protected:
	virtual String _get_export_option_warning(const Ref<EditorExportPlatform> &p_export_platform, const String &p_option_name) const override;

	virtual PackedStringArray _get_export_features(const Ref<EditorExportPlatform> &p_export_platform, bool p_debug) const override;
	virtual String get_android_manifest_element_contents(const Ref<EditorExportPlatform> &p_export_platform, bool p_debug) const override;
	virtual String get_android_manifest_activity_element_contents(const Ref<EditorExportPlatform> &p_export_platform, bool p_debug) const override;

private:
	bool is_openxr_mode() const;
	bool is_spatial_container_enabled() const;
};

class OpenXREditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(OpenXREditorPlugin, EditorPlugin);

	OpenXRActionMapEditor *action_map_editor = nullptr;
	Ref<EditorInspectorPluginBindingModifier> binding_modifier_inspector_plugin = nullptr;
#ifndef ANDROID_ENABLED
	OpenXRSelectRuntime *select_runtime = nullptr;
#endif

public:
	virtual String get_plugin_name() const override { return "OpenXRPlugin"; }
	virtual void edit(Object *p_node) override;
	virtual bool handles(Object *p_node) const override;
	virtual void make_visible(bool p_visible) override;

	OpenXREditorPlugin();

protected:
	void _notification(int p_what);

private:
	Ref<OpenXRExportPlugin> openxr_export_plugin;
};
