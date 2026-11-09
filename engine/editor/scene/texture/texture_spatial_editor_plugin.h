/**************************************************************************/
/*  texture_spatial_editor_plugin.h                                            */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin.h"
#include "scene/ui/spin_box.h"
#include "scene/resources/material.h"
#include "scene/resources/shader.h"
#include "scene/resources/texture.h"

class Button;
class ColorChannelSelector;

class Texture3DEditor : public Control {
	ORBLIT_CLASS(Texture3DEditor, Control);

	struct ThemeCache {
		Color outline_color;
	} theme_cache;

	SpinBox *layer = nullptr;
	Label *info = nullptr;
	Button *info_toggle = nullptr;
	Ref<Texture3D> texture;

	static inline Ref<Shader> texture_shader;
	Ref<ShaderMaterial> texture_material;

	Control *texture_rect = nullptr;

	ColorChannelSelector *channel_selector = nullptr;

	bool setting = false;

	void _draw_outline();

	void _layer_changed(double) {
		if (!setting) {
			_update_material(false);
		}
	}

	void _texture_changed();

	void _texture_rect_update_area();
	void _texture_rect_draw();

	void _update_material(bool p_texture_changed);
	void _update_gui();
	void _toggle_info();

	void on_selected_channels_changed();

protected:
	void _notification(int p_what);

public:
	static void init_shaders();
	static void finish_shaders();

	void edit(Ref<Texture3D> p_texture);

	Texture3DEditor();
	~Texture3DEditor();
};

class EditorInspectorPlugin3DTexture : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPlugin3DTexture, EditorInspectorPlugin);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_begin(Object *p_object) override;
};

class Texture3DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(Texture3DEditorPlugin, EditorPlugin);

public:
	virtual String get_plugin_name() const override { return "Texture3D"; }

	Texture3DEditorPlugin();
};
