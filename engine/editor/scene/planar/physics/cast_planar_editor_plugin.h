/**************************************************************************/
/*  cast_planar_editor_plugin.h                                               */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "scene/planar/entity_planar.h"

class CanvasItemEditor;

class Cast2DEditor : public Control {
	ORBLIT_CLASS(Cast2DEditor, Control);

	CanvasItemEditor *canvas_item_editor = nullptr;
	Entity2D *node = nullptr;

	bool pressed = false;
	Point2 original_target_position;
	Vector2 original_mouse_pos;

protected:
	void _notification(int p_what);
	void _node_removed(Entity *p_node);

public:
	bool forward_canvas_gui_input(const Ref<InputEvent> &p_event);
	void forward_canvas_draw_over_viewport(Control *p_overlay);
	void edit(Entity2D *p_node);
};

class Cast2DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(Cast2DEditorPlugin, EditorPlugin);

	Cast2DEditor *cast_2d_editor = nullptr;

public:
	virtual bool forward_canvas_gui_input(const Ref<InputEvent> &p_event) override { return cast_2d_editor->forward_canvas_gui_input(p_event); }
	virtual void forward_canvas_draw_over_viewport(Control *p_overlay) override { cast_2d_editor->forward_canvas_draw_over_viewport(p_overlay); }

	virtual String get_plugin_name() const override { return "Cast2D"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool visible) override;

	Cast2DEditorPlugin();
};
