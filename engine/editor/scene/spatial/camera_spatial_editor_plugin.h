/**************************************************************************/
/*  camera_spatial_editor_plugin.h                                             */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "editor/scene/texture/texture_editor_plugin.h"

class SubViewport;

class Camera3DEditor : public Control {
	ORBLIT_CLASS(Camera3DEditor, Control);

	Button *preview = nullptr;
	Entity *node = nullptr;

	void _pressed();

protected:
	void _node_removed(Entity *p_node);

public:
	void edit(Entity *p_camera);
	Camera3DEditor();
};

class Camera3DPreview : public MarginContainer {
	ORBLIT_CLASS(Camera3DPreview, MarginContainer);

	Camera3D *camera = nullptr;
	AspectRatioContainer *centering_container = nullptr;
	SubViewport *sub_viewport = nullptr;

	static bool camera_preview_folded;

	void _update_sub_viewport_size();
	void _project_settings_changed();
	void _toggle_folding(bool p_folded);

public:
	Camera3DPreview(Camera3D *p_camera);
};

class EditorInspectorPluginCamera3DPreview : public EditorInspectorPluginTexture {
	ORBLIT_CLASS(EditorInspectorPluginCamera3DPreview, EditorInspectorPluginTexture);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_begin(Object *p_object) override;
};

class Camera3DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(Camera3DEditorPlugin, EditorPlugin);

public:
	virtual String get_plugin_name() const override { return "Camera3D"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	Camera3DEditorPlugin();
};
