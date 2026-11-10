/**************************************************************************/
/*  collision_shape_planar_editor_plugin.h                                    */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "scene/planar/physics/collision_shape_planar.h"

class CanvasItemEditor;

class CollisionShape2DEditor : public Control {
	ORBLIT_CLASS(CollisionShape2DEditor, Control);

	enum ShapeType {
		CAPSULE_SHAPE,
		CIRCLE_SHAPE,
		CONCAVE_POLYGON_SHAPE,
		CONVEX_POLYGON_SHAPE,
		WORLD_BOUNDARY_SHAPE,
		SEPARATION_RAY_SHAPE,
		RECTANGLE_SHAPE,
		SEGMENT_SHAPE
	};

	const Point2 RECT_HANDLES[8] = {
		Point2(1, 0),
		Point2(1, 1),
		Point2(0, 1),
		Point2(-1, 1),
		Point2(-1, 0),
		Point2(-1, -1),
		Point2(0, -1),
		Point2(1, -1),
	};

	CanvasItemEditor *canvas_item_editor = nullptr;
	CollisionShape2D *node = nullptr;

	Vector<Point2> handles;

	int shape_type = -1;
	int hover_handle = -1;
	int edit_handle = -1;
	bool pressed = false;
	real_t grab_threshold = 8;
	Variant original;
	Transform2D original_transform;
	Vector2 original_point;
	Point2 last_point;
	Vector2 original_mouse_pos;

	Ref<Shape2D> current_shape;

	Variant get_handle_value(int idx) const;
	void set_handle(int idx, Point2 &p_point);
	void commit_handle(int idx, Variant &p_org);

	void _update_hover(const Vector2 &p_mouse_pos);
	void _shape_changed();

protected:
	void _notification(int p_what);
	void _node_removed(Entity *p_node);

public:
	bool forward_canvas_gui_input(const Ref<InputEvent> &p_event);
	void forward_canvas_draw_over_viewport(Control *p_overlay);
	void edit(Entity *p_node);

	CollisionShape2DEditor();
};

class CollisionShape2DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(CollisionShape2DEditorPlugin, EditorPlugin);

	CollisionShape2DEditor *collision_shape_2d_editor = nullptr;

public:
	virtual bool forward_canvas_gui_input(const Ref<InputEvent> &p_event) override { return collision_shape_2d_editor->forward_canvas_gui_input(p_event); }
	virtual void forward_canvas_draw_over_viewport(Control *p_overlay) override { collision_shape_2d_editor->forward_canvas_draw_over_viewport(p_overlay); }

	virtual String get_plugin_name() const override { return "CollisionShape2D"; }
	virtual void edit(Object *p_obj) override;
	virtual bool handles(Object *p_obj) const override;
	virtual void make_visible(bool visible) override;

	CollisionShape2DEditorPlugin();
};
