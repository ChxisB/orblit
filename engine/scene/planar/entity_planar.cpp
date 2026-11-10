/**************************************************************************/
/*  entity_planar.cpp                                                         */

#include "entity_planar.h"

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "scene/stage/viewport.h"
#include "systems/display/accessibility_system.h"
#include "systems/rendering/rendering_system.h"

#ifdef DEBUG_ENABLED
Dictionary Entity2D::_edit_get_state() const {
	Dictionary state;
	state["position"] = get_position();
	state["rotation"] = get_rotation();
	state["scale"] = get_scale();
	state["skew"] = get_skew();

	return state;
}

void Entity2D::_edit_set_state(const Dictionary &p_state) {
	position = p_state["position"];
	rotation = p_state["rotation"];
	scale = p_state["scale"];
	skew = p_state["skew"];

	_update_transform();
}

void Entity2D::_edit_set_position(const Point2 &p_position) {
	set_position(p_position);
}

Point2 Entity2D::_edit_get_position() const {
	return position;
}

void Entity2D::_edit_set_scale(const Size2 &p_scale) {
	set_scale(p_scale);
}

Size2 Entity2D::_edit_get_scale() const {
	return scale;
}

void Entity2D::_edit_set_rotation(real_t p_rotation) {
	rotation = p_rotation;
	_update_transform();
}

real_t Entity2D::_edit_get_rotation() const {
	return rotation;
}

bool Entity2D::_edit_use_rotation() const {
	return true;
}

void Entity2D::_edit_set_rect(const Rect2 &p_edit_rect) {
	ERR_FAIL_COND(!_edit_use_rect());

	Rect2 r = _edit_get_rect();

	Vector2 zero_offset;
	Size2 new_scale(1, 1);

	if (r.size.x != 0) {
		zero_offset.x = -r.position.x / r.size.x;
		new_scale.x = p_edit_rect.size.x / r.size.x;
	}

	if (r.size.y != 0) {
		zero_offset.y = -r.position.y / r.size.y;
		new_scale.y = p_edit_rect.size.y / r.size.y;
	}

	Point2 new_pos = p_edit_rect.position + p_edit_rect.size * zero_offset;

	Transform2D postxf;
	postxf.set_rotation_scale_and_skew(rotation, scale, skew);
	new_pos = postxf.xform(new_pos);

	position += new_pos;
	scale *= new_scale;

	_update_transform();
}
#endif // DEBUG_ENABLED

void Entity2D::_set_xform_dirty(bool p_dirty) const {
	if (is_group_processing()) {
		if (p_dirty) {
			xform_dirty.mt.set();
		} else {
			xform_dirty.mt.clear();
		}
	} else {
		xform_dirty.st = p_dirty;
	}
}

void Entity2D::_update_xform_values() const {
	rotation = transform.get_rotation();
	skew = transform.get_skew();
	position = transform.columns[2];
	scale = transform.get_scale();
	_set_xform_dirty(false);
}

void Entity2D::_update_transform() {
	transform.set_rotation_scale_and_skew(rotation, scale, skew);
	transform.columns[2] = position;

	RenderingSystem::get_singleton()->canvas_item_set_transform(get_canvas_item(), transform);

	_notify_transform();
}

void Entity2D::reparent(RequiredParam<Entity> p_parent, bool p_keep_global_transform) {
	ERR_THREAD_GUARD;
	if (p_keep_global_transform) {
		Transform2D temp = get_global_transform();
		Entity::reparent(p_parent);
		set_global_transform(temp);
	} else {
		Entity::reparent(p_parent);
	}
}

void Entity2D::set_position(const Point2 &p_pos) {
	ERR_THREAD_GUARD;
	if (_is_xform_dirty()) {
		_update_xform_values();
	}
	position = p_pos;
	_update_transform();
}

void Entity2D::set_rotation(real_t p_radians) {
	ERR_THREAD_GUARD;
	if (_is_xform_dirty()) {
		_update_xform_values();
	}
	rotation = p_radians;
	_update_transform();
}

void Entity2D::set_rotation_degrees(real_t p_degrees) {
	ERR_THREAD_GUARD;
	set_rotation(Math::deg_to_rad(p_degrees));
}

void Entity2D::set_skew(real_t p_radians) {
	ERR_THREAD_GUARD;
	if (_is_xform_dirty()) {
		_update_xform_values();
	}
	skew = p_radians;
	_update_transform();
}

void Entity2D::set_scale(const Size2 &p_scale) {
	ERR_THREAD_GUARD;
	if (_is_xform_dirty()) {
		_update_xform_values();
	}
	scale = p_scale;
	// Avoid having 0 scale values, can lead to errors in physics and rendering.
	if (Math::is_zero_approx(scale.x)) {
		scale.x = CMP_EPSILON;
	}
	if (Math::is_zero_approx(scale.y)) {
		scale.y = CMP_EPSILON;
	}
	_update_transform();
}

Point2 Entity2D::get_position() const {
	ERR_READ_THREAD_GUARD_V(Point2());
	if (_is_xform_dirty()) {
		_update_xform_values();
	}

	return position;
}

real_t Entity2D::get_rotation() const {
	ERR_READ_THREAD_GUARD_V(0);
	if (_is_xform_dirty()) {
		_update_xform_values();
	}

	return rotation;
}

real_t Entity2D::get_rotation_degrees() const {
	ERR_READ_THREAD_GUARD_V(0);
	return Math::rad_to_deg(get_rotation());
}

real_t Entity2D::get_skew() const {
	ERR_READ_THREAD_GUARD_V(0);
	if (_is_xform_dirty()) {
		_update_xform_values();
	}

	return skew;
}

Size2 Entity2D::get_scale() const {
	ERR_READ_THREAD_GUARD_V(Size2());
	if (_is_xform_dirty()) {
		_update_xform_values();
	}

	return scale;
}

Transform2D Entity2D::get_transform() const {
	ERR_READ_THREAD_GUARD_V(Transform2D());
	return transform;
}

void Entity2D::rotate(real_t p_radians) {
	ERR_THREAD_GUARD;
	set_rotation(get_rotation() + p_radians);
}

void Entity2D::translate(const Vector2 &p_amount) {
	ERR_THREAD_GUARD;
	set_position(get_position() + p_amount);
}

void Entity2D::global_translate(const Vector2 &p_amount) {
	ERR_THREAD_GUARD;
	set_global_position(get_global_position() + p_amount);
}

void Entity2D::apply_scale(const Size2 &p_amount) {
	ERR_THREAD_GUARD;
	set_scale(get_scale() * p_amount);
}

void Entity2D::move_x(real_t p_delta, bool p_scaled) {
	ERR_THREAD_GUARD;
	Transform2D t = get_transform();
	Vector2 m = t[0];
	if (!p_scaled) {
		m.normalize();
	}
	set_position(t[2] + m * p_delta);
}

void Entity2D::move_y(real_t p_delta, bool p_scaled) {
	ERR_THREAD_GUARD;
	Transform2D t = get_transform();
	Vector2 m = t[1];
	if (!p_scaled) {
		m.normalize();
	}
	set_position(t[2] + m * p_delta);
}

Point2 Entity2D::get_global_position() const {
	ERR_READ_THREAD_GUARD_V(Point2());
	return get_global_transform().get_origin();
}

void Entity2D::set_global_position(const Point2 &p_pos) {
	ERR_THREAD_GUARD;
	CanvasItem *parent = get_parent_item();
	if (parent) {
		Transform2D inv = parent->get_global_transform().affine_inverse();
		set_position(inv.xform(p_pos));
	} else {
		set_position(p_pos);
	}
}

real_t Entity2D::get_global_rotation() const {
	ERR_READ_THREAD_GUARD_V(0);
	return get_global_transform().get_rotation();
}

real_t Entity2D::get_global_rotation_degrees() const {
	ERR_READ_THREAD_GUARD_V(0);
	return Math::rad_to_deg(get_global_rotation());
}

real_t Entity2D::get_global_skew() const {
	ERR_READ_THREAD_GUARD_V(0);
	return get_global_transform().get_skew();
}

void Entity2D::set_global_rotation(const real_t p_radians) {
	ERR_THREAD_GUARD;
	CanvasItem *parent = get_parent_item();
	if (parent) {
		Transform2D parent_global_transform = parent->get_global_transform();
		Transform2D new_transform = parent_global_transform * get_transform();
		new_transform.set_rotation(p_radians);
		new_transform = parent_global_transform.affine_inverse() * new_transform;
		set_rotation(new_transform.get_rotation());
	} else {
		set_rotation(p_radians);
	}
}

void Entity2D::set_global_rotation_degrees(const real_t p_degrees) {
	ERR_THREAD_GUARD;
	set_global_rotation(Math::deg_to_rad(p_degrees));
}

void Entity2D::set_global_skew(const real_t p_radians) {
	ERR_THREAD_GUARD;
	CanvasItem *parent = get_parent_item();
	if (parent) {
		Transform2D parent_global_transform = parent->get_global_transform();
		Transform2D new_transform = parent_global_transform * get_transform();
		new_transform.set_skew(p_radians);
		new_transform = parent_global_transform.affine_inverse() * new_transform;
		set_skew(new_transform.get_skew());
	} else {
		set_skew(p_radians);
	}
}

Size2 Entity2D::get_global_scale() const {
	ERR_READ_THREAD_GUARD_V(Size2());
	return get_global_transform().get_scale();
}

void Entity2D::set_global_scale(const Size2 &p_scale) {
	ERR_THREAD_GUARD;
	CanvasItem *parent = get_parent_item();
	if (parent) {
		Transform2D parent_global_transform = parent->get_global_transform();
		Transform2D new_transform = parent_global_transform * get_transform();
		new_transform.set_scale(p_scale);
		new_transform = parent_global_transform.affine_inverse() * new_transform;
		set_scale(new_transform.get_scale());
	} else {
		set_scale(p_scale);
	}
}

void Entity2D::set_transform(const Transform2D &p_transform) {
	ERR_THREAD_GUARD;
	transform = p_transform;
	_set_xform_dirty(true);

	if (!_is_using_identity_transform()) {
		RenderingSystem::get_singleton()->canvas_item_set_transform(get_canvas_item(), transform);
	}

	_notify_transform();
}

void Entity2D::set_global_transform(const Transform2D &p_transform) {
	ERR_THREAD_GUARD;
	CanvasItem *parent = get_parent_item();
	if (parent) {
		set_transform(parent->get_global_transform().affine_inverse() * p_transform);
	} else {
		set_transform(p_transform);
	}
}

Transform2D Entity2D::get_relative_transform_to_parent(RequiredParam<const Entity> p_parent) const {
	EXTRACT_PARAM_OR_FAIL_V(parent, p_parent, Transform2D());
	ERR_READ_THREAD_GUARD_V(Transform2D());
	if (parent == this) {
		return Transform2D();
	}

	Entity2D *parent_2d = Object::cast_to<Entity2D>(get_parent());

	ERR_FAIL_NULL_V(parent_2d, Transform2D());
	if (parent == parent_2d) {
		return get_transform();
	} else {
		return parent_2d->get_relative_transform_to_parent(parent) * get_transform();
	}
}

void Entity2D::look_at(const Vector2 &p_pos) {
	ERR_THREAD_GUARD;
	rotate(get_angle_to(p_pos));
}

real_t Entity2D::get_angle_to(const Vector2 &p_pos) const {
	ERR_READ_THREAD_GUARD_V(0);
	return (to_local(p_pos) * get_scale()).angle();
}

Point2 Entity2D::to_local(const Point2 &p_global) const {
	ERR_READ_THREAD_GUARD_V(Point2());
	return get_global_transform().affine_inverse().xform(p_global);
}

Point2 Entity2D::to_global(const Point2 &p_local) const {
	ERR_READ_THREAD_GUARD_V(Point2());
	return get_global_transform().xform(p_local);
}

void Entity2D::_notification(int p_notification) {
	switch (p_notification) {
		case NOTIFICATION_ACCESSIBILITY_UPDATE: {
			RID ae = get_accessibility_element();
			ERR_FAIL_COND(ae.is_null());

			AccessibilitySystem::get_singleton()->update_set_role(ae, AccessibilitySystemEnums::AccessibilityRole::ROLE_CONTAINER);
		} break;

		case NOTIFICATION_ENTER_STAGE: {
			ERR_MAIN_THREAD_GUARD;

			if (get_viewport()) {
				get_parent()->connect(SNAME("child_order_changed"), callable_mp(get_viewport(), &Viewport::gui_set_root_order_dirty), CONNECT_REFERENCE_COUNTED);
			}
		} break;
		case NOTIFICATION_EXIT_STAGE: {
			ERR_MAIN_THREAD_GUARD;

			if (get_viewport()) {
				get_parent()->disconnect(SNAME("child_order_changed"), callable_mp(get_viewport(), &Viewport::gui_set_root_order_dirty));
			}
		} break;
	}
}

void Entity2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_position", "position"), &Entity2D::set_position);
	ClassRegistry::bind_method(D_METHOD("set_rotation", "radians"), &Entity2D::set_rotation);
	ClassRegistry::bind_method(D_METHOD("set_rotation_degrees", "degrees"), &Entity2D::set_rotation_degrees);
	ClassRegistry::bind_method(D_METHOD("set_skew", "radians"), &Entity2D::set_skew);
	ClassRegistry::bind_method(D_METHOD("set_scale", "scale"), &Entity2D::set_scale);

	ClassRegistry::bind_method(D_METHOD("get_position"), &Entity2D::get_position);
	ClassRegistry::bind_method(D_METHOD("get_rotation"), &Entity2D::get_rotation);
	ClassRegistry::bind_method(D_METHOD("get_rotation_degrees"), &Entity2D::get_rotation_degrees);
	ClassRegistry::bind_method(D_METHOD("get_skew"), &Entity2D::get_skew);
	ClassRegistry::bind_method(D_METHOD("get_scale"), &Entity2D::get_scale);

	ClassRegistry::bind_method(D_METHOD("rotate", "radians"), &Entity2D::rotate);
	ClassRegistry::bind_method(D_METHOD("move_local_x", "delta", "scaled"), &Entity2D::move_x, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("move_local_y", "delta", "scaled"), &Entity2D::move_y, DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("translate", "offset"), &Entity2D::translate);
	ClassRegistry::bind_method(D_METHOD("global_translate", "offset"), &Entity2D::global_translate);
	ClassRegistry::bind_method(D_METHOD("apply_scale", "ratio"), &Entity2D::apply_scale);

	ClassRegistry::bind_method(D_METHOD("set_global_position", "position"), &Entity2D::set_global_position);
	ClassRegistry::bind_method(D_METHOD("get_global_position"), &Entity2D::get_global_position);
	ClassRegistry::bind_method(D_METHOD("set_global_rotation", "radians"), &Entity2D::set_global_rotation);
	ClassRegistry::bind_method(D_METHOD("set_global_rotation_degrees", "degrees"), &Entity2D::set_global_rotation_degrees);
	ClassRegistry::bind_method(D_METHOD("get_global_rotation"), &Entity2D::get_global_rotation);
	ClassRegistry::bind_method(D_METHOD("get_global_rotation_degrees"), &Entity2D::get_global_rotation_degrees);
	ClassRegistry::bind_method(D_METHOD("set_global_skew", "radians"), &Entity2D::set_global_skew);
	ClassRegistry::bind_method(D_METHOD("get_global_skew"), &Entity2D::get_global_skew);
	ClassRegistry::bind_method(D_METHOD("set_global_scale", "scale"), &Entity2D::set_global_scale);
	ClassRegistry::bind_method(D_METHOD("get_global_scale"), &Entity2D::get_global_scale);

	ClassRegistry::bind_method(D_METHOD("set_transform", "xform"), &Entity2D::set_transform);
	ClassRegistry::bind_method(D_METHOD("set_global_transform", "xform"), &Entity2D::set_global_transform);

	ClassRegistry::bind_method(D_METHOD("look_at", "point"), &Entity2D::look_at);
	ClassRegistry::bind_method(D_METHOD("get_angle_to", "point"), &Entity2D::get_angle_to);

	ClassRegistry::bind_method(D_METHOD("to_local", "global_point"), &Entity2D::to_local);
	ClassRegistry::bind_method(D_METHOD("to_global", "local_point"), &Entity2D::to_global);

	ClassRegistry::bind_method(D_METHOD("get_relative_transform_to_parent", "parent"), &Entity2D::get_relative_transform_to_parent);

	ADD_GROUP("Transform", "");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "position", PROPERTY_HINT_RANGE, "-99999,99999,or_less,or_greater,hide_control,suffix:px"), "set_position", "get_position");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "rotation", PROPERTY_HINT_RANGE, "-360,360,0.1,or_less,or_greater,radians_as_degrees"), "set_rotation", "get_rotation");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "rotation_degrees", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_rotation_degrees", "get_rotation_degrees");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "scale", PROPERTY_HINT_LINK), "set_scale", "get_scale");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "skew", PROPERTY_HINT_RANGE, "-89.9,89.9,0.1,radians_as_degrees"), "set_skew", "get_skew");
	ADD_PROPERTY(PropertyInfo(Variant::TRANSFORM2D, "transform", PROPERTY_HINT_NONE, "suffix:px", PROPERTY_USAGE_NONE), "set_transform", "get_transform");

	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "global_position", PROPERTY_HINT_NONE, "suffix:px", PROPERTY_USAGE_NONE), "set_global_position", "get_global_position");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "global_rotation", PROPERTY_HINT_NONE, "radians_as_degrees", PROPERTY_USAGE_NONE), "set_global_rotation", "get_global_rotation");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "global_rotation_degrees", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_global_rotation_degrees", "get_global_rotation_degrees");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "global_scale", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_global_scale", "get_global_scale");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "global_skew", PROPERTY_HINT_NONE, "radians_as_degrees", PROPERTY_USAGE_NONE), "set_global_skew", "get_global_skew");
	ADD_PROPERTY(PropertyInfo(Variant::TRANSFORM2D, "global_transform", PROPERTY_HINT_NONE, "suffix:px", PROPERTY_USAGE_NONE), "set_global_transform", "get_global_transform");
}

Entity2D::Entity2D() {
	_define_ancestry(AncestralClass::NODE_2D);
}
