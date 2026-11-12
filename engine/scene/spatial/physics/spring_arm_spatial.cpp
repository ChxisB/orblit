/**************************************************************************/
/*  spring_arm_spatial.cpp                                                     */

#include "spring_arm_spatial.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"
#include "scene/resources/spatial/shape_spatial.h"
#include "scene/spatial/camera_spatial.h"
#include "scene/stage/stage.h"
#include "systems/physics_spatial/direct_states/physics_direct_space_state_spatial.h"
#include "systems/physics_spatial/physics_system_spatial.h"

void SpringArm3D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			if (!Engine::get_singleton()->is_editor_hint()) {
				set_internal_step_enabled(true);
			}
		} break;

		case NOTIFICATION_EXIT_STAGE: {
			if (!Engine::get_singleton()->is_editor_hint()) {
				set_internal_step_enabled(false);
			}
		} break;

		case NOTIFICATION_INTERNAL_STEP: {
			process_spring();
		} break;
	}
}

void SpringArm3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_hit_length"), &SpringArm3D::get_hit_length);

	ClassRegistry::bind_method(D_METHOD("set_length", "length"), &SpringArm3D::set_length);
	ClassRegistry::bind_method(D_METHOD("get_length"), &SpringArm3D::get_length);

	ClassRegistry::bind_method(D_METHOD("set_shape", "shape"), &SpringArm3D::set_shape);
	ClassRegistry::bind_method(D_METHOD("get_shape"), &SpringArm3D::get_shape);

	ClassRegistry::bind_method(D_METHOD("add_excluded_object", "RID"), &SpringArm3D::add_excluded_object);
	ClassRegistry::bind_method(D_METHOD("remove_excluded_object", "RID"), &SpringArm3D::remove_excluded_object);
	ClassRegistry::bind_method(D_METHOD("clear_excluded_objects"), &SpringArm3D::clear_excluded_objects);

	ClassRegistry::bind_method(D_METHOD("set_collision_mask", "mask"), &SpringArm3D::set_mask);
	ClassRegistry::bind_method(D_METHOD("get_collision_mask"), &SpringArm3D::get_mask);

	ClassRegistry::bind_method(D_METHOD("set_margin", "margin"), &SpringArm3D::set_margin);
	ClassRegistry::bind_method(D_METHOD("get_margin"), &SpringArm3D::get_margin);

	ADD_PROPERTY(PropertyInfo(Variant::INT, "collision_mask", PROPERTY_HINT_LAYERS_3D_PHYSICS), "set_collision_mask", "get_collision_mask");
	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "shape", PROPERTY_HINT_RESOURCE_TYPE, Shape3D::get_static_class_name()), "set_shape", "get_shape");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "spring_length", PROPERTY_HINT_NONE, "suffix:m"), "set_length", "get_length");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "margin", PROPERTY_HINT_NONE, "suffix:m"), "set_margin", "get_margin");
}

real_t SpringArm3D::get_length() const {
	return spring_length;
}

void SpringArm3D::set_length(real_t p_length) {
	if (is_inside_stage() && (Engine::get_singleton()->is_editor_hint() || PhysicsSystem3D::get_singleton()->debug_is_enabled())) {
		update_gizmos();
	}

	spring_length = p_length;
}

void SpringArm3D::set_shape(Ref<Shape3D> p_shape) {
	shape = p_shape;
}

Ref<Shape3D> SpringArm3D::get_shape() const {
	return shape;
}

void SpringArm3D::set_mask(uint32_t p_mask) {
	mask = p_mask;
}

uint32_t SpringArm3D::get_mask() {
	return mask;
}

real_t SpringArm3D::get_margin() {
	return margin;
}

void SpringArm3D::set_margin(real_t p_margin) {
	margin = p_margin;
}

void SpringArm3D::add_excluded_object(RID p_rid) {
	excluded_objects.insert(p_rid);
}

bool SpringArm3D::remove_excluded_object(RID p_rid) {
	return excluded_objects.erase(p_rid);
}

void SpringArm3D::clear_excluded_objects() {
	excluded_objects.clear();
}

real_t SpringArm3D::get_hit_length() {
	return current_spring_length;
}

// The arm stops short of what it hit by the margin, however it was cast.
real_t SpringArm3D::_get_delta_with_margin(real_t p_motion_delta) const {
	if (p_motion_delta >= 1 || spring_length <= 0) {
		return p_motion_delta;
	}
	return MAX(real_t(0), p_motion_delta - margin / spring_length);
}

void SpringArm3D::process_spring() {
	// From
	real_t motion_delta(1);
	real_t motion_delta_unsafe(1);

	Vector3 motion;
	const Vector3 cast_direction(get_global_transform().basis.xform(Vector3(0, 0, 1)));

	motion = Vector3(cast_direction * (spring_length));

	if (shape.is_null()) {
		Camera3D *camera = nullptr;
		for (int i = get_child_count() - 1; 0 <= i; --i) {
			camera = Object::cast_to<Camera3D>(get_child(i));
			if (camera) {
				break;
			}
		}

		if (camera != nullptr) {
			//use camera rotation, but spring arm position
			Transform3D base_transform = camera->get_global_transform();
			base_transform.origin = get_global_transform().origin;

			PS3DT::ShapeParameters shape_params;
			shape_params.shape_rid = camera->get_pyramid_shape_rid();
			shape_params.transform = base_transform;
			shape_params.motion = motion;
			shape_params.exclude = excluded_objects;
			shape_params.collision_mask = mask;

			get_world_3d()->get_direct_space_state()->cast_motion(shape_params, motion_delta, motion_delta_unsafe);
		} else {
			PS3DT::RayParameters ray_params;
			ray_params.from = get_global_transform().origin;
			ray_params.to = get_global_transform().origin + motion;
			ray_params.exclude = excluded_objects;
			ray_params.collision_mask = mask;

			PS3DT::RayResult r;
			bool intersected = get_world_3d()->get_direct_space_state()->intersect_ray(ray_params, r);
			if (intersected) {
				real_t dist = get_global_transform().origin.distance_to(r.position);
				motion_delta = dist / (spring_length);
			}
		}
	} else {
		PS3DT::ShapeParameters shape_params;
		shape_params.shape_rid = shape->get_rid();
		shape_params.transform = get_global_transform();
		shape_params.motion = motion;
		shape_params.exclude = excluded_objects;
		shape_params.collision_mask = mask;

		get_world_3d()->get_direct_space_state()->cast_motion(shape_params, motion_delta, motion_delta_unsafe);
	}

	motion_delta = _get_delta_with_margin(motion_delta);
	current_spring_length = spring_length * motion_delta;
	Transform3D child_transform;
	child_transform.origin = get_global_transform().origin + cast_direction * (spring_length * motion_delta);

	for (int i = get_child_count() - 1; 0 <= i; --i) {
		Entity3D *child = Object::cast_to<Entity3D>(get_child(i));
		if (child) {
			child_transform.basis = child->get_global_transform().basis;
			child->set_global_transform(child_transform);
		}
	}
}
