/**************************************************************************/
/*  ray_cast_planar.cpp                                                       */

#include "ray_cast_planar.h"

#include "core/config/engine.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "scene/planar/physics/collision_object_planar.h"
#include "scene/resources/world_planar.h"
#include "scene/stage/stage.h"
#include "systems/physics_planar/direct_states/physics_direct_space_state_planar.h"
#include "systems/physics_planar/physics_system_planar.h"

void RayCast2D::set_target_position(const Vector2 &p_point) {
	target_position = p_point;
	if (is_inside_stage() && (Engine::get_singleton()->is_editor_hint() || PhysicsSystem2D::get_singleton()->debug_is_enabled())) {
		queue_redraw();
	}
}

Vector2 RayCast2D::get_target_position() const {
	return target_position;
}

void RayCast2D::set_collision_mask(uint32_t p_mask) {
	collision_mask = p_mask;
}

uint32_t RayCast2D::get_collision_mask() const {
	return collision_mask;
}

void RayCast2D::set_collision_mask_value(int p_layer_number, bool p_value) {
	ERR_FAIL_COND_MSG(p_layer_number < 1, "Collision layer number must be between 1 and 32 inclusive.");
	ERR_FAIL_COND_MSG(p_layer_number > 32, "Collision layer number must be between 1 and 32 inclusive.");
	uint32_t mask = get_collision_mask();
	if (p_value) {
		mask |= 1 << (p_layer_number - 1);
	} else {
		mask &= ~(1 << (p_layer_number - 1));
	}
	set_collision_mask(mask);
}

bool RayCast2D::get_collision_mask_value(int p_layer_number) const {
	ERR_FAIL_COND_V_MSG(p_layer_number < 1, false, "Collision layer number must be between 1 and 32 inclusive.");
	ERR_FAIL_COND_V_MSG(p_layer_number > 32, false, "Collision layer number must be between 1 and 32 inclusive.");
	return get_collision_mask() & (1 << (p_layer_number - 1));
}

bool RayCast2D::is_colliding() const {
	return collided;
}

Object *RayCast2D::get_collider() const {
	if (against.is_null()) {
		return nullptr;
	}

	return ObjectRegistry::get_instance(against);
}

RID RayCast2D::get_collider_rid() const {
	return against_rid;
}

int RayCast2D::get_collider_shape() const {
	return against_shape;
}

Vector2 RayCast2D::get_collision_point() const {
	return collision_point;
}

Vector2 RayCast2D::get_collision_normal() const {
	return collision_normal;
}

void RayCast2D::set_enabled(bool p_enabled) {
	enabled = p_enabled;
	queue_redraw();
	if (is_inside_stage() && !Engine::get_singleton()->is_editor_hint()) {
		set_internal_step_enabled(p_enabled);
	}
	if (!p_enabled) {
		collided = false;
	}
}

bool RayCast2D::is_enabled() const {
	return enabled;
}

void RayCast2D::set_exclude_parent_body(bool p_exclude_parent_body) {
	if (exclude_parent_body == p_exclude_parent_body) {
		return;
	}

	exclude_parent_body = p_exclude_parent_body;

	if (!is_inside_stage()) {
		return;
	}

	if (Object::cast_to<CollisionObject2D>(get_parent())) {
		if (exclude_parent_body) {
			exclude.insert(Object::cast_to<CollisionObject2D>(get_parent())->get_rid());
		} else {
			exclude.erase(Object::cast_to<CollisionObject2D>(get_parent())->get_rid());
		}
	}
}

bool RayCast2D::get_exclude_parent_body() const {
	return exclude_parent_body;
}

void RayCast2D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			if (enabled && !Engine::get_singleton()->is_editor_hint()) {
				set_internal_step_enabled(true);
			} else {
				set_internal_step_enabled(false);
			}

			if (Object::cast_to<CollisionObject2D>(get_parent())) {
				if (exclude_parent_body) {
					exclude.insert(Object::cast_to<CollisionObject2D>(get_parent())->get_rid());
				} else {
					exclude.erase(Object::cast_to<CollisionObject2D>(get_parent())->get_rid());
				}
			}
		} break;

		case NOTIFICATION_EXIT_STAGE: {
			if (enabled) {
				set_internal_step_enabled(false);
			}
		} break;

		case NOTIFICATION_DRAW: {
			ERR_FAIL_COND(!is_inside_stage());
			if (!Engine::get_singleton()->is_editor_hint() && !PhysicsSystem2D::get_singleton()->debug_is_enabled()) {
				break;
			}
			_draw_debug_shape();
		} break;

		case NOTIFICATION_INTERNAL_STEP: {
			if (!enabled) {
				break;
			}
			_update_raycast_state();
		} break;
	}
}

void RayCast2D::_update_raycast_state() {
	Ref<World2D> w2d = get_world_2d();
	ERR_FAIL_COND(w2d.is_null());

	PhysicsDirectSpaceState2D *dss = PhysicsSystem2D::get_singleton()->space_get_direct_state(w2d->get_space());
	ERR_FAIL_NULL(dss);

	Transform2D gt = get_global_transform();

	Vector2 to = target_position;
	if (to == Vector2()) {
		to = Vector2(0, 0.01);
	}

	PS2DT::RayResult rr;
	bool prev_collision_state = collided;

	PS2DT::RayParameters ray_params;
	ray_params.from = gt.get_origin();
	ray_params.to = gt.xform(to);
	ray_params.exclude = exclude;
	ray_params.collision_mask = collision_mask;
	ray_params.collide_with_bodies = collide_with_bodies;
	ray_params.collide_with_areas = collide_with_areas;
	ray_params.hit_from_inside = hit_from_inside;

	if (dss->intersect_ray(ray_params, rr)) {
		collided = true;
		against = rr.collider_id;
		against_rid = rr.rid;
		collision_point = rr.position;
		collision_normal = rr.normal;
		against_shape = rr.shape;
	} else {
		collided = false;
		against = ObjectID();
		against_rid = RID();
		against_shape = 0;
	}

	if (prev_collision_state != collided) {
		queue_redraw();
	}
}

void RayCast2D::_draw_debug_shape() {
	Color draw_col = collided ? Color(1.0, 0.01, 0) : get_stage()->get_debug_collisions_color();
	if (!enabled) {
		const float g = draw_col.get_v();
		draw_col.r = g;
		draw_col.g = g;
		draw_col.b = g;
	}

	// Draw an arrow indicating where the RayCast is pointing to
	const real_t max_arrow_size = 6;
	const real_t line_width = 1.4;
	bool no_line = target_position.length() < line_width;
	real_t arrow_size = CLAMP(target_position.length() * 2 / 3, line_width, max_arrow_size);

	if (no_line) {
		arrow_size = target_position.length();
	} else {
		draw_line(Vector2(), target_position - target_position.normalized() * arrow_size, draw_col, line_width);
	}

	Transform2D xf;
	xf.rotate(target_position.angle());
	xf.translate_local(Vector2(no_line ? 0 : target_position.length() - arrow_size, 0));

	Vector<Vector2> pts = {
		xf.xform(Vector2(arrow_size, 0)),
		xf.xform(Vector2(0, 0.5 * arrow_size)),
		xf.xform(Vector2(0, -0.5 * arrow_size))
	};

	Vector<Color> cols = { draw_col, draw_col, draw_col };

	draw_primitive(pts, cols, Vector<Vector2>());
}

void RayCast2D::force_raycast_update() {
	_update_raycast_state();
}

void RayCast2D::add_exception_rid(const RID &p_rid) {
	exclude.insert(p_rid);
}

void RayCast2D::add_exception(RequiredParam<const CollisionObject2D> p_node) {
	EXTRACT_PARAM_OR_FAIL_MSG(node, p_node, "The passed Entity must be an instance of CollisionObject2D.");
	add_exception_rid(node->get_rid());
}

void RayCast2D::remove_exception_rid(const RID &p_rid) {
	exclude.erase(p_rid);
}

void RayCast2D::remove_exception(RequiredParam<const CollisionObject2D> p_node) {
	EXTRACT_PARAM_OR_FAIL_MSG(node, p_node, "The passed Entity must be an instance of CollisionObject2D.");
	remove_exception_rid(node->get_rid());
}

void RayCast2D::clear_exceptions() {
	exclude.clear();

	if (exclude_parent_body && is_inside_stage()) {
		CollisionObject2D *parent = Object::cast_to<CollisionObject2D>(get_parent());
		if (parent) {
			exclude.insert(parent->get_rid());
		}
	}
}

void RayCast2D::set_collide_with_areas(bool p_enabled) {
	collide_with_areas = p_enabled;
}

bool RayCast2D::is_collide_with_areas_enabled() const {
	return collide_with_areas;
}

void RayCast2D::set_collide_with_bodies(bool p_enabled) {
	collide_with_bodies = p_enabled;
}

bool RayCast2D::is_collide_with_bodies_enabled() const {
	return collide_with_bodies;
}

void RayCast2D::set_hit_from_inside(bool p_enabled) {
	hit_from_inside = p_enabled;
}

bool RayCast2D::is_hit_from_inside_enabled() const {
	return hit_from_inside;
}

void RayCast2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_enabled", "enabled"), &RayCast2D::set_enabled);
	ClassRegistry::bind_method(D_METHOD("is_enabled"), &RayCast2D::is_enabled);

	ClassRegistry::bind_method(D_METHOD("set_target_position", "local_point"), &RayCast2D::set_target_position);
	ClassRegistry::bind_method(D_METHOD("get_target_position"), &RayCast2D::get_target_position);

	ClassRegistry::bind_method(D_METHOD("is_colliding"), &RayCast2D::is_colliding);
	ClassRegistry::bind_method(D_METHOD("force_raycast_update"), &RayCast2D::force_raycast_update);

	ClassRegistry::bind_method(D_METHOD("get_collider"), &RayCast2D::get_collider);
	ClassRegistry::bind_method(D_METHOD("get_collider_rid"), &RayCast2D::get_collider_rid);
	ClassRegistry::bind_method(D_METHOD("get_collider_shape"), &RayCast2D::get_collider_shape);
	ClassRegistry::bind_method(D_METHOD("get_collision_point"), &RayCast2D::get_collision_point);
	ClassRegistry::bind_method(D_METHOD("get_collision_normal"), &RayCast2D::get_collision_normal);

	ClassRegistry::bind_method(D_METHOD("add_exception_rid", "rid"), &RayCast2D::add_exception_rid);
	ClassRegistry::bind_method(D_METHOD("add_exception", "node"), &RayCast2D::add_exception);

	ClassRegistry::bind_method(D_METHOD("remove_exception_rid", "rid"), &RayCast2D::remove_exception_rid);
	ClassRegistry::bind_method(D_METHOD("remove_exception", "node"), &RayCast2D::remove_exception);

	ClassRegistry::bind_method(D_METHOD("clear_exceptions"), &RayCast2D::clear_exceptions);

	ClassRegistry::bind_method(D_METHOD("set_collision_mask", "mask"), &RayCast2D::set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("get_collision_mask"), &RayCast2D::get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("set_collision_mask_value", "layer_number", "value"), &RayCast2D::set_collision_mask_value);
	ClassRegistry::bind_method(D_METHOD("get_collision_mask_value", "layer_number"), &RayCast2D::get_collision_mask_value);

	ClassRegistry::bind_method(D_METHOD("set_exclude_parent_body", "mask"), &RayCast2D::set_exclude_parent_body);
	ClassRegistry::bind_method(D_METHOD("get_exclude_parent_body"), &RayCast2D::get_exclude_parent_body);

	ClassRegistry::bind_method(D_METHOD("set_collide_with_areas", "enable"), &RayCast2D::set_collide_with_areas);
	ClassRegistry::bind_method(D_METHOD("is_collide_with_areas_enabled"), &RayCast2D::is_collide_with_areas_enabled);

	ClassRegistry::bind_method(D_METHOD("set_collide_with_bodies", "enable"), &RayCast2D::set_collide_with_bodies);
	ClassRegistry::bind_method(D_METHOD("is_collide_with_bodies_enabled"), &RayCast2D::is_collide_with_bodies_enabled);

	ClassRegistry::bind_method(D_METHOD("set_hit_from_inside", "enable"), &RayCast2D::set_hit_from_inside);
	ClassRegistry::bind_method(D_METHOD("is_hit_from_inside_enabled"), &RayCast2D::is_hit_from_inside_enabled);

	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "enabled"), "set_enabled", "is_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "exclude_parent"), "set_exclude_parent_body", "get_exclude_parent_body");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "target_position", PROPERTY_HINT_NONE, "suffix:px"), "set_target_position", "get_target_position");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collision_mask", PROPERTY_HINT_LAYERS_2D_PHYSICS), "set_collision_mask", "get_collision_mask");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "hit_from_inside"), "set_hit_from_inside", "is_hit_from_inside_enabled");

	ADD_GROUP("Collide With", "collide_with");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_with_areas"), "set_collide_with_areas", "is_collide_with_areas_enabled");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "collide_with_bodies"), "set_collide_with_bodies", "is_collide_with_bodies_enabled");
}

RayCast2D::RayCast2D() {
	set_hide_clip_children(true);

#ifdef DEBUG_ENABLED
	PhysicsSystem2D::get_singleton()->connect("_debug_changed", callable_mp((CanvasItem *)this, &CanvasItem::queue_redraw));
#endif
}
