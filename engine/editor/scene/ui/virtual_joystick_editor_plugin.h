/**************************************************************************/
/*  virtual_joystick_editor_plugin.h                                      */

#pragma once

#include "editor/plugins/editor_plugin.h"

class CanvasItemEditor;
class VirtualJoystick;

class VirtualJoystickEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(VirtualJoystickEditorPlugin, EditorPlugin);

	VirtualJoystick *virtual_joystick = nullptr;

public:
	void forward_canvas_draw_over_viewport(Control *p_viewport_control) override;

	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
};
