/**************************************************************************/
/*  physics_system_spatial.cpp                                                 */

#include "physics_system_spatial.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"
#include "core/object/ref_counted.h"

PhysicsSystem3D *PhysicsSystem3D::singleton = nullptr;

PhysicsSystem3D *PhysicsSystem3D::get_singleton() {
	return singleton;
}

void PhysicsSystem3D::debug_set_enabled(bool p_enabled) {
#ifdef DEBUG_ENABLED
	if (debug_enabled == p_enabled) {
		return;
	}
	debug_enabled = p_enabled;
	emit_signal("_debug_changed");
#endif
}

bool PhysicsSystem3D::debug_is_enabled() const {
#ifdef DEBUG_ENABLED
	return debug_enabled;
#else
	return false;
#endif
}

bool PhysicsSystem3D::_body_test_motion(RID p_body, RequiredParam<PhysicsTestMotionParameters3D> p_parameters, const Ref<PhysicsTestMotionResult3D> &p_result) {
	EXTRACT_PARAM_OR_FAIL_V(parameters, p_parameters, false);

	PS3DT::MotionResult *result_ptr = nullptr;
	if (p_result.is_valid()) {
		result_ptr = p_result->get_result_ptr();
	}

	return body_test_motion(p_body, parameters->get_parameters(), result_ptr);
}

RID PhysicsSystem3D::shape_create(PS3DE::ShapeType p_shape) {
	switch (p_shape) {
		case PS3DE::SHAPE_WORLD_BOUNDARY:
			return world_boundary_shape_create();
		case PS3DE::SHAPE_SEPARATION_RAY:
			return separation_ray_shape_create();
		case PS3DE::SHAPE_SPHERE:
			return sphere_shape_create();
		case PS3DE::SHAPE_BOX:
			return box_shape_create();
		case PS3DE::SHAPE_CAPSULE:
			return capsule_shape_create();
		case PS3DE::SHAPE_CYLINDER:
			return cylinder_shape_create();
		case PS3DE::SHAPE_CONVEX_POLYGON:
			return convex_polygon_shape_create();
		case PS3DE::SHAPE_CONCAVE_POLYGON:
			return concave_polygon_shape_create();
		case PS3DE::SHAPE_HEIGHTMAP:
			return heightmap_shape_create();
		case PS3DE::SHAPE_CUSTOM:
			return custom_shape_create();
		default:
			return RID();
	}
}

void PhysicsSystem3D::register_members() {
#ifndef SPATIAL_DISABLED

	ClassRegistry::bind_method(D_METHOD("world_boundary_shape_create"), &PhysicsSystem3D::world_boundary_shape_create);
	ClassRegistry::bind_method(D_METHOD("separation_ray_shape_create"), &PhysicsSystem3D::separation_ray_shape_create);
	ClassRegistry::bind_method(D_METHOD("sphere_shape_create"), &PhysicsSystem3D::sphere_shape_create);
	ClassRegistry::bind_method(D_METHOD("box_shape_create"), &PhysicsSystem3D::box_shape_create);
	ClassRegistry::bind_method(D_METHOD("capsule_shape_create"), &PhysicsSystem3D::capsule_shape_create);
	ClassRegistry::bind_method(D_METHOD("cylinder_shape_create"), &PhysicsSystem3D::cylinder_shape_create);
	ClassRegistry::bind_method(D_METHOD("convex_polygon_shape_create"), &PhysicsSystem3D::convex_polygon_shape_create);
	ClassRegistry::bind_method(D_METHOD("concave_polygon_shape_create"), &PhysicsSystem3D::concave_polygon_shape_create);
	ClassRegistry::bind_method(D_METHOD("heightmap_shape_create"), &PhysicsSystem3D::heightmap_shape_create);
	ClassRegistry::bind_method(D_METHOD("custom_shape_create"), &PhysicsSystem3D::custom_shape_create);

	ClassRegistry::bind_method(D_METHOD("shape_set_data", "shape", "data"), &PhysicsSystem3D::shape_set_data);
	ClassRegistry::bind_method(D_METHOD("shape_set_margin", "shape", "margin"), &PhysicsSystem3D::shape_set_margin);

	ClassRegistry::bind_method(D_METHOD("shape_get_type", "shape"), &PhysicsSystem3D::shape_get_type);
	ClassRegistry::bind_method(D_METHOD("shape_get_data", "shape"), &PhysicsSystem3D::shape_get_data);
	ClassRegistry::bind_method(D_METHOD("shape_get_margin", "shape"), &PhysicsSystem3D::shape_get_margin);

	ClassRegistry::bind_method(D_METHOD("space_create"), &PhysicsSystem3D::space_create);
	ClassRegistry::bind_method(D_METHOD("space_set_active", "space", "active"), &PhysicsSystem3D::space_set_active);
	ClassRegistry::bind_method(D_METHOD("space_is_active", "space"), &PhysicsSystem3D::space_is_active);
	ClassRegistry::bind_method(D_METHOD("space_set_param", "space", "param", "value"), &PhysicsSystem3D::space_set_param);
	ClassRegistry::bind_method(D_METHOD("space_get_param", "space", "param"), &PhysicsSystem3D::space_get_param);
	ClassRegistry::bind_method(D_METHOD("space_get_direct_state", "space"), &PhysicsSystem3D::space_get_direct_state);

	ClassRegistry::bind_method(D_METHOD("area_create"), &PhysicsSystem3D::area_create);
	ClassRegistry::bind_method(D_METHOD("area_set_space", "area", "space"), &PhysicsSystem3D::area_set_space);
	ClassRegistry::bind_method(D_METHOD("area_get_space", "area"), &PhysicsSystem3D::area_get_space);

	ClassRegistry::bind_method(D_METHOD("area_add_shape", "area", "shape", "transform", "disabled"), &PhysicsSystem3D::area_add_shape, DEFVAL(Transform3D()), DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("area_set_shape", "area", "shape_idx", "shape"), &PhysicsSystem3D::area_set_shape);
	ClassRegistry::bind_method(D_METHOD("area_set_shape_transform", "area", "shape_idx", "transform"), &PhysicsSystem3D::area_set_shape_transform);
	ClassRegistry::bind_method(D_METHOD("area_set_shape_disabled", "area", "shape_idx", "disabled"), &PhysicsSystem3D::area_set_shape_disabled);

	ClassRegistry::bind_method(D_METHOD("area_get_shape_count", "area"), &PhysicsSystem3D::area_get_shape_count);
	ClassRegistry::bind_method(D_METHOD("area_get_shape", "area", "shape_idx"), &PhysicsSystem3D::area_get_shape);
	ClassRegistry::bind_method(D_METHOD("area_get_shape_transform", "area", "shape_idx"), &PhysicsSystem3D::area_get_shape_transform);

	ClassRegistry::bind_method(D_METHOD("area_remove_shape", "area", "shape_idx"), &PhysicsSystem3D::area_remove_shape);
	ClassRegistry::bind_method(D_METHOD("area_clear_shapes", "area"), &PhysicsSystem3D::area_clear_shapes);

	ClassRegistry::bind_method(D_METHOD("area_set_collision_layer", "area", "layer"), &PhysicsSystem3D::area_set_collision_layer);
	ClassRegistry::bind_method(D_METHOD("area_get_collision_layer", "area"), &PhysicsSystem3D::area_get_collision_layer);

	ClassRegistry::bind_method(D_METHOD("area_set_collision_mask", "area", "mask"), &PhysicsSystem3D::area_set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("area_get_collision_mask", "area"), &PhysicsSystem3D::area_get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("area_set_param", "area", "param", "value"), &PhysicsSystem3D::area_set_param);
	ClassRegistry::bind_method(D_METHOD("area_set_transform", "area", "transform"), &PhysicsSystem3D::area_set_transform);

	ClassRegistry::bind_method(D_METHOD("area_get_param", "area", "param"), &PhysicsSystem3D::area_get_param);
	ClassRegistry::bind_method(D_METHOD("area_get_transform", "area"), &PhysicsSystem3D::area_get_transform);

	ClassRegistry::bind_method(D_METHOD("area_attach_object_instance_id", "area", "id"), &PhysicsSystem3D::area_attach_object_instance_id);
	ClassRegistry::bind_method(D_METHOD("area_get_object_instance_id", "area"), &PhysicsSystem3D::area_get_object_instance_id);

	ClassRegistry::bind_method(D_METHOD("area_set_monitor_callback", "area", "callback"), &PhysicsSystem3D::area_set_monitor_callback);
	ClassRegistry::bind_method(D_METHOD("area_set_area_monitor_callback", "area", "callback"), &PhysicsSystem3D::area_set_area_monitor_callback);
	ClassRegistry::bind_method(D_METHOD("area_set_monitorable", "area", "monitorable"), &PhysicsSystem3D::area_set_monitorable);

	ClassRegistry::bind_method(D_METHOD("area_set_ray_pickable", "area", "enable"), &PhysicsSystem3D::area_set_ray_pickable);

	ClassRegistry::bind_method(D_METHOD("body_create"), &PhysicsSystem3D::body_create);

	ClassRegistry::bind_method(D_METHOD("body_set_space", "body", "space"), &PhysicsSystem3D::body_set_space);
	ClassRegistry::bind_method(D_METHOD("body_get_space", "body"), &PhysicsSystem3D::body_get_space);

	ClassRegistry::bind_method(D_METHOD("body_set_mode", "body", "mode"), &PhysicsSystem3D::body_set_mode);
	ClassRegistry::bind_method(D_METHOD("body_get_mode", "body"), &PhysicsSystem3D::body_get_mode);

	ClassRegistry::bind_method(D_METHOD("body_set_collision_layer", "body", "layer"), &PhysicsSystem3D::body_set_collision_layer);
	ClassRegistry::bind_method(D_METHOD("body_get_collision_layer", "body"), &PhysicsSystem3D::body_get_collision_layer);

	ClassRegistry::bind_method(D_METHOD("body_set_collision_mask", "body", "mask"), &PhysicsSystem3D::body_set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("body_get_collision_mask", "body"), &PhysicsSystem3D::body_get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("body_set_collision_priority", "body", "priority"), &PhysicsSystem3D::body_set_collision_priority);
	ClassRegistry::bind_method(D_METHOD("body_get_collision_priority", "body"), &PhysicsSystem3D::body_get_collision_priority);

	ClassRegistry::bind_method(D_METHOD("body_add_shape", "body", "shape", "transform", "disabled"), &PhysicsSystem3D::body_add_shape, DEFVAL(Transform3D()), DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("body_set_shape", "body", "shape_idx", "shape"), &PhysicsSystem3D::body_set_shape);
	ClassRegistry::bind_method(D_METHOD("body_set_shape_transform", "body", "shape_idx", "transform"), &PhysicsSystem3D::body_set_shape_transform);
	ClassRegistry::bind_method(D_METHOD("body_set_shape_disabled", "body", "shape_idx", "disabled"), &PhysicsSystem3D::body_set_shape_disabled);

	ClassRegistry::bind_method(D_METHOD("body_get_shape_count", "body"), &PhysicsSystem3D::body_get_shape_count);
	ClassRegistry::bind_method(D_METHOD("body_get_shape", "body", "shape_idx"), &PhysicsSystem3D::body_get_shape);
	ClassRegistry::bind_method(D_METHOD("body_get_shape_transform", "body", "shape_idx"), &PhysicsSystem3D::body_get_shape_transform);

	ClassRegistry::bind_method(D_METHOD("body_remove_shape", "body", "shape_idx"), &PhysicsSystem3D::body_remove_shape);
	ClassRegistry::bind_method(D_METHOD("body_clear_shapes", "body"), &PhysicsSystem3D::body_clear_shapes);

	ClassRegistry::bind_method(D_METHOD("body_attach_object_instance_id", "body", "id"), &PhysicsSystem3D::body_attach_object_instance_id);
	ClassRegistry::bind_method(D_METHOD("body_get_object_instance_id", "body"), &PhysicsSystem3D::body_get_object_instance_id);

	ClassRegistry::bind_method(D_METHOD("body_set_enable_continuous_collision_detection", "body", "enable"), &PhysicsSystem3D::body_set_enable_continuous_collision_detection);
	ClassRegistry::bind_method(D_METHOD("body_is_continuous_collision_detection_enabled", "body"), &PhysicsSystem3D::body_is_continuous_collision_detection_enabled);

	ClassRegistry::bind_method(D_METHOD("body_set_param", "body", "param", "value"), &PhysicsSystem3D::body_set_param);
	ClassRegistry::bind_method(D_METHOD("body_get_param", "body", "param"), &PhysicsSystem3D::body_get_param);

	ClassRegistry::bind_method(D_METHOD("body_reset_mass_properties", "body"), &PhysicsSystem3D::body_reset_mass_properties);

	ClassRegistry::bind_method(D_METHOD("body_set_state", "body", "state", "value"), &PhysicsSystem3D::body_set_state);
	ClassRegistry::bind_method(D_METHOD("body_get_state", "body", "state"), &PhysicsSystem3D::body_get_state);

	ClassRegistry::bind_method(D_METHOD("body_apply_central_impulse", "body", "impulse"), &PhysicsSystem3D::body_apply_central_impulse);
	ClassRegistry::bind_method(D_METHOD("body_apply_impulse", "body", "impulse", "position"), &PhysicsSystem3D::body_apply_impulse, Vector3());
	ClassRegistry::bind_method(D_METHOD("body_apply_torque_impulse", "body", "impulse"), &PhysicsSystem3D::body_apply_torque_impulse);

	ClassRegistry::bind_method(D_METHOD("body_apply_central_force", "body", "force"), &PhysicsSystem3D::body_apply_central_force);
	ClassRegistry::bind_method(D_METHOD("body_apply_force", "body", "force", "position"), &PhysicsSystem3D::body_apply_force, Vector3());
	ClassRegistry::bind_method(D_METHOD("body_apply_torque", "body", "torque"), &PhysicsSystem3D::body_apply_torque);

	ClassRegistry::bind_method(D_METHOD("body_add_constant_central_force", "body", "force"), &PhysicsSystem3D::body_add_constant_central_force);
	ClassRegistry::bind_method(D_METHOD("body_add_constant_force", "body", "force", "position"), &PhysicsSystem3D::body_add_constant_force, Vector3());
	ClassRegistry::bind_method(D_METHOD("body_add_constant_torque", "body", "torque"), &PhysicsSystem3D::body_add_constant_torque);

	ClassRegistry::bind_method(D_METHOD("body_set_constant_force", "body", "force"), &PhysicsSystem3D::body_set_constant_force);
	ClassRegistry::bind_method(D_METHOD("body_get_constant_force", "body"), &PhysicsSystem3D::body_get_constant_force);

	ClassRegistry::bind_method(D_METHOD("body_set_constant_torque", "body", "torque"), &PhysicsSystem3D::body_set_constant_torque);
	ClassRegistry::bind_method(D_METHOD("body_get_constant_torque", "body"), &PhysicsSystem3D::body_get_constant_torque);

	ClassRegistry::bind_method(D_METHOD("body_set_axis_velocity", "body", "axis_velocity"), &PhysicsSystem3D::body_set_axis_velocity);

	ClassRegistry::bind_method(D_METHOD("body_set_axis_lock", "body", "axis", "lock"), &PhysicsSystem3D::body_set_axis_lock);
	ClassRegistry::bind_method(D_METHOD("body_is_axis_locked", "body", "axis"), &PhysicsSystem3D::body_is_axis_locked);

	ClassRegistry::bind_method(D_METHOD("body_add_collision_exception", "body", "excepted_body"), &PhysicsSystem3D::body_add_collision_exception);
	ClassRegistry::bind_method(D_METHOD("body_remove_collision_exception", "body", "excepted_body"), &PhysicsSystem3D::body_remove_collision_exception);

	ClassRegistry::bind_method(D_METHOD("body_set_max_contacts_reported", "body", "amount"), &PhysicsSystem3D::body_set_max_contacts_reported);
	ClassRegistry::bind_method(D_METHOD("body_get_max_contacts_reported", "body"), &PhysicsSystem3D::body_get_max_contacts_reported);

	ClassRegistry::bind_method(D_METHOD("body_set_omit_force_integration", "body", "enable"), &PhysicsSystem3D::body_set_omit_force_integration);
	ClassRegistry::bind_method(D_METHOD("body_is_omitting_force_integration", "body"), &PhysicsSystem3D::body_is_omitting_force_integration);

	ClassRegistry::bind_method(D_METHOD("body_set_state_sync_callback", "body", "callable"), &PhysicsSystem3D::body_set_state_sync_callback);

	ClassRegistry::bind_method(D_METHOD("body_set_force_integration_callback", "body", "callable", "userdata"), &PhysicsSystem3D::body_set_force_integration_callback, DEFVAL(Variant()));

	ClassRegistry::bind_method(D_METHOD("body_set_ray_pickable", "body", "enable"), &PhysicsSystem3D::body_set_ray_pickable);

	ClassRegistry::bind_method(D_METHOD("body_test_motion", "body", "parameters", "result"), &PhysicsSystem3D::_body_test_motion, DEFVAL(Variant()));

	ClassRegistry::bind_method(D_METHOD("body_get_direct_state", "body"), &PhysicsSystem3D::body_get_direct_state);

	/* SOFT BODY API */

	ClassRegistry::bind_method(D_METHOD("soft_body_create"), &PhysicsSystem3D::soft_body_create);

	ClassRegistry::bind_method(D_METHOD("soft_body_update_rendering_system", "body", "rendering_system_handler"), &PhysicsSystem3D::soft_body_update_rendering_system);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_space", "body", "space"), &PhysicsSystem3D::soft_body_set_space);
	ClassRegistry::bind_method(D_METHOD("soft_body_get_space", "body"), &PhysicsSystem3D::soft_body_get_space);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_mesh", "body", "mesh"), &PhysicsSystem3D::soft_body_set_mesh);

	ClassRegistry::bind_method(D_METHOD("soft_body_get_bounds", "body"), &PhysicsSystem3D::soft_body_get_bounds);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_collision_layer", "body", "layer"), &PhysicsSystem3D::soft_body_set_collision_layer);
	ClassRegistry::bind_method(D_METHOD("soft_body_get_collision_layer", "body"), &PhysicsSystem3D::soft_body_get_collision_layer);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_collision_mask", "body", "mask"), &PhysicsSystem3D::soft_body_set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("soft_body_get_collision_mask", "body"), &PhysicsSystem3D::soft_body_get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("soft_body_add_collision_exception", "body", "body_b"), &PhysicsSystem3D::soft_body_add_collision_exception);
	ClassRegistry::bind_method(D_METHOD("soft_body_remove_collision_exception", "body", "body_b"), &PhysicsSystem3D::soft_body_remove_collision_exception);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_state", "body", "state", "variant"), &PhysicsSystem3D::soft_body_set_state);
	ClassRegistry::bind_method(D_METHOD("soft_body_get_state", "body", "state"), &PhysicsSystem3D::soft_body_get_state);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_transform", "body", "transform"), &PhysicsSystem3D::soft_body_set_transform);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_ray_pickable", "body", "enable"), &PhysicsSystem3D::soft_body_set_ray_pickable);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_simulation_precision", "body", "simulation_precision"), &PhysicsSystem3D::soft_body_set_simulation_precision);
	ClassRegistry::bind_method(D_METHOD("soft_body_get_simulation_precision", "body"), &PhysicsSystem3D::soft_body_get_simulation_precision);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_total_mass", "body", "total_mass"), &PhysicsSystem3D::soft_body_set_total_mass);
	ClassRegistry::bind_method(D_METHOD("soft_body_get_total_mass", "body"), &PhysicsSystem3D::soft_body_get_total_mass);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_linear_stiffness", "body", "stiffness"), &PhysicsSystem3D::soft_body_set_linear_stiffness);
	ClassRegistry::bind_method(D_METHOD("soft_body_get_linear_stiffness", "body"), &PhysicsSystem3D::soft_body_get_linear_stiffness);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_shrinking_factor", "body", "shrinking_factor"), &PhysicsSystem3D::soft_body_set_shrinking_factor);
	ClassRegistry::bind_method(D_METHOD("soft_body_get_shrinking_factor", "body"), &PhysicsSystem3D::soft_body_get_shrinking_factor);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_pressure_coefficient", "body", "pressure_coefficient"), &PhysicsSystem3D::soft_body_set_pressure_coefficient);
	ClassRegistry::bind_method(D_METHOD("soft_body_get_pressure_coefficient", "body"), &PhysicsSystem3D::soft_body_get_pressure_coefficient);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_damping_coefficient", "body", "damping_coefficient"), &PhysicsSystem3D::soft_body_set_damping_coefficient);
	ClassRegistry::bind_method(D_METHOD("soft_body_get_damping_coefficient", "body"), &PhysicsSystem3D::soft_body_get_damping_coefficient);

	ClassRegistry::bind_method(D_METHOD("soft_body_set_drag_coefficient", "body", "drag_coefficient"), &PhysicsSystem3D::soft_body_set_drag_coefficient);
	ClassRegistry::bind_method(D_METHOD("soft_body_get_drag_coefficient", "body"), &PhysicsSystem3D::soft_body_get_drag_coefficient);

	ClassRegistry::bind_method(D_METHOD("soft_body_move_point", "body", "point_index", "global_position"), &PhysicsSystem3D::soft_body_move_point);
	ClassRegistry::bind_method(D_METHOD("soft_body_get_point_global_position", "body", "point_index"), &PhysicsSystem3D::soft_body_get_point_global_position);

	ClassRegistry::bind_method(D_METHOD("soft_body_remove_all_pinned_points", "body"), &PhysicsSystem3D::soft_body_remove_all_pinned_points);

	ClassRegistry::bind_method(D_METHOD("soft_body_pin_point", "body", "point_index", "pin"), &PhysicsSystem3D::soft_body_pin_point);

	ClassRegistry::bind_method(D_METHOD("soft_body_is_point_pinned", "body", "point_index"), &PhysicsSystem3D::soft_body_is_point_pinned);

	ClassRegistry::bind_method(D_METHOD("soft_body_apply_point_impulse", "body", "point_index", "impulse"), &PhysicsSystem3D::soft_body_apply_point_impulse);
	ClassRegistry::bind_method(D_METHOD("soft_body_apply_point_force", "body", "point_index", "force"), &PhysicsSystem3D::soft_body_apply_point_force);
	ClassRegistry::bind_method(D_METHOD("soft_body_apply_central_impulse", "body", "impulse"), &PhysicsSystem3D::soft_body_apply_central_impulse);
	ClassRegistry::bind_method(D_METHOD("soft_body_apply_central_force", "body", "force"), &PhysicsSystem3D::soft_body_apply_central_force);

	/* JOINT API */

	ClassRegistry::bind_method(D_METHOD("joint_create"), &PhysicsSystem3D::joint_create);
	ClassRegistry::bind_method(D_METHOD("joint_clear", "joint"), &PhysicsSystem3D::joint_clear);

	BIND_ENUM_CONSTANT(PS3DE::JOINT_TYPE_PIN);
	BIND_ENUM_CONSTANT(PS3DE::JOINT_TYPE_HINGE);
	BIND_ENUM_CONSTANT(PS3DE::JOINT_TYPE_SLIDER);
	BIND_ENUM_CONSTANT(PS3DE::JOINT_TYPE_CONE_TWIST);
	BIND_ENUM_CONSTANT(PS3DE::JOINT_TYPE_6DOF);
	BIND_ENUM_CONSTANT(PS3DE::JOINT_TYPE_MAX);

	ClassRegistry::bind_method(D_METHOD("joint_make_pin", "joint", "body_A", "local_A", "body_B", "local_B"), &PhysicsSystem3D::joint_make_pin);
	ClassRegistry::bind_method(D_METHOD("pin_joint_set_param", "joint", "param", "value"), &PhysicsSystem3D::pin_joint_set_param);
	ClassRegistry::bind_method(D_METHOD("pin_joint_get_param", "joint", "param"), &PhysicsSystem3D::pin_joint_get_param);

	ClassRegistry::bind_method(D_METHOD("pin_joint_set_local_a", "joint", "local_A"), &PhysicsSystem3D::pin_joint_set_local_a);
	ClassRegistry::bind_method(D_METHOD("pin_joint_get_local_a", "joint"), &PhysicsSystem3D::pin_joint_get_local_a);

	ClassRegistry::bind_method(D_METHOD("pin_joint_set_local_b", "joint", "local_B"), &PhysicsSystem3D::pin_joint_set_local_b);
	ClassRegistry::bind_method(D_METHOD("pin_joint_get_local_b", "joint"), &PhysicsSystem3D::pin_joint_get_local_b);

	BIND_ENUM_CONSTANT(PS3DE::PIN_JOINT_BIAS);
	BIND_ENUM_CONSTANT(PS3DE::PIN_JOINT_DAMPING);
	BIND_ENUM_CONSTANT(PS3DE::PIN_JOINT_IMPULSE_CLAMP);

	BIND_ENUM_CONSTANT(PS3DE::HINGE_JOINT_BIAS);
	BIND_ENUM_CONSTANT(PS3DE::HINGE_JOINT_LIMIT_UPPER);
	BIND_ENUM_CONSTANT(PS3DE::HINGE_JOINT_LIMIT_LOWER);
	BIND_ENUM_CONSTANT(PS3DE::HINGE_JOINT_LIMIT_BIAS);
	BIND_ENUM_CONSTANT(PS3DE::HINGE_JOINT_LIMIT_SOFTNESS);
	BIND_ENUM_CONSTANT(PS3DE::HINGE_JOINT_LIMIT_RELAXATION);
	BIND_ENUM_CONSTANT(PS3DE::HINGE_JOINT_MOTOR_TARGET_VELOCITY);
	BIND_ENUM_CONSTANT(PS3DE::HINGE_JOINT_MOTOR_MAX_IMPULSE);

	BIND_ENUM_CONSTANT(PS3DE::HINGE_JOINT_FLAG_USE_LIMIT);
	BIND_ENUM_CONSTANT(PS3DE::HINGE_JOINT_FLAG_ENABLE_MOTOR);

	ClassRegistry::bind_method(D_METHOD("joint_make_hinge", "joint", "body_A", "hinge_A", "body_B", "hinge_B"), &PhysicsSystem3D::joint_make_hinge);

	ClassRegistry::bind_method(D_METHOD("hinge_joint_set_param", "joint", "param", "value"), &PhysicsSystem3D::hinge_joint_set_param);
	ClassRegistry::bind_method(D_METHOD("hinge_joint_get_param", "joint", "param"), &PhysicsSystem3D::hinge_joint_get_param);

	ClassRegistry::bind_method(D_METHOD("hinge_joint_set_flag", "joint", "flag", "enabled"), &PhysicsSystem3D::hinge_joint_set_flag);
	ClassRegistry::bind_method(D_METHOD("hinge_joint_get_flag", "joint", "flag"), &PhysicsSystem3D::hinge_joint_get_flag);

	ClassRegistry::bind_method(D_METHOD("joint_make_slider", "joint", "body_A", "local_ref_A", "body_B", "local_ref_B"), &PhysicsSystem3D::joint_make_slider);

	ClassRegistry::bind_method(D_METHOD("slider_joint_set_param", "joint", "param", "value"), &PhysicsSystem3D::slider_joint_set_param);
	ClassRegistry::bind_method(D_METHOD("slider_joint_get_param", "joint", "param"), &PhysicsSystem3D::slider_joint_get_param);

	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_LINEAR_LIMIT_UPPER);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_LINEAR_LIMIT_LOWER);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_LINEAR_LIMIT_SOFTNESS);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_LINEAR_LIMIT_RESTITUTION);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_LINEAR_LIMIT_DAMPING);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_LINEAR_MOTION_SOFTNESS);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_LINEAR_MOTION_RESTITUTION);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_LINEAR_MOTION_DAMPING);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_LINEAR_ORTHOGONAL_SOFTNESS);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_LINEAR_ORTHOGONAL_RESTITUTION);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_LINEAR_ORTHOGONAL_DAMPING);

	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_ANGULAR_LIMIT_UPPER);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_ANGULAR_LIMIT_LOWER);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_ANGULAR_LIMIT_SOFTNESS);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_ANGULAR_LIMIT_RESTITUTION);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_ANGULAR_LIMIT_DAMPING);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_ANGULAR_MOTION_SOFTNESS);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_ANGULAR_MOTION_RESTITUTION);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_ANGULAR_MOTION_DAMPING);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_ANGULAR_ORTHOGONAL_SOFTNESS);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_ANGULAR_ORTHOGONAL_RESTITUTION);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_ANGULAR_ORTHOGONAL_DAMPING);
	BIND_ENUM_CONSTANT(PS3DE::SLIDER_JOINT_MAX);

	ClassRegistry::bind_method(D_METHOD("joint_make_cone_twist", "joint", "body_A", "local_ref_A", "body_B", "local_ref_B"), &PhysicsSystem3D::joint_make_cone_twist);

	ClassRegistry::bind_method(D_METHOD("cone_twist_joint_set_param", "joint", "param", "value"), &PhysicsSystem3D::cone_twist_joint_set_param);
	ClassRegistry::bind_method(D_METHOD("cone_twist_joint_get_param", "joint", "param"), &PhysicsSystem3D::cone_twist_joint_get_param);

	BIND_ENUM_CONSTANT(PS3DE::CONE_TWIST_JOINT_SWING_SPAN);
	BIND_ENUM_CONSTANT(PS3DE::CONE_TWIST_JOINT_TWIST_SPAN);
	BIND_ENUM_CONSTANT(PS3DE::CONE_TWIST_JOINT_BIAS);
	BIND_ENUM_CONSTANT(PS3DE::CONE_TWIST_JOINT_SOFTNESS);
	BIND_ENUM_CONSTANT(PS3DE::CONE_TWIST_JOINT_RELAXATION);

	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_LINEAR_LOWER_LIMIT);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_LINEAR_UPPER_LIMIT);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_LINEAR_LIMIT_SOFTNESS);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_LINEAR_RESTITUTION);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_LINEAR_DAMPING);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_LINEAR_MOTOR_TARGET_VELOCITY);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_LINEAR_SPRING_STIFFNESS);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_LINEAR_SPRING_DAMPING);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_LINEAR_SPRING_EQUILIBRIUM_POINT);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_ANGULAR_LOWER_LIMIT);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_ANGULAR_UPPER_LIMIT);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_ANGULAR_LIMIT_SOFTNESS);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_ANGULAR_DAMPING);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_ANGULAR_RESTITUTION);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_ANGULAR_FORCE_LIMIT);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_ANGULAR_ERP);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_ANGULAR_MOTOR_TARGET_VELOCITY);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_ANGULAR_SPRING_STIFFNESS);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_ANGULAR_SPRING_DAMPING);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_ANGULAR_SPRING_EQUILIBRIUM_POINT);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_LINEAR_DRIVE_FORCE_LIMIT);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_ANGULAR_DRIVE_TORQUE_LIMIT);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_MAX);

	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_FLAG_ENABLE_LINEAR_LIMIT);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_FLAG_ENABLE_ANGULAR_LIMIT);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_FLAG_ENABLE_ANGULAR_SPRING);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_FLAG_ENABLE_LINEAR_SPRING);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_FLAG_ENABLE_ANGULAR_MOTOR);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_FLAG_ENABLE_LINEAR_MOTOR);
	BIND_ENUM_CONSTANT(PS3DE::G6DOF_JOINT_FLAG_MAX);

	ClassRegistry::bind_method(D_METHOD("joint_get_type", "joint"), &PhysicsSystem3D::joint_get_type);

	ClassRegistry::bind_method(D_METHOD("joint_set_solver_priority", "joint", "priority"), &PhysicsSystem3D::joint_set_solver_priority);
	ClassRegistry::bind_method(D_METHOD("joint_get_solver_priority", "joint"), &PhysicsSystem3D::joint_get_solver_priority);

	ClassRegistry::bind_method(D_METHOD("joint_disable_collisions_between_bodies", "joint", "disable"), &PhysicsSystem3D::joint_disable_collisions_between_bodies);
	ClassRegistry::bind_method(D_METHOD("joint_is_disabled_collisions_between_bodies", "joint"), &PhysicsSystem3D::joint_is_disabled_collisions_between_bodies);

	ClassRegistry::bind_method(D_METHOD("joint_make_generic_6dof", "joint", "body_A", "local_ref_A", "body_B", "local_ref_B"), &PhysicsSystem3D::joint_make_generic_6dof);

	ClassRegistry::bind_method(D_METHOD("generic_6dof_joint_set_param", "joint", "axis", "param", "value"), &PhysicsSystem3D::generic_6dof_joint_set_param);
	ClassRegistry::bind_method(D_METHOD("generic_6dof_joint_get_param", "joint", "axis", "param"), &PhysicsSystem3D::generic_6dof_joint_get_param);

	ClassRegistry::bind_method(D_METHOD("generic_6dof_joint_set_flag", "joint", "axis", "flag", "enable"), &PhysicsSystem3D::generic_6dof_joint_set_flag);
	ClassRegistry::bind_method(D_METHOD("generic_6dof_joint_get_flag", "joint", "axis", "flag"), &PhysicsSystem3D::generic_6dof_joint_get_flag);

	ClassRegistry::bind_method(D_METHOD("generic_6dof_joint_set_angular_target_rotation", "joint", "target_rotation"), &PhysicsSystem3D::generic_6dof_joint_set_angular_target_rotation);
	ClassRegistry::bind_method(D_METHOD("generic_6dof_joint_get_angular_target_rotation", "joint"), &PhysicsSystem3D::generic_6dof_joint_get_angular_target_rotation);

	ClassRegistry::bind_method(D_METHOD("free_rid", "rid"), &PhysicsSystem3D::free_rid);

	ClassRegistry::bind_method(D_METHOD("set_active", "active"), &PhysicsSystem3D::set_active);

	ClassRegistry::bind_method(D_METHOD("get_process_info", "process_info"), &PhysicsSystem3D::get_process_info);

	BIND_ENUM_CONSTANT(PS3DE::SHAPE_WORLD_BOUNDARY);
	BIND_ENUM_CONSTANT(PS3DE::SHAPE_SEPARATION_RAY);
	BIND_ENUM_CONSTANT(PS3DE::SHAPE_SPHERE);
	BIND_ENUM_CONSTANT(PS3DE::SHAPE_BOX);
	BIND_ENUM_CONSTANT(PS3DE::SHAPE_CAPSULE);
	BIND_ENUM_CONSTANT(PS3DE::SHAPE_CYLINDER);
	BIND_ENUM_CONSTANT(PS3DE::SHAPE_CONVEX_POLYGON);
	BIND_ENUM_CONSTANT(PS3DE::SHAPE_CONCAVE_POLYGON);
	BIND_ENUM_CONSTANT(PS3DE::SHAPE_HEIGHTMAP);
	BIND_ENUM_CONSTANT(PS3DE::SHAPE_SOFT_BODY);
	BIND_ENUM_CONSTANT(PS3DE::SHAPE_CUSTOM);

	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_GRAVITY_OVERRIDE_MODE);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_GRAVITY);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_GRAVITY_VECTOR);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_GRAVITY_IS_POINT);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_GRAVITY_POINT_UNIT_DISTANCE);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_LINEAR_DAMP_OVERRIDE_MODE);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_LINEAR_DAMP);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_ANGULAR_DAMP_OVERRIDE_MODE);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_ANGULAR_DAMP);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_PRIORITY);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_WIND_FORCE_MAGNITUDE);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_WIND_SOURCE);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_WIND_DIRECTION);
	BIND_ENUM_CONSTANT(PS3DE::AREA_PARAM_WIND_ATTENUATION_FACTOR);

	BIND_ENUM_CONSTANT(PS3DE::AREA_SPACE_OVERRIDE_DISABLED);
	BIND_ENUM_CONSTANT(PS3DE::AREA_SPACE_OVERRIDE_COMBINE);
	BIND_ENUM_CONSTANT(PS3DE::AREA_SPACE_OVERRIDE_COMBINE_REPLACE);
	BIND_ENUM_CONSTANT(PS3DE::AREA_SPACE_OVERRIDE_REPLACE);
	BIND_ENUM_CONSTANT(PS3DE::AREA_SPACE_OVERRIDE_REPLACE_COMBINE);

	BIND_ENUM_CONSTANT(PS3DE::BODY_MODE_STATIC);
	BIND_ENUM_CONSTANT(PS3DE::BODY_MODE_KINEMATIC);
	BIND_ENUM_CONSTANT(PS3DE::BODY_MODE_RIGID);
	BIND_ENUM_CONSTANT(PS3DE::BODY_MODE_RIGID_LINEAR);

	BIND_ENUM_CONSTANT(PS3DE::BODY_PARAM_BOUNCE);
	BIND_ENUM_CONSTANT(PS3DE::BODY_PARAM_FRICTION);
	BIND_ENUM_CONSTANT(PS3DE::BODY_PARAM_MASS);
	BIND_ENUM_CONSTANT(PS3DE::BODY_PARAM_INERTIA);
	BIND_ENUM_CONSTANT(PS3DE::BODY_PARAM_CENTER_OF_MASS);
	BIND_ENUM_CONSTANT(PS3DE::BODY_PARAM_GRAVITY_SCALE);
	BIND_ENUM_CONSTANT(PS3DE::BODY_PARAM_LINEAR_DAMP_MODE);
	BIND_ENUM_CONSTANT(PS3DE::BODY_PARAM_ANGULAR_DAMP_MODE);
	BIND_ENUM_CONSTANT(PS3DE::BODY_PARAM_LINEAR_DAMP);
	BIND_ENUM_CONSTANT(PS3DE::BODY_PARAM_ANGULAR_DAMP);
	BIND_ENUM_CONSTANT(PS3DE::BODY_PARAM_MAX);

	BIND_ENUM_CONSTANT(PS3DE::BODY_DAMP_MODE_COMBINE);
	BIND_ENUM_CONSTANT(PS3DE::BODY_DAMP_MODE_REPLACE);

	BIND_ENUM_CONSTANT(PS3DE::BODY_STATE_TRANSFORM);
	BIND_ENUM_CONSTANT(PS3DE::BODY_STATE_LINEAR_VELOCITY);
	BIND_ENUM_CONSTANT(PS3DE::BODY_STATE_ANGULAR_VELOCITY);
	BIND_ENUM_CONSTANT(PS3DE::BODY_STATE_SLEEPING);
	BIND_ENUM_CONSTANT(PS3DE::BODY_STATE_CAN_SLEEP);

	BIND_ENUM_CONSTANT(PS3DE::AREA_BODY_ADDED);
	BIND_ENUM_CONSTANT(PS3DE::AREA_BODY_REMOVED);

	BIND_ENUM_CONSTANT(PS3DE::INFO_ACTIVE_OBJECTS);
	BIND_ENUM_CONSTANT(PS3DE::INFO_COLLISION_PAIRS);
	BIND_ENUM_CONSTANT(PS3DE::INFO_ISLAND_COUNT);

	BIND_ENUM_CONSTANT(PS3DE::SPACE_PARAM_CONTACT_RECYCLE_RADIUS);
	BIND_ENUM_CONSTANT(PS3DE::SPACE_PARAM_CONTACT_MAX_SEPARATION);
	BIND_ENUM_CONSTANT(PS3DE::SPACE_PARAM_CONTACT_MAX_ALLOWED_PENETRATION);
	BIND_ENUM_CONSTANT(PS3DE::SPACE_PARAM_CONTACT_DEFAULT_BIAS);
	BIND_ENUM_CONSTANT(PS3DE::SPACE_PARAM_BODY_LINEAR_VELOCITY_SLEEP_THRESHOLD);
	BIND_ENUM_CONSTANT(PS3DE::SPACE_PARAM_BODY_ANGULAR_VELOCITY_SLEEP_THRESHOLD);
	BIND_ENUM_CONSTANT(PS3DE::SPACE_PARAM_BODY_TIME_TO_SLEEP);
	BIND_ENUM_CONSTANT(PS3DE::SPACE_PARAM_SOLVER_ITERATIONS);

	BIND_ENUM_CONSTANT(PS3DE::BODY_AXIS_LINEAR_X);
	BIND_ENUM_CONSTANT(PS3DE::BODY_AXIS_LINEAR_Y);
	BIND_ENUM_CONSTANT(PS3DE::BODY_AXIS_LINEAR_Z);
	BIND_ENUM_CONSTANT(PS3DE::BODY_AXIS_ANGULAR_X);
	BIND_ENUM_CONSTANT(PS3DE::BODY_AXIS_ANGULAR_Y);
	BIND_ENUM_CONSTANT(PS3DE::BODY_AXIS_ANGULAR_Z);
#endif

#ifdef DEBUG_ENABLED
	ADD_SIGNAL(MethodInfo("_debug_changed"));
#endif
}

PhysicsSystem3D::PhysicsSystem3D() {
	singleton = this;

	// World3D physics space
	GLOBAL_DEF_BASIC(PropertyInfo(Variant::FLOAT, "physics/spatial/default_gravity", PROPERTY_HINT_RANGE, U"-32,32,0.001,or_less,or_greater,suffix:m/s\u00B2"), 9.8);
	GLOBAL_DEF_BASIC(PropertyInfo(Variant::VECTOR3, "physics/spatial/default_gravity_vector", PROPERTY_HINT_RANGE, "-10,10,0.001,or_less,or_greater"), Vector3(0, -1, 0));
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/spatial/default_linear_damp", PROPERTY_HINT_RANGE, "0,100,0.001,or_greater"), 0.1);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/spatial/default_angular_damp", PROPERTY_HINT_RANGE, "0,100,0.001,or_greater"), 0.1);

	// PhysicsSystem3D
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/spatial/sleep_threshold_linear", PROPERTY_HINT_RANGE, "0,1,0.001,or_greater,suffix:m/s"), 0.1);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/spatial/sleep_threshold_angular", PROPERTY_HINT_RANGE, U"0,90,0.1,radians_as_degrees,suffix:°/s"), Math::deg_to_rad(8.0));
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/spatial/time_before_sleep", PROPERTY_HINT_RANGE, "0,5,0.01,or_greater,suffix:s"), 0.5);
	GLOBAL_DEF(PropertyInfo(Variant::INT, "physics/spatial/solver/solver_iterations", PROPERTY_HINT_RANGE, "1,32,1,or_greater"), 16);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/spatial/solver/contact_recycle_radius", PROPERTY_HINT_RANGE, "0,0.1,0.001,or_greater,suffix:m"), 0.01);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/spatial/solver/contact_max_separation", PROPERTY_HINT_RANGE, "0,0.1,0.001,or_greater,suffix:m"), 0.05);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/spatial/solver/contact_max_allowed_penetration", PROPERTY_HINT_RANGE, "0.001,0.1,0.001,or_greater,suffix:m"), 0.01);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/spatial/solver/default_contact_bias", PROPERTY_HINT_RANGE, "0,1,0.01"), 0.8);
}

PhysicsSystem3D::~PhysicsSystem3D() {
	singleton = nullptr;
}
