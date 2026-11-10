/**************************************************************************/
/*  editor_scene_exporter_gltf_plugin.h                                   */

#pragma once

#include "../gltf_document.h"
#include "editor_scene_exporter_gltf_settings.h"

#include "editor/plugins/editor_plugin.h"

class ConfirmationDialog;
class EditorFileDialog;
class EditorInspector;

class SceneExporterGLTFPlugin : public EditorPlugin {
	ORBLIT_CLASS(SceneExporterGLTFPlugin, EditorPlugin);

	Ref<GLTFDocument> _gltf_document;
	Ref<EditorSceneExporterGLTFSettings> _export_settings;
	String export_path;

	EditorInspector *_settings_inspector = nullptr;
	ConfirmationDialog *_config_dialog = nullptr;
	EditorFileDialog *_file_dialog = nullptr;

	void _popup_gltf_settings_dialog(const String &p_selected_path);
	void _popup_gltf_export_dialog();
	void _export_scene_as_gltf();

public:
	virtual String get_plugin_name() const override;
	SceneExporterGLTFPlugin();
};
