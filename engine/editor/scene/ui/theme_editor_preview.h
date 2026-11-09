/**************************************************************************/
/*  theme_editor_preview.h                                                */

#pragma once

#include "scene/resources/theme.h"
#include "scene/ui/box_container.h"
#include "scene/ui/margin_container.h"

class Button;
class ColorPickerButton;
class ColorRect;
class Prefab;
class ScrollContainer;

class ScalableContainer : public MarginContainer {
	ORBLIT_CLASS(ScalableContainer, MarginContainer);

protected:
	void _notification(int p_what);

public:
	virtual Size2 get_minimum_size() const override;

	ScalableContainer();
};

class ThemeEditorPreview : public VBoxContainer {
	ORBLIT_CLASS(ThemeEditorPreview, VBoxContainer);

	ScrollContainer *preview_container = nullptr;
	MarginContainer *preview_root = nullptr;
	ColorRect *preview_bg = nullptr;
	MarginContainer *preview_overlay = nullptr;
	Control *picker_overlay = nullptr;
	Control *hovered_control = nullptr;

	struct ThemeCache {
		Ref<StyleBox> preview_picker_overlay;
		Color preview_picker_overlay_color;
		Ref<StyleBox> preview_picker_label;
		Ref<Font> preview_picker_font;
		int font_size = 16;
	} theme_cache;

	void _picker_button_cbk();
	Control *_find_hovered_control(Control *p_parent, Vector2 p_mouse_position);

	void _draw_picker_overlay();
	void _gui_input_picker_overlay(const Ref<InputEvent> &p_event);
	void _reset_picker_overlay();

	void _update_preview_bg();

protected:
	HBoxContainer *preview_toolbar = nullptr;
	ScalableContainer *preview_content = nullptr;
	Button *picker_button = nullptr;

	void add_preview_overlay(Control *p_overlay);

	void _notification(int p_what);
	static void register_members();

public:
	void set_preview_theme(const Ref<Theme> &p_theme);

	ThemeEditorPreview();
};

class DefaultThemeEditorPreview : public ThemeEditorPreview {
	ORBLIT_CLASS(DefaultThemeEditorPreview, ThemeEditorPreview);

	ColorPickerButton *test_color_picker_button = nullptr;

protected:
	void _notification(int p_what);

public:
	DefaultThemeEditorPreview();
};

class SceneThemeEditorPreview : public ThemeEditorPreview {
	ORBLIT_CLASS(SceneThemeEditorPreview, ThemeEditorPreview);

	Ref<Prefab> loaded_scene;

	Button *reload_scene_button = nullptr;

	void _reload_scene();

protected:
	void _notification(int p_what);
	static void register_members();

public:
	bool set_preview_scene(const String &p_path);
	String get_preview_scene_path() const;

	SceneThemeEditorPreview();
};
