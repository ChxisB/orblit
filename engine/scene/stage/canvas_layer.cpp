/**************************************************************************/
/*  canvas_layer.cpp                                                      */

#include "canvas_layer.h"

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "scene/resources/world_planar.h"
#include "scene/stage/canvas_item.h"
#include "scene/stage/viewport.h"
#include "systems/rendering/rendering_system.h"

void CanvasLayer::set_layer(int p_xform) {
	layer = p_xform;
	if (viewport.is_valid()) {
		RenderingSystem::get_singleton()->viewport_set_canvas_stacking(viewport, canvas, layer, get_index());
		vp->gui_set_root_order_dirty();
	}
}

int CanvasLayer::get_layer() const {
	return layer;
}

void CanvasLayer::set_visible(bool p_visible) {
	if (p_visible == visible) {
		return;
	}

	visible = p_visible;
	emit_signal(SceneStringName(visibility_changed));

	for (int i = 0; i < get_child_count(); i++) {
		CanvasItem *c = Object::cast_to<CanvasItem>(get_child(i));
		if (c) {
			RenderingSystem::get_singleton()->canvas_item_set_visible(c->get_canvas_item(), p_visible && c->is_visible());

			c->_propagate_visibility_changed(p_visible);
		}
	}
}

void CanvasLayer::show() {
	set_visible(true);
}

void CanvasLayer::hide() {
	set_visible(false);
}

bool CanvasLayer::is_visible() const {
	return visible;
}

void CanvasLayer::set_transform(const Transform2D &p_xform) {
	transform = p_xform;
	locrotscale_dirty = true;
	if (viewport.is_valid()) {
		RenderingSystem::get_singleton()->viewport_set_canvas_transform(viewport, canvas, transform);
	}
}

Transform2D CanvasLayer::get_transform() const {
	return transform;
}

Transform2D CanvasLayer::get_final_transform() const {
	if (is_following_viewport()) {
		Transform2D follow;
		follow.scale(Vector2(get_follow_viewport_scale(), get_follow_viewport_scale()));
		if (vp) {
			follow = vp->get_canvas_transform() * follow;
		}
		return follow * transform;
	}
	return transform;
}

void CanvasLayer::_update_xform() {
	transform.set_rotation_and_scale(rot, scale);
	transform.set_origin(ofs);
	if (viewport.is_valid()) {
		RenderingSystem::get_singleton()->viewport_set_canvas_transform(viewport, canvas, transform);
	}
}

void CanvasLayer::_update_locrotscale() const {
	ofs = transform.columns[2];
	rot = transform.get_rotation();
	scale = transform.get_scale();
	locrotscale_dirty = false;
}

void CanvasLayer::set_offset(const Vector2 &p_offset) {
	if (locrotscale_dirty) {
		_update_locrotscale();
	}

	ofs = p_offset;
	_update_xform();
}

Vector2 CanvasLayer::get_offset() const {
	if (locrotscale_dirty) {
		_update_locrotscale();
	}

	return ofs;
}

void CanvasLayer::set_rotation(real_t p_radians) {
	if (locrotscale_dirty) {
		_update_locrotscale();
	}

	rot = p_radians;
	_update_xform();
}

real_t CanvasLayer::get_rotation() const {
	if (locrotscale_dirty) {
		_update_locrotscale();
	}

	return rot;
}

void CanvasLayer::set_scale(const Vector2 &p_scale) {
	if (locrotscale_dirty) {
		_update_locrotscale();
	}

	scale = p_scale;
	_update_xform();
}

Vector2 CanvasLayer::get_scale() const {
	if (locrotscale_dirty) {
		_update_locrotscale();
	}

	return scale;
}

void CanvasLayer::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			if (custom_viewport && ObjectRegistry::get_instance(custom_viewport_id)) {
				vp = custom_viewport;
			} else {
				vp = Entity::get_viewport();
			}
			ERR_FAIL_NULL_MSG(vp, "Viewport is not initialized.");

			vp->_canvas_layer_add(this);
			viewport = vp->get_viewport_rid();

			RenderingSystem::get_singleton()->viewport_attach_canvas(viewport, canvas);
			RenderingSystem::get_singleton()->viewport_set_canvas_transform(viewport, canvas, transform);
			_update_follow_viewport();

			if (vp) {
				get_parent()->connect(SNAME("child_order_changed"), callable_mp(vp, &Viewport::canvas_parent_mark_dirty).bind(get_parent()), CONNECT_REFERENCE_COUNTED);
				vp->canvas_parent_mark_dirty(get_parent());
			}
		} break;

		case NOTIFICATION_EXIT_STAGE: {
			ERR_FAIL_NULL_MSG(vp, "Viewport is not initialized.");
			get_parent()->disconnect(SNAME("child_order_changed"), callable_mp(vp, &Viewport::canvas_parent_mark_dirty).bind(get_parent()));

			vp->_canvas_layer_remove(this);
			RenderingSystem::get_singleton()->viewport_remove_canvas(viewport, canvas);
			viewport = RID();
			_update_follow_viewport(false);
		} break;
	}
}

void CanvasLayer::update_draw_order() {
	if (is_inside_stage()) {
		RenderingSystem::get_singleton()->viewport_set_canvas_stacking(viewport, canvas, layer, get_index());
	}
}

Size2 CanvasLayer::get_viewport_size() const {
	if (!is_inside_stage()) {
		return Size2(1, 1);
	}

	ERR_FAIL_NULL_V_MSG(vp, Size2(1, 1), "Viewport is not initialized.");

	Rect2 r = vp->get_visible_rect();
	return r.size;
}

RID CanvasLayer::get_viewport() const {
	return viewport;
}

void CanvasLayer::set_custom_viewport(Entity *p_viewport) {
	ERR_FAIL_NULL_MSG(p_viewport, "Cannot set viewport to nullptr.");
	if (is_inside_stage()) {
		vp->_canvas_layer_remove(this);
		RenderingSystem::get_singleton()->viewport_remove_canvas(viewport, canvas);
		viewport = RID();
	}

	custom_viewport = Object::cast_to<Viewport>(p_viewport);

	if (custom_viewport) {
		custom_viewport_id = custom_viewport->get_instance_id();
	} else {
		custom_viewport_id = ObjectID();
	}

	if (is_inside_stage()) {
		if (custom_viewport) {
			vp = custom_viewport;
		} else {
			vp = Entity::get_viewport();
		}

		vp->_canvas_layer_add(this);
		viewport = vp->get_viewport_rid();

		RenderingSystem::get_singleton()->viewport_attach_canvas(viewport, canvas);
		RenderingSystem::get_singleton()->viewport_set_canvas_stacking(viewport, canvas, layer, get_index());
		RenderingSystem::get_singleton()->viewport_set_canvas_transform(viewport, canvas, transform);
	}
}

Entity *CanvasLayer::get_custom_viewport() const {
	return custom_viewport;
}

void CanvasLayer::reset_sort_index() {
	sort_index = 0;
}

int CanvasLayer::get_sort_index() {
	return sort_index++;
}

RID CanvasLayer::get_canvas() const {
	return canvas;
}

void CanvasLayer::set_follow_viewport(bool p_enable) {
	if (follow_viewport == p_enable) {
		return;
	}

	follow_viewport = p_enable;
	_update_follow_viewport();
}

bool CanvasLayer::is_following_viewport() const {
	return follow_viewport;
}

void CanvasLayer::set_follow_viewport_scale(float p_ratio) {
	follow_viewport_scale = p_ratio;
	_update_follow_viewport();
}

float CanvasLayer::get_follow_viewport_scale() const {
	return follow_viewport_scale;
}

void CanvasLayer::_update_follow_viewport(bool p_force_exit) {
	if (!is_inside_stage()) {
		return;
	}
	if (p_force_exit || !follow_viewport) {
		RS::get_singleton()->canvas_set_parent(canvas, RID(), 1.0);
	} else {
		RS::get_singleton()->canvas_set_parent(canvas, vp->get_world_2d()->get_canvas(), follow_viewport_scale);
	}
}

void CanvasLayer::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_layer", "layer"), &CanvasLayer::set_layer);
	ClassRegistry::bind_method(D_METHOD("get_layer"), &CanvasLayer::get_layer);

	ClassRegistry::bind_method(D_METHOD("set_visible", "visible"), &CanvasLayer::set_visible);
	ClassRegistry::bind_method(D_METHOD("is_visible"), &CanvasLayer::is_visible);
	ClassRegistry::bind_method(D_METHOD("show"), &CanvasLayer::show);
	ClassRegistry::bind_method(D_METHOD("hide"), &CanvasLayer::hide);

	ClassRegistry::bind_method(D_METHOD("set_transform", "transform"), &CanvasLayer::set_transform);
	ClassRegistry::bind_method(D_METHOD("get_transform"), &CanvasLayer::get_transform);
	ClassRegistry::bind_method(D_METHOD("get_final_transform"), &CanvasLayer::get_final_transform);

	ClassRegistry::bind_method(D_METHOD("set_offset", "offset"), &CanvasLayer::set_offset);
	ClassRegistry::bind_method(D_METHOD("get_offset"), &CanvasLayer::get_offset);

	ClassRegistry::bind_method(D_METHOD("set_rotation", "radians"), &CanvasLayer::set_rotation);
	ClassRegistry::bind_method(D_METHOD("get_rotation"), &CanvasLayer::get_rotation);

	ClassRegistry::bind_method(D_METHOD("set_scale", "scale"), &CanvasLayer::set_scale);
	ClassRegistry::bind_method(D_METHOD("get_scale"), &CanvasLayer::get_scale);

	ClassRegistry::bind_method(D_METHOD("set_follow_viewport", "enable"), &CanvasLayer::set_follow_viewport);
	ClassRegistry::bind_method(D_METHOD("is_following_viewport"), &CanvasLayer::is_following_viewport);

	ClassRegistry::bind_method(D_METHOD("set_follow_viewport_scale", "scale"), &CanvasLayer::set_follow_viewport_scale);
	ClassRegistry::bind_method(D_METHOD("get_follow_viewport_scale"), &CanvasLayer::get_follow_viewport_scale);

	ClassRegistry::bind_method(D_METHOD("set_custom_viewport", "viewport"), &CanvasLayer::set_custom_viewport);
	ClassRegistry::bind_method(D_METHOD("get_custom_viewport"), &CanvasLayer::get_custom_viewport);

	ClassRegistry::bind_method(D_METHOD("get_canvas"), &CanvasLayer::get_canvas);

	ADD_GROUP("Layer", "");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "layer", PROPERTY_HINT_RANGE, itos(RSE::CANVAS_LAYER_MIN) + "," + itos(RSE::CANVAS_LAYER_MAX) + ",1"), "set_layer", "get_layer");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "visible"), "set_visible", "is_visible");
	ADD_GROUP("Transform", "");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "offset", PROPERTY_HINT_NONE, "suffix:px"), "set_offset", "get_offset");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "rotation", PROPERTY_HINT_RANGE, "-1080,1080,0.1,or_less,or_greater,radians_as_degrees"), "set_rotation", "get_rotation");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "scale", PROPERTY_HINT_LINK), "set_scale", "get_scale");
	ADD_PROPERTY(PropertyInfo(Variant::TRANSFORM2D, "transform", PROPERTY_HINT_NONE, "suffix:px"), "set_transform", "get_transform");
	ADD_GROUP("", "");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "custom_viewport", PROPERTY_HINT_RESOURCE_TYPE, Viewport::get_static_class_name(), PROPERTY_USAGE_NONE), "set_custom_viewport", "get_custom_viewport");
	ADD_GROUP("Follow Viewport", "follow_viewport");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "follow_viewport_enabled", PROPERTY_HINT_GROUP_ENABLE), "set_follow_viewport", "is_following_viewport");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "follow_viewport_scale", PROPERTY_HINT_RANGE, "0.001,1000,0.001,or_greater,or_less"), "set_follow_viewport_scale", "get_follow_viewport_scale");

	ADD_SIGNAL(MethodInfo("visibility_changed"));
}

CanvasLayer::CanvasLayer() {
	canvas = RS::get_singleton()->canvas_create();
}

CanvasLayer::~CanvasLayer() {
	ERR_FAIL_NULL(RenderingSystem::get_singleton());
	RS::get_singleton()->free_rid(canvas);
}
