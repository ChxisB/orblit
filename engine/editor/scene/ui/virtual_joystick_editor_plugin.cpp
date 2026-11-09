/**************************************************************************/
/*  virtual_joystick_editor_plugin.cpp                                    */

#include "virtual_joystick_editor_plugin.h"

#include "core/object/callable_mp.h"
#include "editor/scene/canvas_item_editor_plugin.h"
#include "editor/themes/editor_scale.h"
#include "scene/ui/virtual_joystick.h"

void VirtualJoystickEditorPlugin::edit(Object *p_object) {
	if (virtual_joystick) {
		virtual_joystick->disconnect(SceneStringName(draw), callable_mp(CanvasItemEditor::get_singleton(), &CanvasItemEditor::update_viewport));
	}

	virtual_joystick = Object::cast_to<VirtualJoystick>(p_object);

	if (virtual_joystick) {
		virtual_joystick->connect(SceneStringName(draw), callable_mp(CanvasItemEditor::get_singleton(), &CanvasItemEditor::update_viewport));
	}
	CanvasItemEditor::get_singleton()->update_viewport();
}

bool VirtualJoystickEditorPlugin::handles(Object *p_object) const {
	return Object::cast_to<VirtualJoystick>(p_object) != nullptr;
}

void VirtualJoystickEditorPlugin::forward_canvas_draw_over_viewport(Control *p_viewport_control) {
	if (!virtual_joystick || !virtual_joystick->is_visible_in_stage()) {
		return;
	}

	Transform2D xform = CanvasItemEditor::get_singleton()->get_canvas_transform() * virtual_joystick->get_screen_transform();

	Vector2 center = virtual_joystick->get_joystick_position();
	float base_radius = virtual_joystick->get_joystick_size() * 0.5f;

	float clampzone_radius = base_radius * virtual_joystick->get_clampzone_ratio();
	float deadzone_radius = clampzone_radius * virtual_joystick->get_deadzone_ratio();

	// NOTE: This color is copied from Camera2DEditor::forward_canvas_draw_over_viewport.
	// We may want to unify them somehow in the future.
	Color clampzone_color = Color(1, 1, 0.25, 0.63);
	Color deadzone_color = Color(1, 1, 0.25, 0.63);

	int width = Math::round(1 * EDSCALE);

	p_viewport_control->draw_circle(xform.xform(center), clampzone_radius * xform.get_scale().x, clampzone_color, false, width);

	p_viewport_control->draw_circle(xform.xform(center), deadzone_radius * xform.get_scale().x, deadzone_color, false, width);
}
