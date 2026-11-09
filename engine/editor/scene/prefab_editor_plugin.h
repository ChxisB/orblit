/**************************************************************************/
/*  prefab_editor_plugin.h                                                */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin.h"
#include "scene/ui/box_container.h"

class Prefab;

class PrefabEditor : public VBoxContainer {
	ORBLIT_CLASS(PrefabEditor, VBoxContainer);

	Ref<Prefab> prefab;

	void _on_open_scene_pressed();

public:
	PrefabEditor(Ref<Prefab> &p_prefab);
};

class EditorInspectorPluginPrefab : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginPrefab, EditorInspectorPlugin);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_begin(Object *p_object) override;
};

class PrefabEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(PrefabEditorPlugin, EditorPlugin);

public:
	PrefabEditorPlugin();
};
