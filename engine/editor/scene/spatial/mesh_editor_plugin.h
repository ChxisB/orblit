/**************************************************************************/
/*  mesh_editor_plugin.h                                                  */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin.h"
#include "scene/spatial/camera_spatial.h"
#include "scene/spatial/light_spatial.h"
#include "scene/spatial/mesh_instance_spatial.h"
#include "scene/ui/subviewport_container.h"
#include "scene/resources/camera_attributes.h"

class SubViewport;
class Button;

class MeshEditor : public SubViewportContainer {
	ORBLIT_CLASS(MeshEditor, SubViewportContainer);

	float rot_x;
	float rot_y;

	SubViewport *viewport = nullptr;
	MeshInstance3D *mesh_instance = nullptr;
	Entity3D *rotation = nullptr;
	DirectionalLight3D *light1 = nullptr;
	DirectionalLight3D *light2 = nullptr;
	Camera3D *camera = nullptr;
	Ref<CameraAttributesPractical> camera_attributes;

	Ref<Mesh> mesh;

	Button *light_1_switch = nullptr;
	Button *light_2_switch = nullptr;

	struct ThemeCache {
		Ref<Texture2D> light_1_icon;
		Ref<Texture2D> light_2_icon;
	} theme_cache;

	void _on_light_1_switch_pressed();
	void _on_light_2_switch_pressed();
	void _update_rotation();

protected:
	virtual void _update_theme_item_cache() override;
	void _notification(int p_what);
	void gui_input(const Ref<InputEvent> &p_event) override;

public:
	void edit(Ref<Mesh> p_mesh);
	MeshEditor();
};

class EditorInspectorPluginMesh : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginMesh, EditorInspectorPlugin);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_begin(Object *p_object) override;
};

class MeshEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(MeshEditorPlugin, EditorPlugin);

public:
	virtual String get_plugin_name() const override { return "Mesh"; }

	MeshEditorPlugin();
};
