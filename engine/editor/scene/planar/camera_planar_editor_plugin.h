/**************************************************************************/
/*  camera_planar_editor_plugin.h                                             */

#pragma once

#include "editor/plugins/editor_plugin.h"

class Camera2D;
class Label;
class MenuButton;

class Camera2DEditor : public Control {
	ORBLIT_CLASS(Camera2DEditor, Control);

	EditorPlugin *plugin = nullptr;

	enum Menu {
		MENU_SNAP_LIMITS_TO_VIEWPORT,
	};

	enum class Drag {
		NONE,
		LEFT,
		TOP,
		RIGHT,
		BOTTOM,
		TOP_LEFT,
		TOP_RIGHT,
		BOTTOM_LEFT,
		BOTTOM_RIGHT,
		CENTER,
	};
	Drag drag_type = Drag::NONE;
	Drag hover_type = Drag::NONE;

	Rect2 drag_revert;
	Vector2 center_drag_point;

	Camera2D *selected_camera = nullptr;

	friend class Camera2DEditorPlugin;
	MenuButton *options = nullptr;

	void _menu_option(int p_option);
	void _snap_limits_to_viewport(Camera2D *p_camera);
	void _update_overlays_if_needed(Camera2D *p_camera);
	void _update_hover(const Vector2 &p_mouse_pos);

protected:
	static void register_members();
	void _notification(int p_what);

public:
	void edit(Camera2D *p_camera);

	bool forward_canvas_gui_input(const Ref<InputEvent> &p_event);
	void forward_canvas_draw_over_viewport(Control *p_overlay);

	Camera2DEditor(EditorPlugin *p_plugin);
};

class Camera2DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(Camera2DEditorPlugin, EditorPlugin);

	Camera2DEditor *camera_2d_editor = nullptr;

public:
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	virtual bool forward_canvas_gui_input(const Ref<InputEvent> &p_event) override { return camera_2d_editor->forward_canvas_gui_input(p_event); }
	virtual void forward_canvas_draw_over_viewport(Control *p_overlay) override { camera_2d_editor->forward_canvas_draw_over_viewport(p_overlay); }

	Camera2DEditorPlugin();
};
