/**************************************************************************/
/*  touch_screen_button.cpp                                               */

#include "touch_screen_button.h"

#include "core/config/engine.h"
#include "core/input/input.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "scene/stage/stage.h"
#include "scene/stage/viewport.h"
#include "systems/display/accessibility_system.h"
#include "systems/display/display_system.h"

#if defined(DEBUG_ENABLED) && !defined(PHYSICS_PLANAR_DISABLED)
#include "systems/physics_planar/physics_system_planar.h"
#endif

void TouchScreenButton::set_texture_normal(const Ref<Texture2D> &p_texture) {
	if (texture_normal == p_texture) {
		return;
	}
	if (texture_normal.is_valid()) {
		texture_normal->disconnect(CoreStringName(changed), callable_mp((CanvasItem *)this, &CanvasItem::queue_redraw));
	}
	texture_normal = p_texture;
	if (texture_normal.is_valid()) {
		texture_normal->connect(CoreStringName(changed), callable_mp((CanvasItem *)this, &CanvasItem::queue_redraw), CONNECT_REFERENCE_COUNTED);
	}
	queue_redraw();
}

Ref<Texture2D> TouchScreenButton::get_texture_normal() const {
	return texture_normal;
}

void TouchScreenButton::set_texture_pressed(const Ref<Texture2D> &p_texture_pressed) {
	if (texture_pressed == p_texture_pressed) {
		return;
	}
	if (texture_pressed.is_valid()) {
		texture_pressed->disconnect(CoreStringName(changed), callable_mp((CanvasItem *)this, &CanvasItem::queue_redraw));
	}
	texture_pressed = p_texture_pressed;
	if (texture_pressed.is_valid()) {
		texture_pressed->connect(CoreStringName(changed), callable_mp((CanvasItem *)this, &CanvasItem::queue_redraw), CONNECT_REFERENCE_COUNTED);
	}
	queue_redraw();
}

Ref<Texture2D> TouchScreenButton::get_texture_pressed() const {
	return texture_pressed;
}

void TouchScreenButton::set_bitmask(const Ref<BitMap> &p_bitmask) {
	bitmask = p_bitmask;
}

Ref<BitMap> TouchScreenButton::get_bitmask() const {
	return bitmask;
}

void TouchScreenButton::set_shape(const Ref<Shape2D> &p_shape) {
	if (shape == p_shape) {
		return;
	}
	if (shape.is_valid()) {
		shape->disconnect_changed(callable_mp((CanvasItem *)this, &CanvasItem::queue_redraw));
	}
	shape = p_shape;
	if (shape.is_valid()) {
		shape->connect_changed(callable_mp((CanvasItem *)this, &CanvasItem::queue_redraw));
	}
	queue_redraw();
}

Ref<Shape2D> TouchScreenButton::get_shape() const {
	return shape;
}

void TouchScreenButton::set_shape_centered(bool p_shape_centered) {
	shape_centered = p_shape_centered;
	queue_redraw();
}

bool TouchScreenButton::is_shape_visible() const {
	return shape_visible;
}

void TouchScreenButton::set_shape_visible(bool p_shape_visible) {
	shape_visible = p_shape_visible;
	queue_redraw();
}

bool TouchScreenButton::is_shape_centered() const {
	return shape_centered;
}

void TouchScreenButton::_accessibility_action_click(const Variant &p_data) {
	_press(0);
	_release();
}

void TouchScreenButton::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ACCESSIBILITY_UPDATE: {
			RID ae = get_accessibility_element();
			ERR_FAIL_COND(ae.is_null());

			Rect2 dst_rect(Point2(), texture_normal.is_valid() ? texture_normal->get_size() : Size2());

			AccessibilitySystem::get_singleton()->update_set_role(ae, AccessibilitySystemEnums::AccessibilityRole::ROLE_BUTTON);

			AccessibilitySystem::get_singleton()->update_add_action(ae, AccessibilitySystemEnums::AccessibilityAction::ACTION_CLICK, callable_mp(this, &TouchScreenButton::_accessibility_action_click));

			AccessibilitySystem::get_singleton()->update_set_transform(ae, get_transform());
			AccessibilitySystem::get_singleton()->update_set_bounds(ae, dst_rect);
		} break;

		case NOTIFICATION_DRAW: {
			if (!is_inside_stage()) {
				return;
			}
			if (!Engine::get_singleton()->is_editor_hint() && !DisplaySystem::get_singleton()->is_touchscreen_available() && visibility == VISIBILITY_TOUCHSCREEN_ONLY) {
				return;
			}

			if (finger_pressed != -1) {
				if (texture_pressed.is_valid()) {
					draw_texture(texture_pressed, Point2());
				} else if (texture_normal.is_valid()) {
					draw_texture(texture_normal, Point2());
				}

			} else {
				if (texture_normal.is_valid()) {
					draw_texture(texture_normal, Point2());
				}
			}

#if defined(DEBUG_ENABLED) && !defined(PHYSICS_PLANAR_DISABLED)
			if (!shape_visible) {
				return;
			}
			if (!Engine::get_singleton()->is_editor_hint() && !PhysicsSystem2D::get_singleton()->debug_is_enabled()) {
				return;
			}
			if (shape.is_valid()) {
				Color draw_col = get_stage()->get_debug_collisions_color();

				Vector2 pos;
				if (shape_centered && texture_normal.is_valid()) {
					pos = texture_normal->get_size() * 0.5;
				}

				draw_set_transform_matrix(get_canvas_transform().translated_local(pos));
				shape->draw(get_canvas_item(), draw_col);
			}
#endif
		} break;

		case NOTIFICATION_ENTER_STAGE: {
			if (!Engine::get_singleton()->is_editor_hint() && !DisplaySystem::get_singleton()->is_touchscreen_available() && visibility == VISIBILITY_TOUCHSCREEN_ONLY) {
				return;
			}
			queue_redraw();

			if (!Engine::get_singleton()->is_editor_hint()) {
				set_input_enabled(is_visible_in_stage());
			}
		} break;

		case NOTIFICATION_EXIT_STAGE: {
			if (is_pressed()) {
				_release(true);
			}
		} break;

		case NOTIFICATION_VISIBILITY_CHANGED: {
			if (Engine::get_singleton()->is_editor_hint()) {
				break;
			}
			if (is_visible_in_stage()) {
				set_input_enabled(true);
			} else {
				set_input_enabled(false);
				if (is_pressed()) {
					_release();
				}
			}
		} break;

		case NOTIFICATION_SUSPENDED:
		case NOTIFICATION_PAUSED: {
			if (is_pressed()) {
				_release();
			}
		} break;
	}
}

bool TouchScreenButton::is_pressed() const {
	return finger_pressed != -1;
}

void TouchScreenButton::set_action(const String &p_action) {
	action = p_action;
}

String TouchScreenButton::get_action() const {
	return action;
}

void TouchScreenButton::input(const Ref<InputEvent> &p_event) {
	ERR_FAIL_COND(p_event.is_null());

	if (!is_visible_in_stage()) {
		return;
	}

	const InputEventScreenTouch *st = Object::cast_to<InputEventScreenTouch>(*p_event);

	if (passby_press) {
		const InputEventScreenDrag *sd = Object::cast_to<InputEventScreenDrag>(*p_event);

		if (st && !st->is_pressed() && finger_pressed == st->get_index()) {
			_release();
		}

		if ((st && st->is_pressed()) || sd) {
			int index = st ? st->get_index() : sd->get_index();
			Point2 coord = st ? st->get_position() : sd->get_position();

			if (finger_pressed == -1 || index == finger_pressed) {
				if (_is_point_inside(coord)) {
					if (finger_pressed == -1) {
						_press(index);
					}
				} else {
					if (finger_pressed != -1) {
						_release();
					}
				}
			}
		}

	} else {
		if (st) {
			if (st->is_pressed()) {
				const bool can_press = finger_pressed == -1;
				if (!can_press) {
					return; //already fingering
				}

				if (_is_point_inside(st->get_position())) {
					_press(st->get_index());
				}
			} else {
				if (st->get_index() == finger_pressed) {
					_release();
				}
			}
		}
	}
}

bool TouchScreenButton::_is_point_inside(const Point2 &p_point) {
	Point2 coord = (get_global_transform_with_canvas()).affine_inverse().xform(p_point);

	bool touched = false;
	bool check_rect = true;

	if (shape.is_valid()) {
		check_rect = false;

		Vector2 pos;
		if (shape_centered && texture_normal.is_valid()) {
			pos = texture_normal->get_size() * 0.5;
		}

		touched = shape->collide(Transform2D().translated_local(pos), unit_rect, Transform2D(0, coord + Vector2(0.5, 0.5)));
	}

	if (bitmask.is_valid()) {
		check_rect = false;
		if (!touched && Rect2(Point2(), bitmask->get_size()).has_point(coord)) {
			if (bitmask->get_bitv(coord)) {
				touched = true;
			}
		}
	}

	if (!touched && check_rect) {
		if (texture_normal.is_valid()) {
			touched = Rect2(Size2(), texture_normal->get_size()).has_point(coord);
		}
	}

	return touched;
}

void TouchScreenButton::_press(int p_finger_pressed) {
	finger_pressed = p_finger_pressed;

	if (action != StringName()) {
		Input::get_singleton()->action_press(action);
		Ref<InputEventAction> iea;
		iea.instantiate();
		iea->set_action(action);
		iea->set_pressed(true);
		get_viewport()->push_input(iea, true);
	}

	emit_signal(SceneStringName(pressed));
	queue_redraw();
}

void TouchScreenButton::_release(bool p_exiting_tree) {
	finger_pressed = -1;

	if (action != StringName()) {
		Input::get_singleton()->action_release(action);
		if (!p_exiting_tree) {
			Ref<InputEventAction> iea;
			iea.instantiate();
			iea->set_action(action);
			iea->set_pressed(false);
			get_viewport()->push_input(iea, true);
		}
	}

	if (!p_exiting_tree) {
		emit_signal(SNAME("released"));
		queue_redraw();
	}
}

#ifdef DEBUG_ENABLED
Rect2 TouchScreenButton::_edit_get_rect() const {
	if (texture_normal.is_null()) {
		return CanvasItem::_edit_get_rect();
	}

	return Rect2(Size2(), texture_normal->get_size());
}

bool TouchScreenButton::_edit_use_rect() const {
	return texture_normal.is_valid();
}
#endif // DEBUG_ENABLED

Rect2 TouchScreenButton::get_anchorable_rect() const {
	if (texture_normal.is_null()) {
		return CanvasItem::get_anchorable_rect();
	}

	return Rect2(Size2(), texture_normal->get_size());
}

void TouchScreenButton::set_visibility_mode(VisibilityMode p_mode) {
	visibility = p_mode;
	queue_redraw();
}

TouchScreenButton::VisibilityMode TouchScreenButton::get_visibility_mode() const {
	return visibility;
}

void TouchScreenButton::set_passby_press(bool p_enable) {
	passby_press = p_enable;
}

bool TouchScreenButton::is_passby_press_enabled() const {
	return passby_press;
}

void TouchScreenButton::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_texture_normal", "texture"), &TouchScreenButton::set_texture_normal);
	ClassRegistry::bind_method(D_METHOD("get_texture_normal"), &TouchScreenButton::get_texture_normal);

	ClassRegistry::bind_method(D_METHOD("set_texture_pressed", "texture"), &TouchScreenButton::set_texture_pressed);
	ClassRegistry::bind_method(D_METHOD("get_texture_pressed"), &TouchScreenButton::get_texture_pressed);

	ClassRegistry::bind_method(D_METHOD("set_bitmask", "bitmask"), &TouchScreenButton::set_bitmask);
	ClassRegistry::bind_method(D_METHOD("get_bitmask"), &TouchScreenButton::get_bitmask);

	ClassRegistry::bind_method(D_METHOD("set_shape", "shape"), &TouchScreenButton::set_shape);
	ClassRegistry::bind_method(D_METHOD("get_shape"), &TouchScreenButton::get_shape);

	ClassRegistry::bind_method(D_METHOD("set_shape_centered", "bool"), &TouchScreenButton::set_shape_centered);
	ClassRegistry::bind_method(D_METHOD("is_shape_centered"), &TouchScreenButton::is_shape_centered);

	ClassRegistry::bind_method(D_METHOD("set_shape_visible", "bool"), &TouchScreenButton::set_shape_visible);
	ClassRegistry::bind_method(D_METHOD("is_shape_visible"), &TouchScreenButton::is_shape_visible);

	ClassRegistry::bind_method(D_METHOD("set_action", "action"), &TouchScreenButton::set_action);
	ClassRegistry::bind_method(D_METHOD("get_action"), &TouchScreenButton::get_action);

	ClassRegistry::bind_method(D_METHOD("set_visibility_mode", "mode"), &TouchScreenButton::set_visibility_mode);
	ClassRegistry::bind_method(D_METHOD("get_visibility_mode"), &TouchScreenButton::get_visibility_mode);

	ClassRegistry::bind_method(D_METHOD("set_passby_press", "enabled"), &TouchScreenButton::set_passby_press);
	ClassRegistry::bind_method(D_METHOD("is_passby_press_enabled"), &TouchScreenButton::is_passby_press_enabled);

	ClassRegistry::bind_method(D_METHOD("is_pressed"), &TouchScreenButton::is_pressed);

	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "texture_normal", PROPERTY_HINT_RESOURCE_TYPE, Texture2D::get_static_class_name()), "set_texture_normal", "get_texture_normal");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "texture_pressed", PROPERTY_HINT_RESOURCE_TYPE, Texture2D::get_static_class_name()), "set_texture_pressed", "get_texture_pressed");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "bitmask", PROPERTY_HINT_RESOURCE_TYPE, BitMap::get_static_class_name()), "set_bitmask", "get_bitmask");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "shape", PROPERTY_HINT_RESOURCE_TYPE, Shape2D::get_static_class_name()), "set_shape", "get_shape");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "shape_centered"), "set_shape_centered", "is_shape_centered");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "shape_visible"), "set_shape_visible", "is_shape_visible");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "passby_press"), "set_passby_press", "is_passby_press_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::STRING_NAME, "action", PROPERTY_HINT_INPUT_NAME, "show_builtin,loose_mode"), "set_action", "get_action");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "visibility_mode", PROPERTY_HINT_ENUM, "Always,TouchScreen Only"), "set_visibility_mode", "get_visibility_mode");

	ADD_SIGNAL(MethodInfo("pressed"));
	ADD_SIGNAL(MethodInfo("released"));

	BIND_ENUM_CONSTANT(VISIBILITY_ALWAYS);
	BIND_ENUM_CONSTANT(VISIBILITY_TOUCHSCREEN_ONLY);
}

TouchScreenButton::TouchScreenButton() {
	unit_rect.instantiate();
	unit_rect->set_size(Vector2(1, 1));

#if defined(DEBUG_ENABLED) && !defined(PHYSICS_PLANAR_DISABLED)
	PhysicsSystem2D::get_singleton()->connect("_debug_changed", callable_mp((CanvasItem *)this, &CanvasItem::queue_redraw));
#endif
}
