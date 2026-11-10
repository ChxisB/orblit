/**************************************************************************/
/*  subviewport_container.cpp                                             */

#include "subviewport_container.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"
#include "scene/stage/viewport.h"

Size2 SubViewportContainer::get_minimum_size() const {
	if (stretch) {
		return Size2();
	}
	return Container::_get_minimum_size();
}

void SubViewportContainer::set_stretch(bool p_enable) {
	if (stretch == p_enable) {
		return;
	}

	stretch = p_enable;
	recalc_force_viewport_sizes();
	update_minimum_size();
	queue_sort();
	queue_redraw();
}

bool SubViewportContainer::is_stretch_enabled() const {
	return stretch;
}

void SubViewportContainer::set_stretch_shrink(int p_shrink) {
	ERR_FAIL_COND(p_shrink < 1);
	if (shrink == p_shrink) {
		return;
	}

	shrink = p_shrink;

	recalc_force_viewport_sizes();
	queue_redraw();
}

void SubViewportContainer::recalc_force_viewport_sizes() {
	if (!stretch) {
		return;
	}

	// If stretch is enabled, make sure that all child SubViwewports have the correct size.
	for (int i = 0; i < get_child_count(); i++) {
		SubViewport *c = Object::cast_to<SubViewport>(get_child(i));
		if (!c) {
			continue;
		}

		c->set_size_force(get_size() / shrink);
	}
}

int SubViewportContainer::get_stretch_shrink() const {
	return shrink;
}

Vector<int> SubViewportContainer::get_allowed_size_flags_horizontal() const {
	Vector<int> flags;
	flags.append(SIZE_MAXIMIZE);
	return flags;
}

Vector<int> SubViewportContainer::get_allowed_size_flags_vertical() const {
	Vector<int> flags;
	flags.append(SIZE_MAXIMIZE);
	return flags;
}

void SubViewportContainer::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_RESIZED: {
			recalc_force_viewport_sizes();
		} break;

		case NOTIFICATION_ENTER_STAGE:
		case NOTIFICATION_VISIBILITY_CHANGED: {
			for (int i = 0; i < get_child_count(); i++) {
				SubViewport *c = Object::cast_to<SubViewport>(get_child(i));
				if (!c) {
					continue;
				}

				if (is_visible_in_stage()) {
					c->set_update_mode(SubViewport::UPDATE_ALWAYS);
				} else {
					c->set_update_mode(SubViewport::UPDATE_DISABLED);
				}

				c->set_handle_input_locally(false); //do not handle input locally here
			}
		} break;

		case NOTIFICATION_DRAW: {
			for (int i = 0; i < get_child_count(); i++) {
				SubViewport *c = Object::cast_to<SubViewport>(get_child(i));
				if (!c) {
					continue;
				}

				if (stretch) {
					draw_texture_rect(c->get_texture(), Rect2(Vector2(), get_size()));
				} else {
					draw_texture_rect(c->get_texture(), Rect2(Vector2(), c->get_size()));
				}
			}
		} break;

		case NOTIFICATION_FOCUS_ENTER: {
			// If focused, send InputEvent to the SubViewport before the Gui-Input stage.
			set_input_enabled(true);
			set_unhandled_input_enabled(false);
		} break;

		case NOTIFICATION_FOCUS_EXIT: {
			// A different Control has focus and should receive Gui-Input before the InputEvent is sent to the SubViewport.
			set_input_enabled(false);
			set_unhandled_input_enabled(true);
		} break;
	}
}

void SubViewportContainer::_notify_viewports(int p_notification) {
	for (int i = 0; i < get_child_count(); i++) {
		SubViewport *c = Object::cast_to<SubViewport>(get_child(i));
		if (!c) {
			continue;
		}
		c->notification(p_notification);
	}
}

void SubViewportContainer::input(const Ref<InputEvent> &p_event) {
	_propagate_nonpositional_event(p_event);
}

void SubViewportContainer::unhandled_input(const Ref<InputEvent> &p_event) {
	_propagate_nonpositional_event(p_event);
}

void SubViewportContainer::_propagate_nonpositional_event(const Ref<InputEvent> &p_event) {
	ERR_FAIL_COND(p_event.is_null());

	if (Engine::get_singleton()->is_editor_hint()) {
		return;
	}

	if (_is_propagated_in_gui_input(p_event)) {
		return;
	}

	bool send;
	if (ORBLIT_VIRTUAL_CALL(_propagate_input_event, p_event, send)) {
		if (!send) {
			return;
		}
	}

	_send_event_to_viewports(p_event);
}

void SubViewportContainer::gui_input(const Ref<InputEvent> &p_event) {
	ERR_FAIL_COND(p_event.is_null());

	if (Engine::get_singleton()->is_editor_hint()) {
		return;
	}

	if (!_is_propagated_in_gui_input(p_event)) {
		return;
	}

	bool send;
	if (ORBLIT_VIRTUAL_CALL(_propagate_input_event, p_event, send)) {
		if (!send) {
			return;
		}
	}

	if (stretch && shrink > 1) {
		Transform2D xform;
		xform.scale(Vector2(1, 1) / shrink);
		_send_event_to_viewports(p_event->xformed_by(xform));
	} else {
		_send_event_to_viewports(p_event);
	}
}

void SubViewportContainer::_send_event_to_viewports(const Ref<InputEvent> &p_event) {
	for (int i = 0; i < get_child_count(); i++) {
		SubViewport *c = Object::cast_to<SubViewport>(get_child(i));
		if (!c || c->is_input_disabled()) {
			continue;
		}

		c->push_input(p_event);
	}
}

bool SubViewportContainer::_is_propagated_in_gui_input(const Ref<InputEvent> &p_event) {
	// Propagation of events with a position property happen in gui_input
	// Propagation of other events happen in input
	if (Object::cast_to<InputEventMouse>(*p_event) || Object::cast_to<InputEventScreenDrag>(*p_event) || Object::cast_to<InputEventScreenTouch>(*p_event) || Object::cast_to<InputEventGesture>(*p_event)) {
		return true;
	}
	return false;
}

void SubViewportContainer::set_mouse_target(bool p_enable) {
	mouse_target = p_enable;
}

bool SubViewportContainer::is_mouse_target_enabled() {
	return mouse_target;
}

void SubViewportContainer::add_child_notify(Entity *p_child) {
	if (Object::cast_to<SubViewport>(p_child)) {
		queue_redraw();
	}
}

void SubViewportContainer::remove_child_notify(Entity *p_child) {
	if (Object::cast_to<SubViewport>(p_child)) {
		queue_redraw();
	}
}

PackedStringArray SubViewportContainer::get_configuration_warnings() const {
	PackedStringArray warnings = Container::get_configuration_warnings();

	bool has_viewport = false;
	for (int i = 0; i < get_child_count(); i++) {
		if (Object::cast_to<SubViewport>(get_child(i))) {
			has_viewport = true;
			break;
		}
	}
	if (!has_viewport) {
		warnings.push_back(RTR("This node doesn't have a SubViewport as child, so it can't display its intended content.\nConsider adding a SubViewport as a child to provide something displayable."));
	}

	if (get_default_cursor_shape() != Control::CURSOR_ARROW) {
		warnings.push_back(RTR("The default mouse cursor shape of SubViewportContainer has no effect.\nConsider leaving it at its initial value `CURSOR_ARROW`."));
	}

	return warnings;
}

void SubViewportContainer::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_stretch", "enable"), &SubViewportContainer::set_stretch);
	ClassRegistry::bind_method(D_METHOD("is_stretch_enabled"), &SubViewportContainer::is_stretch_enabled);

	ClassRegistry::bind_method(D_METHOD("set_stretch_shrink", "amount"), &SubViewportContainer::set_stretch_shrink);
	ClassRegistry::bind_method(D_METHOD("get_stretch_shrink"), &SubViewportContainer::get_stretch_shrink);

	ClassRegistry::bind_method(D_METHOD("set_mouse_target", "amount"), &SubViewportContainer::set_mouse_target);
	ClassRegistry::bind_method(D_METHOD("is_mouse_target_enabled"), &SubViewportContainer::is_mouse_target_enabled);

	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "stretch"), "set_stretch", "is_stretch_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "stretch_shrink", PROPERTY_HINT_RANGE, "1,32,1,or_greater"), "set_stretch_shrink", "get_stretch_shrink");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "mouse_target"), "set_mouse_target", "is_mouse_target_enabled");

	ORBLIT_VIRTUAL_BIND(_propagate_input_event, "event");
}

SubViewportContainer::SubViewportContainer() {
	set_unhandled_input_enabled(true);
	set_focus_mode(FOCUS_CLICK);
}
