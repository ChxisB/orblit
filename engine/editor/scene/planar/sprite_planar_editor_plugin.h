/**************************************************************************/
/*  sprite_planar_editor_plugin.h                                             */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "scene/planar/sprite_planar.h"
#include "scene/ui/spin_box.h"

class AcceptDialog;
class Button;
class ConfirmationDialog;
class EditorZoomWidget;
class HBoxContainer;
class MenuButton;
class Panel;
class ViewPanner;

class Sprite2DEditor : public Control {
	ORBLIT_CLASS(Sprite2DEditor, Control);

	enum Menu {
		MENU_OPTION_CONVERT_TO_MESH_2D,
		MENU_OPTION_CONVERT_TO_POLYGON_2D,
		MENU_OPTION_CREATE_COLLISION_POLY_2D,
		MENU_OPTION_CREATE_LIGHT_OCCLUDER_2D
	};

	HBoxContainer *top_hb = nullptr;

	Menu selected_menu_item;

	Sprite2D *node = nullptr;

	MenuButton *options = nullptr;
	Button *resize_region_rect = nullptr;

	ConfirmationDialog *outline_dialog = nullptr;

	AcceptDialog *err_dialog = nullptr;

	ConfirmationDialog *debug_uv_dialog = nullptr;
	Panel *debug_uv = nullptr;
	Vector<Vector2> uv_lines;
	Vector<Vector<Vector2>> outline_lines;
	Vector<Vector<Vector2>> computed_outline_lines;
	Vector<Vector2> computed_vertices;
	Vector<Vector2> computed_uv;
	Vector<int> computed_indices;

	HScrollBar *h_scroll = nullptr;
	VScrollBar *v_scroll = nullptr;
	EditorZoomWidget *zoom_widget = nullptr;
	Ref<ViewPanner> panner;
	Vector2 draw_offset;
	real_t draw_zoom = 1.0;

	SpinBox *simplification = nullptr;
	SpinBox *grow_pixels = nullptr;
	SpinBox *shrink_pixels = nullptr;
	Button *update_preview = nullptr;

	void _menu_option(int p_option);

	//void _create_uv_lines();
	friend class Sprite2DEditorPlugin;

	void _debug_uv_input(const Ref<InputEvent> &p_input);
	void _debug_uv_draw();
	void _popup_debug_uv_dialog();
	void _center_view();
	void _pan_callback(Vector2 p_scroll_vec, Ref<InputEvent> p_event);
	void _zoom_callback(float p_zoom_factor, Vector2 p_origin, Ref<InputEvent> p_event);
	void _update_zoom_and_pan(bool p_zoom_at_center);
	void _update_mesh_data();

	void _create_node();
	void _convert_to_mesh_2d_node();
	void _convert_to_polygon_2d_node();
	void _create_collision_polygon_2d_node();
	void _create_light_occluder_2d_node();

	void _add_as_sibling_or_child(Entity *p_own_node, Entity *p_new_node);

	void _sync_sprite_resize_mode();
	void _update_sprite_resize_mode_button();

protected:
	void _node_removed(Entity *p_node);
	void _notification(int p_what);
	static void register_members();

public:
	void edit(Sprite2D *p_sprite);
	Sprite2DEditor();
};

class Sprite2DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(Sprite2DEditorPlugin, EditorPlugin);

	Sprite2DEditor *sprite_editor = nullptr;

	Label *dragging_mode_hint = nullptr;

public:
	virtual String get_plugin_name() const override { return "Sprite2D"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	Sprite2DEditorPlugin();
};
