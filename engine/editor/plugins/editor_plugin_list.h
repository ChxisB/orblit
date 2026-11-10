/**************************************************************************/
/*  editor_plugin_list.h                                                  */

#pragma once

#include "editor/plugins/editor_plugin.h"

class Control;
class InputEvent;

class EditorPluginList : public Object {
	ORBLIT_CLASS(EditorPluginList, Object);

	LocalVector<EditorPlugin *> plugins_list;

public:
	bool forward_gui_input(const Ref<InputEvent> &p_event) const;
	void forward_canvas_draw_over_viewport(Control *p_overlay) const;
	void forward_canvas_force_draw_over_viewport(Control *p_overlay) const;
	EditorPlugin::AfterGUIInput forward_3d_gui_input(Camera3D *p_camera, const Ref<InputEvent> &p_event, bool p_serve_when_force_input_enabled) const;
	void forward_3d_draw_over_viewport(Control *p_overlay) const;
	void forward_3d_force_draw_over_viewport(Control *p_overlay) const;

	void add_plugin(EditorPlugin *p_plugin);
	void remove_plugin(EditorPlugin *p_plugin);
};
