/**************************************************************************/
/*  path_planar_editor_plugin.h                                               */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "scene/planar/path_planar.h"
#include "scene/ui/box_container.h"

class CanvasItemEditor;
class ConfirmationDialog;
class MenuButton;

class Path2DEditor : public HBoxContainer {
	ORBLIT_CLASS(Path2DEditor, HBoxContainer);

	friend class Path2DEditorPlugin;

	CanvasItemEditor *canvas_item_editor = nullptr;
	Panel *panel = nullptr;
	Path2D *node = nullptr;

	enum Mode {
		MODE_CREATE,
		MODE_EDIT,
		MODE_EDIT_CURVE,
		MODE_DELETE,
		MODE_CLOSE,
		MODE_CLEAR_POINTS,
	};

	Mode mode = MODE_EDIT;
	HBoxContainer *toolbar = nullptr;
	Button *curve_clear_points = nullptr;
	Button *curve_close = nullptr;
	Button *curve_create = nullptr;
	Button *curve_del = nullptr;
	Button *curve_edit = nullptr;
	Button *curve_edit_curve = nullptr;
	MenuButton *handle_menu = nullptr;

	Button *create_curve_button = nullptr;
	ConfirmationDialog *clear_points_dialog = nullptr;

	bool mirror_handle_angle = true;
	bool mirror_handle_length = true;
	bool on_edge = false;

	enum HandleOption {
		HANDLE_OPTION_ANGLE,
		HANDLE_OPTION_LENGTH,
	};

	enum Action {
		ACTION_NONE,
		ACTION_MOVING_POINT,
		ACTION_MOVING_NEW_POINT,
		ACTION_MOVING_NEW_POINT_FROM_SPLIT,
		ACTION_MOVING_IN,
		ACTION_MOVING_OUT,
	};

	Action action = ACTION_NONE;
	int action_point = 0;
	Point2 moving_from;
	Point2 moving_screen_from;
	float orig_in_length = 0.0f;
	float orig_out_length = 0.0f;
	Vector2 edge_point;
	Vector2 original_mouse_pos;

	// Number of control points in range of the last click.
	// 0, 1, or 2.
	int control_points_in_range = 0;

	void _mode_selected(int p_mode);
	void _handle_option_pressed(int p_option);
	void _cancel_current_action();

	void _node_visibility_changed();
	void _update_toolbar();

	void _create_curve();
	void _confirm_clear_points();
	void _clear_curve_points(Path2D *p_path2d);
	void _restore_curve_points(Path2D *p_path2d, const PackedVector2Array &p_points);

	RID debug_mesh_rid;
	RID debug_handle_mesh_rid;
	RID debug_handle_multimesh_rid;

	RID debug_handle_curve_multimesh_rid;
	RID debug_handle_sharp_multimesh_rid;
	RID debug_handle_smooth_multimesh_rid;

	LocalVector<Vector2> debug_handle_lines;
	LocalVector<Transform2D> debug_handle_curve_transforms;
	LocalVector<Transform2D> debug_handle_sharp_transforms;
	LocalVector<Transform2D> debug_handle_smooth_transforms;

protected:
	void _notification(int p_what);
	void _node_removed(Entity *p_node);
	static void register_members();

public:
	bool forward_gui_input(const Ref<InputEvent> &p_event);
	void forward_canvas_draw_over_viewport(Control *p_overlay);
	void edit(Entity *p_path2d);
	Path2DEditor();
	~Path2DEditor();
};

class Path2DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(Path2DEditorPlugin, EditorPlugin);

	Path2DEditor *path2d_editor = nullptr;

public:
	virtual bool forward_canvas_gui_input(const Ref<InputEvent> &p_event) override { return path2d_editor->forward_gui_input(p_event); }
	virtual void forward_canvas_draw_over_viewport(Control *p_overlay) override { path2d_editor->forward_canvas_draw_over_viewport(p_overlay); }

	virtual String get_plugin_name() const override { return "Path2D"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	Path2DEditorPlugin();
};
