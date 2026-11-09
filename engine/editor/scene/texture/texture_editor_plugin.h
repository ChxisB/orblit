/**************************************************************************/
/*  texture_editor_plugin.h                                               */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin.h"
#include "scene/ui/margin_container.h"
#include "scene/resources/texture.h"

class AspectRatioContainer;
class ColorRect;
class TextureRect;
class ShaderMaterial;
class Button;
class ColorChannelSelector;
class SpinBox;

class TexturePreview : public MarginContainer {
	ORBLIT_CLASS(TexturePreview, MarginContainer);

private:
	struct ThemeCache {
		Color outline_color;
	} theme_cache;

	TextureRect *texture_display = nullptr;

	MarginContainer *margin_container = nullptr;
	Control *outline_overlay = nullptr;
	AspectRatioContainer *centering_container = nullptr;
	ColorRect *bg_rect = nullptr;
	TextureRect *checkerboard = nullptr;
	Label *metadata_label = nullptr;
	Button *metadata_toggle = nullptr;

	static inline Ref<ShaderMaterial> texture_material;

	ColorChannelSelector *channel_selector = nullptr;
	SpinBox *mipmap_spinbox = nullptr;

	void _draw_outline();
	void _update_metadata_label_text();
	void _toggle_metadata_label();

protected:
	void _notification(int p_what);
	void _update_texture_display_ratio();
	void on_selected_channels_changed();
	void on_selected_mipmap_changed(double p_value);

public:
	static void init_shaders();
	static void finish_shaders();

	TextureRect *get_texture_display();
	TexturePreview(Ref<Texture2D> p_texture, bool p_show_metadata);
};

class EditorInspectorPluginTexture : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginTexture, EditorInspectorPlugin);

	Ref<Image> this_image;

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_begin(Object *p_object) override;
};

class TextureEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(TextureEditorPlugin, EditorPlugin);

public:
	virtual String get_plugin_name() const override { return "Texture2D"; }

	TextureEditorPlugin();
};
