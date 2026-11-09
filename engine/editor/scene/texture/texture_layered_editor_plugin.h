/**************************************************************************/
/*  texture_layered_editor_plugin.h                                       */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin.h"
#include "scene/ui/spin_box.h"
#include "scene/resources/material.h"
#include "scene/resources/shader.h"
#include "scene/resources/texture.h"

class Button;
class ColorChannelSelector;

class TextureLayeredEditor : public Control {
	ORBLIT_CLASS(TextureLayeredEditor, Control);

	struct ThemeCache {
		Color outline_color;
	} theme_cache;

	SpinBox *layer = nullptr;
	Label *info = nullptr;
	Button *info_toggle = nullptr;
	Ref<TextureLayered> texture;

	static inline Ref<Shader> shaders[3];
	Ref<ShaderMaterial> materials[3];

	float x_rot = 0;
	float y_rot = 0;
	Control *texture_rect = nullptr;

	bool setting = false;
	Vector2 original_mouse_pos;
	bool use_rotation = false;

	ColorChannelSelector *channel_selector = nullptr;

	void _draw_outline();
	void _toggle_info();

	void _make_materials();
	void _update_material(bool p_texture_changed);

	void _layer_changed(double) {
		if (!setting) {
			_update_material(false);
		}
	}

	void _texture_changed();

	void _texture_rect_update_area();
	void _texture_rect_draw();

	void _update_gui();

	void on_selected_channels_changed();

protected:
	void _notification(int p_what);
	virtual void gui_input(const Ref<InputEvent> &p_event) override;

public:
	static void init_shaders();
	static void finish_shaders();

	void edit(Ref<TextureLayered> p_texture);

	TextureLayeredEditor();
};

class EditorInspectorPluginLayeredTexture : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginLayeredTexture, EditorInspectorPlugin);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_begin(Object *p_object) override;
};

class TextureLayeredEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(TextureLayeredEditorPlugin, EditorPlugin);

public:
	virtual String get_plugin_name() const override { return "TextureLayered"; }

	TextureLayeredEditorPlugin();
};
