/**************************************************************************/
/*  navigation_link_planar_editor_plugin.h                                    */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "scene/planar/navigation/navigation_link_planar.h"

class CanvasItemEditor;

class NavigationLink2DEditor : public Control {
	ORBLIT_CLASS(NavigationLink2DEditor, Control);

	CanvasItemEditor *canvas_item_editor = nullptr;
	NavigationLink2D *node = nullptr;

	bool start_grabbed = false;
	Vector2 original_start_position;

	bool end_grabbed = false;
	Vector2 original_end_position;

protected:
	void _notification(int p_what);
	void _node_removed(Entity *p_node);

public:
	bool forward_canvas_gui_input(const Ref<InputEvent> &p_event);
	void forward_canvas_draw_over_viewport(Control *p_overlay);
	void edit(NavigationLink2D *p_node);
};

class NavigationLink2DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(NavigationLink2DEditorPlugin, EditorPlugin);

	NavigationLink2DEditor *editor = nullptr;

public:
	virtual bool forward_canvas_gui_input(const Ref<InputEvent> &p_event) override { return editor->forward_canvas_gui_input(p_event); }
	virtual void forward_canvas_draw_over_viewport(Control *p_overlay) override { editor->forward_canvas_draw_over_viewport(p_overlay); }

	virtual String get_plugin_name() const override { return "NavigationLink2D"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	NavigationLink2DEditorPlugin();
};
