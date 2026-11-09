/**************************************************************************/
/*  gradient_texture_planar_editor_plugin.h                                   */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/plugins/editor_plugin.h"

class Button;
class EditorSpinSlider;
class GradientTexture2D;

class GradientTexture2DEdit : public Control {
	ORBLIT_CLASS(GradientTexture2DEdit, Control);

	enum Handle {
		HANDLE_NONE,
		HANDLE_FROM,
		HANDLE_TO
	};

	Ref<GradientTexture2D> texture;
	bool snap_enabled = false;
	int snap_count = 0;

	TextureRect *checkerboard = nullptr;

	Handle hovered = HANDLE_NONE;
	Handle grabbed = HANDLE_NONE;
	Point2 initial_grab_pos;

	Size2 handle_size;
	Point2 offset;
	Size2 size;

	Point2 _get_handle_pos(const Handle p_handle);
	Handle get_handle_at(const Vector2 &p_pos);
	void set_fill_pos(const Vector2 &p_pos);

	virtual void gui_input(const Ref<InputEvent> &p_event) override;

	void _draw();

protected:
	void _notification(int p_what);

public:
	void set_texture(Ref<GradientTexture2D> &p_texture);
	void set_snap_enabled(bool p_snap_enabled);
	void set_snap_count(int p_snap_count);

	GradientTexture2DEdit();
};

class GradientTexture2DEditor : public VBoxContainer {
	ORBLIT_CLASS(GradientTexture2DEditor, VBoxContainer);

	Ref<GradientTexture2D> texture;

	Button *reverse_button = nullptr;
	Button *snap_button = nullptr;
	EditorSpinSlider *snap_count_edit = nullptr;
	GradientTexture2DEdit *texture_editor_rect = nullptr;

	void _reverse_button_pressed();
	void _set_snap_enabled(bool p_enabled);
	void _set_snap_count(int p_snap_count);

protected:
	void _notification(int p_what);

public:
	static const int DEFAULT_SNAP;
	void set_texture(Ref<GradientTexture2D> &p_texture);

	GradientTexture2DEditor();
};

class EditorInspectorPluginGradientTexture2D : public EditorInspectorPlugin {
	ORBLIT_CLASS(EditorInspectorPluginGradientTexture2D, EditorInspectorPlugin);

public:
	virtual bool can_handle(Object *p_object) override;
	virtual void parse_begin(Object *p_object) override;
};

class GradientTexture2DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(GradientTexture2DEditorPlugin, EditorPlugin);

public:
	GradientTexture2DEditorPlugin();
};
