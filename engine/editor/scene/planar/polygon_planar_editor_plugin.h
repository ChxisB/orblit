/**************************************************************************/
/*  polygon_planar_editor_plugin.h                                            */

#pragma once

#include "editor/scene/planar/abstract_polygon_planar_editor.h"
#include "scene/planar/polygon_planar.h"

class AcceptDialog;
class ButtonGroup;
class EditorDock;
class EditorZoomWidget;
class HScrollBar;
class HSlider;
class Label;
class MenuButton;
class Panel;
class ScrollContainer;
class SpinBox;
class TextureRect;
class ViewPanner;
class VScrollBar;

class Polygon2DEditor : public AbstractPolygon2DEditor {
	ORBLIT_CLASS(Polygon2DEditor, AbstractPolygon2DEditor);

	enum {
		MENU_POLYGON_TO_UV,
		MENU_UV_TO_POLYGON,
		MENU_UV_CLEAR,
		MENU_GRID_SETTINGS,
	};

	enum Mode {
		MODE_POINTS,
		MODE_POLYGONS,
		MODE_UV,
		MODE_BONES,
		MODE_MAX
	};

	enum Action {
		ACTION_CREATE,
		ACTION_CREATE_INTERNAL,
		ACTION_REMOVE_INTERNAL,
		ACTION_EDIT_POINT,
		ACTION_MOVE,
		ACTION_ROTATE,
		ACTION_SCALE,
		ACTION_ADD_POLYGON,
		ACTION_REMOVE_POLYGON,
		ACTION_PAINT_WEIGHT,
		ACTION_CLEAR_WEIGHT,
		ACTION_MAX
	};

	Polygon2D *node = nullptr;
	Polygon2D *previous_node = nullptr;

	EditorDock *polygon_edit = nullptr;
	Mode current_mode = MODE_MAX; // Uninitialized.
	Button *mode_buttons[MODE_MAX];
	Action selected_action = ACTION_CREATE;
	Button *action_buttons[ACTION_MAX];
	Button *b_snap_enable = nullptr;
	Button *b_snap_grid = nullptr;
	MenuButton *edit_menu = nullptr;

	Control *canvas = nullptr;
	Panel *canvas_background = nullptr;
	Polygon2D *preview_polygon = nullptr;
	EditorZoomWidget *zoom_widget = nullptr;
	HScrollBar *hscroll = nullptr;
	VScrollBar *vscroll = nullptr;
	bool center_view_on_draw = false;

	Ref<ViewPanner> panner;
	void _pan_callback(Vector2 p_scroll_vec, Ref<InputEvent> p_event);
	void _zoom_callback(float p_zoom_factor, Vector2 p_origin, Ref<InputEvent> p_event);
	Vector2 draw_offset;
	real_t draw_zoom = 1.0;

	VBoxContainer *bone_scroll_main_vb = nullptr;
	ScrollContainer *bone_scroll = nullptr;
	VBoxContainer *bone_scroll_vb = nullptr;
	Button *sync_bones = nullptr;
	HSlider *bone_paint_strength = nullptr;
	SpinBox *bone_paint_radius = nullptr;
	Label *bone_paint_radius_label = nullptr;
	bool bone_painting = false;
	int bone_painting_bone = 0;
	Vector<float> prev_weights;
	Vector2 bone_paint_pos;
	AcceptDialog *grid_settings = nullptr;

	void _sync_bones();
	void _update_bone_list(const Polygon2D *p_for_node);

	Vector<Vector2> editing_points;
	Vector<Vector2> previous_uv;
	Vector<Vector2> previous_polygon;
	Vector<Color> previous_colors;
	int previous_internal_vertices = 0;
	Array previous_bones;
	Array previous_polygons;

	Vector2 create_to;
	int point_drag_index = -1;
	bool is_dragging = false;
	bool is_creating = false;
	int hovered_point = -1;
	Vector<int> polygon_create;
	Action current_action = ACTION_CREATE;
	Vector2 drag_from;

	AcceptDialog *error = nullptr;

	bool use_snap = false;
	bool snap_show_grid = false;
	Vector2 snap_offset;
	Vector2 snap_step;

	void _edit_menu_option(int p_option);

	void _cancel_editing();
	void _update_polygon_editing_state();
	void _update_available_modes();

	void _center_view();
	void _update_zoom_and_pan(bool p_zoom_at_center);
	void _canvas_input(const Ref<InputEvent> &p_input);
	void _center_view_on_draw(bool p_enabled = true);
	void _canvas_draw();
	void _set_action(int p_mode);

	void _set_use_snap(bool p_use);
	void _set_show_grid(bool p_show);
	void _set_snap_off_x(real_t p_val);
	void _set_snap_off_y(real_t p_val);
	void _set_snap_step_x(real_t p_val);
	void _set_snap_step_y(real_t p_val);

	void _select_mode(int p_mode);
	void _bone_paint_selected(int p_index);

	int _get_polygon_count() const override;

protected:
	virtual Entity2D *_get_node() const override;
	virtual void _set_node(Entity *p_polygon) override;

	virtual Vector2 _get_offset(int p_idx) const override;

	virtual bool _has_uv() const override { return true; }
	virtual void _commit_action() override;

	void _notification(int p_what);
	static void register_members();

	Vector2 snap_point(Vector2 p_target) const;

public:
	Polygon2DEditor();
};

class Polygon2DEditorPlugin : public AbstractPolygon2DEditorPlugin {
	ORBLIT_CLASS(Polygon2DEditorPlugin, AbstractPolygon2DEditorPlugin);

public:
	Polygon2DEditorPlugin();
};
