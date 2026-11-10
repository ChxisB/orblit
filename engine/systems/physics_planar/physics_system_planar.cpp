/**************************************************************************/
/*  physics_system_planar.cpp                                                 */

#include "physics_system_planar.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"

PhysicsSystem2D *PhysicsSystem2D::singleton = nullptr;

PhysicsSystem2D *PhysicsSystem2D::get_singleton() {
	return singleton;
}

void PhysicsSystem2D::debug_set_enabled(bool p_enabled) {
#ifdef DEBUG_ENABLED
	if (debug_enabled == p_enabled) {
		return;
	}
	debug_enabled = p_enabled;
	emit_signal("_debug_changed");
#endif
}

bool PhysicsSystem2D::debug_is_enabled() const {
#ifdef DEBUG_ENABLED
	return debug_enabled;
#else
	return false;
#endif
}

bool PhysicsSystem2D::_body_test_motion(RID p_body, RequiredParam<PhysicsTestMotionParameters2D> p_parameters, const Ref<PhysicsTestMotionResult2D> &p_result) {
	EXTRACT_PARAM_OR_FAIL_V(parameters, p_parameters, false);

	PS2DT::MotionResult *result_ptr = nullptr;
	if (p_result.is_valid()) {
		result_ptr = p_result->get_result_ptr();
	}

	return body_test_motion(p_body, parameters->get_parameters(), result_ptr);
}

void PhysicsSystem2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("world_boundary_shape_create"), &PhysicsSystem2D::world_boundary_shape_create);
	ClassRegistry::bind_method(D_METHOD("separation_ray_shape_create"), &PhysicsSystem2D::separation_ray_shape_create);
	ClassRegistry::bind_method(D_METHOD("segment_shape_create"), &PhysicsSystem2D::segment_shape_create);
	ClassRegistry::bind_method(D_METHOD("circle_shape_create"), &PhysicsSystem2D::circle_shape_create);
	ClassRegistry::bind_method(D_METHOD("rectangle_shape_create"), &PhysicsSystem2D::rectangle_shape_create);
	ClassRegistry::bind_method(D_METHOD("capsule_shape_create"), &PhysicsSystem2D::capsule_shape_create);
	ClassRegistry::bind_method(D_METHOD("convex_polygon_shape_create"), &PhysicsSystem2D::convex_polygon_shape_create);
	ClassRegistry::bind_method(D_METHOD("concave_polygon_shape_create"), &PhysicsSystem2D::concave_polygon_shape_create);

	ClassRegistry::bind_method(D_METHOD("shape_set_data", "shape", "data"), &PhysicsSystem2D::shape_set_data);

	ClassRegistry::bind_method(D_METHOD("shape_get_type", "shape"), &PhysicsSystem2D::shape_get_type);
	ClassRegistry::bind_method(D_METHOD("shape_get_data", "shape"), &PhysicsSystem2D::shape_get_data);

	ClassRegistry::bind_method(D_METHOD("space_create"), &PhysicsSystem2D::space_create);
	ClassRegistry::bind_method(D_METHOD("space_set_active", "space", "active"), &PhysicsSystem2D::space_set_active);
	ClassRegistry::bind_method(D_METHOD("space_is_active", "space"), &PhysicsSystem2D::space_is_active);
	ClassRegistry::bind_method(D_METHOD("space_set_param", "space", "param", "value"), &PhysicsSystem2D::space_set_param);
	ClassRegistry::bind_method(D_METHOD("space_get_param", "space", "param"), &PhysicsSystem2D::space_get_param);
	ClassRegistry::bind_method(D_METHOD("space_get_direct_state", "space"), &PhysicsSystem2D::space_get_direct_state);

	ClassRegistry::bind_method(D_METHOD("area_create"), &PhysicsSystem2D::area_create);
	ClassRegistry::bind_method(D_METHOD("area_set_space", "area", "space"), &PhysicsSystem2D::area_set_space);
	ClassRegistry::bind_method(D_METHOD("area_get_space", "area"), &PhysicsSystem2D::area_get_space);

	ClassRegistry::bind_method(D_METHOD("area_add_shape", "area", "shape", "transform", "disabled"), &PhysicsSystem2D::area_add_shape, DEFVAL(Transform2D()), DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("area_set_shape", "area", "shape_idx", "shape"), &PhysicsSystem2D::area_set_shape);
	ClassRegistry::bind_method(D_METHOD("area_set_shape_transform", "area", "shape_idx", "transform"), &PhysicsSystem2D::area_set_shape_transform);
	ClassRegistry::bind_method(D_METHOD("area_set_shape_disabled", "area", "shape_idx", "disabled"), &PhysicsSystem2D::area_set_shape_disabled);

	ClassRegistry::bind_method(D_METHOD("area_get_shape_count", "area"), &PhysicsSystem2D::area_get_shape_count);
	ClassRegistry::bind_method(D_METHOD("area_get_shape", "area", "shape_idx"), &PhysicsSystem2D::area_get_shape);
	ClassRegistry::bind_method(D_METHOD("area_get_shape_transform", "area", "shape_idx"), &PhysicsSystem2D::area_get_shape_transform);

	ClassRegistry::bind_method(D_METHOD("area_remove_shape", "area", "shape_idx"), &PhysicsSystem2D::area_remove_shape);
	ClassRegistry::bind_method(D_METHOD("area_clear_shapes", "area"), &PhysicsSystem2D::area_clear_shapes);

	ClassRegistry::bind_method(D_METHOD("area_set_collision_layer", "area", "layer"), &PhysicsSystem2D::area_set_collision_layer);
	ClassRegistry::bind_method(D_METHOD("area_get_collision_layer", "area"), &PhysicsSystem2D::area_get_collision_layer);

	ClassRegistry::bind_method(D_METHOD("area_set_collision_mask", "area", "mask"), &PhysicsSystem2D::area_set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("area_get_collision_mask", "area"), &PhysicsSystem2D::area_get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("area_set_param", "area", "param", "value"), &PhysicsSystem2D::area_set_param);
	ClassRegistry::bind_method(D_METHOD("area_set_transform", "area", "transform"), &PhysicsSystem2D::area_set_transform);

	ClassRegistry::bind_method(D_METHOD("area_get_param", "area", "param"), &PhysicsSystem2D::area_get_param);
	ClassRegistry::bind_method(D_METHOD("area_get_transform", "area"), &PhysicsSystem2D::area_get_transform);

	ClassRegistry::bind_method(D_METHOD("area_attach_object_instance_id", "area", "id"), &PhysicsSystem2D::area_attach_object_instance_id);
	ClassRegistry::bind_method(D_METHOD("area_get_object_instance_id", "area"), &PhysicsSystem2D::area_get_object_instance_id);

	ClassRegistry::bind_method(D_METHOD("area_attach_canvas_instance_id", "area", "id"), &PhysicsSystem2D::area_attach_canvas_instance_id);
	ClassRegistry::bind_method(D_METHOD("area_get_canvas_instance_id", "area"), &PhysicsSystem2D::area_get_canvas_instance_id);

	ClassRegistry::bind_method(D_METHOD("area_set_monitor_callback", "area", "callback"), &PhysicsSystem2D::area_set_monitor_callback);
	ClassRegistry::bind_method(D_METHOD("area_set_area_monitor_callback", "area", "callback"), &PhysicsSystem2D::area_set_area_monitor_callback);
	ClassRegistry::bind_method(D_METHOD("area_set_monitorable", "area", "monitorable"), &PhysicsSystem2D::area_set_monitorable);

	ClassRegistry::bind_method(D_METHOD("body_create"), &PhysicsSystem2D::body_create);

	ClassRegistry::bind_method(D_METHOD("body_set_space", "body", "space"), &PhysicsSystem2D::body_set_space);
	ClassRegistry::bind_method(D_METHOD("body_get_space", "body"), &PhysicsSystem2D::body_get_space);

	ClassRegistry::bind_method(D_METHOD("body_set_mode", "body", "mode"), &PhysicsSystem2D::body_set_mode);
	ClassRegistry::bind_method(D_METHOD("body_get_mode", "body"), &PhysicsSystem2D::body_get_mode);

	ClassRegistry::bind_method(D_METHOD("body_add_shape", "body", "shape", "transform", "disabled"), &PhysicsSystem2D::body_add_shape, DEFVAL(Transform2D()), DEFVAL(false));
	ClassRegistry::bind_method(D_METHOD("body_set_shape", "body", "shape_idx", "shape"), &PhysicsSystem2D::body_set_shape);
	ClassRegistry::bind_method(D_METHOD("body_set_shape_transform", "body", "shape_idx", "transform"), &PhysicsSystem2D::body_set_shape_transform);

	ClassRegistry::bind_method(D_METHOD("body_get_shape_count", "body"), &PhysicsSystem2D::body_get_shape_count);
	ClassRegistry::bind_method(D_METHOD("body_get_shape", "body", "shape_idx"), &PhysicsSystem2D::body_get_shape);
	ClassRegistry::bind_method(D_METHOD("body_get_shape_transform", "body", "shape_idx"), &PhysicsSystem2D::body_get_shape_transform);

	ClassRegistry::bind_method(D_METHOD("body_remove_shape", "body", "shape_idx"), &PhysicsSystem2D::body_remove_shape);
	ClassRegistry::bind_method(D_METHOD("body_clear_shapes", "body"), &PhysicsSystem2D::body_clear_shapes);

	ClassRegistry::bind_method(D_METHOD("body_set_shape_disabled", "body", "shape_idx", "disabled"), &PhysicsSystem2D::body_set_shape_disabled);
	ClassRegistry::bind_method(D_METHOD("body_set_shape_as_one_way_collision", "body", "shape_idx", "enable", "margin", "direction"), &PhysicsSystem2D::body_set_shape_as_one_way_collision, Vector2(0, 1));

	ClassRegistry::bind_method(D_METHOD("body_attach_object_instance_id", "body", "id"), &PhysicsSystem2D::body_attach_object_instance_id);
	ClassRegistry::bind_method(D_METHOD("body_get_object_instance_id", "body"), &PhysicsSystem2D::body_get_object_instance_id);

	ClassRegistry::bind_method(D_METHOD("body_attach_canvas_instance_id", "body", "id"), &PhysicsSystem2D::body_attach_canvas_instance_id);
	ClassRegistry::bind_method(D_METHOD("body_get_canvas_instance_id", "body"), &PhysicsSystem2D::body_get_canvas_instance_id);

	ClassRegistry::bind_method(D_METHOD("body_set_continuous_collision_detection_mode", "body", "mode"), &PhysicsSystem2D::body_set_continuous_collision_detection_mode);
	ClassRegistry::bind_method(D_METHOD("body_get_continuous_collision_detection_mode", "body"), &PhysicsSystem2D::body_get_continuous_collision_detection_mode);

	ClassRegistry::bind_method(D_METHOD("body_set_collision_layer", "body", "layer"), &PhysicsSystem2D::body_set_collision_layer);
	ClassRegistry::bind_method(D_METHOD("body_get_collision_layer", "body"), &PhysicsSystem2D::body_get_collision_layer);

	ClassRegistry::bind_method(D_METHOD("body_set_collision_mask", "body", "mask"), &PhysicsSystem2D::body_set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("body_get_collision_mask", "body"), &PhysicsSystem2D::body_get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("body_set_collision_priority", "body", "priority"), &PhysicsSystem2D::body_set_collision_priority);
	ClassRegistry::bind_method(D_METHOD("body_get_collision_priority", "body"), &PhysicsSystem2D::body_get_collision_priority);

	ClassRegistry::bind_method(D_METHOD("body_set_param", "body", "param", "value"), &PhysicsSystem2D::body_set_param);
	ClassRegistry::bind_method(D_METHOD("body_get_param", "body", "param"), &PhysicsSystem2D::body_get_param);

	ClassRegistry::bind_method(D_METHOD("body_reset_mass_properties", "body"), &PhysicsSystem2D::body_reset_mass_properties);

	ClassRegistry::bind_method(D_METHOD("body_set_state", "body", "state", "value"), &PhysicsSystem2D::body_set_state);
	ClassRegistry::bind_method(D_METHOD("body_get_state", "body", "state"), &PhysicsSystem2D::body_get_state);

	ClassRegistry::bind_method(D_METHOD("body_apply_central_impulse", "body", "impulse"), &PhysicsSystem2D::body_apply_central_impulse);
	ClassRegistry::bind_method(D_METHOD("body_apply_torque_impulse", "body", "impulse"), &PhysicsSystem2D::body_apply_torque_impulse);
	ClassRegistry::bind_method(D_METHOD("body_apply_impulse", "body", "impulse", "position"), &PhysicsSystem2D::body_apply_impulse, Vector2());

	ClassRegistry::bind_method(D_METHOD("body_apply_central_force", "body", "force"), &PhysicsSystem2D::body_apply_central_force);
	ClassRegistry::bind_method(D_METHOD("body_apply_force", "body", "force", "position"), &PhysicsSystem2D::body_apply_force, Vector2());
	ClassRegistry::bind_method(D_METHOD("body_apply_torque", "body", "torque"), &PhysicsSystem2D::body_apply_torque);

	ClassRegistry::bind_method(D_METHOD("body_add_constant_central_force", "body", "force"), &PhysicsSystem2D::body_add_constant_central_force);
	ClassRegistry::bind_method(D_METHOD("body_add_constant_force", "body", "force", "position"), &PhysicsSystem2D::body_add_constant_force, Vector2());
	ClassRegistry::bind_method(D_METHOD("body_add_constant_torque", "body", "torque"), &PhysicsSystem2D::body_add_constant_torque);

	ClassRegistry::bind_method(D_METHOD("body_set_constant_force", "body", "force"), &PhysicsSystem2D::body_set_constant_force);
	ClassRegistry::bind_method(D_METHOD("body_get_constant_force", "body"), &PhysicsSystem2D::body_get_constant_force);

	ClassRegistry::bind_method(D_METHOD("body_set_constant_torque", "body", "torque"), &PhysicsSystem2D::body_set_constant_torque);
	ClassRegistry::bind_method(D_METHOD("body_get_constant_torque", "body"), &PhysicsSystem2D::body_get_constant_torque);

	ClassRegistry::bind_method(D_METHOD("body_set_axis_velocity", "body", "axis_velocity"), &PhysicsSystem2D::body_set_axis_velocity);

	ClassRegistry::bind_method(D_METHOD("body_add_collision_exception", "body", "excepted_body"), &PhysicsSystem2D::body_add_collision_exception);
	ClassRegistry::bind_method(D_METHOD("body_remove_collision_exception", "body", "excepted_body"), &PhysicsSystem2D::body_remove_collision_exception);

	ClassRegistry::bind_method(D_METHOD("body_set_max_contacts_reported", "body", "amount"), &PhysicsSystem2D::body_set_max_contacts_reported);
	ClassRegistry::bind_method(D_METHOD("body_get_max_contacts_reported", "body"), &PhysicsSystem2D::body_get_max_contacts_reported);

	ClassRegistry::bind_method(D_METHOD("body_set_omit_force_integration", "body", "enable"), &PhysicsSystem2D::body_set_omit_force_integration);
	ClassRegistry::bind_method(D_METHOD("body_is_omitting_force_integration", "body"), &PhysicsSystem2D::body_is_omitting_force_integration);

	ClassRegistry::bind_method(D_METHOD("body_set_state_sync_callback", "body", "callable"), &PhysicsSystem2D::body_set_state_sync_callback);

	ClassRegistry::bind_method(D_METHOD("body_set_force_integration_callback", "body", "callable", "userdata"), &PhysicsSystem2D::body_set_force_integration_callback, DEFVAL(Variant()));

	ClassRegistry::bind_method(D_METHOD("body_test_motion", "body", "parameters", "result"), &PhysicsSystem2D::_body_test_motion, DEFVAL(Variant()));

	ClassRegistry::bind_method(D_METHOD("body_get_direct_state", "body"), &PhysicsSystem2D::body_get_direct_state);

	/* JOINT API */

	ClassRegistry::bind_method(D_METHOD("joint_create"), &PhysicsSystem2D::joint_create);

	ClassRegistry::bind_method(D_METHOD("joint_clear", "joint"), &PhysicsSystem2D::joint_clear);

	ClassRegistry::bind_method(D_METHOD("joint_set_param", "joint", "param", "value"), &PhysicsSystem2D::joint_set_param);
	ClassRegistry::bind_method(D_METHOD("joint_get_param", "joint", "param"), &PhysicsSystem2D::joint_get_param);

	ClassRegistry::bind_method(D_METHOD("joint_disable_collisions_between_bodies", "joint", "disable"), &PhysicsSystem2D::joint_disable_collisions_between_bodies);
	ClassRegistry::bind_method(D_METHOD("joint_is_disabled_collisions_between_bodies", "joint"), &PhysicsSystem2D::joint_is_disabled_collisions_between_bodies);

	ClassRegistry::bind_method(D_METHOD("joint_make_pin", "joint", "anchor", "body_a", "body_b"), &PhysicsSystem2D::joint_make_pin, DEFVAL(RID()));
	ClassRegistry::bind_method(D_METHOD("joint_make_groove", "joint", "groove1_a", "groove2_a", "anchor_b", "body_a", "body_b"), &PhysicsSystem2D::joint_make_groove, DEFVAL(RID()), DEFVAL(RID()));
	ClassRegistry::bind_method(D_METHOD("joint_make_damped_spring", "joint", "anchor_a", "anchor_b", "body_a", "body_b"), &PhysicsSystem2D::joint_make_damped_spring, DEFVAL(RID()));

	ClassRegistry::bind_method(D_METHOD("pin_joint_set_flag", "joint", "flag", "enabled"), &PhysicsSystem2D::pin_joint_set_flag);
	ClassRegistry::bind_method(D_METHOD("pin_joint_get_flag", "joint", "flag"), &PhysicsSystem2D::pin_joint_get_flag);

	ClassRegistry::bind_method(D_METHOD("pin_joint_set_param", "joint", "param", "value"), &PhysicsSystem2D::pin_joint_set_param);
	ClassRegistry::bind_method(D_METHOD("pin_joint_get_param", "joint", "param"), &PhysicsSystem2D::pin_joint_get_param);

	ClassRegistry::bind_method(D_METHOD("damped_spring_joint_set_param", "joint", "param", "value"), &PhysicsSystem2D::damped_spring_joint_set_param);
	ClassRegistry::bind_method(D_METHOD("damped_spring_joint_get_param", "joint", "param"), &PhysicsSystem2D::damped_spring_joint_get_param);

	ClassRegistry::bind_method(D_METHOD("joint_get_type", "joint"), &PhysicsSystem2D::joint_get_type);

	ClassRegistry::bind_method(D_METHOD("free_rid", "rid"), &PhysicsSystem2D::free_rid);

	ClassRegistry::bind_method(D_METHOD("set_active", "active"), &PhysicsSystem2D::set_active);

	ClassRegistry::bind_method(D_METHOD("get_process_info", "process_info"), &PhysicsSystem2D::get_process_info);

	BIND_ENUM_CONSTANT(PS2DE::SPACE_PARAM_CONTACT_RECYCLE_RADIUS);
	BIND_ENUM_CONSTANT(PS2DE::SPACE_PARAM_CONTACT_MAX_SEPARATION);
	BIND_ENUM_CONSTANT(PS2DE::SPACE_PARAM_CONTACT_MAX_ALLOWED_PENETRATION);
	BIND_ENUM_CONSTANT(PS2DE::SPACE_PARAM_CONTACT_DEFAULT_BIAS);
	BIND_ENUM_CONSTANT(PS2DE::SPACE_PARAM_BODY_LINEAR_VELOCITY_SLEEP_THRESHOLD);
	BIND_ENUM_CONSTANT(PS2DE::SPACE_PARAM_BODY_ANGULAR_VELOCITY_SLEEP_THRESHOLD);
	BIND_ENUM_CONSTANT(PS2DE::SPACE_PARAM_BODY_TIME_TO_SLEEP);
	BIND_ENUM_CONSTANT(PS2DE::SPACE_PARAM_CONSTRAINT_DEFAULT_BIAS);
	BIND_ENUM_CONSTANT(PS2DE::SPACE_PARAM_SOLVER_ITERATIONS);

	BIND_ENUM_CONSTANT(PS2DE::SHAPE_WORLD_BOUNDARY);
	BIND_ENUM_CONSTANT(PS2DE::SHAPE_SEPARATION_RAY);
	BIND_ENUM_CONSTANT(PS2DE::SHAPE_SEGMENT);
	BIND_ENUM_CONSTANT(PS2DE::SHAPE_CIRCLE);
	BIND_ENUM_CONSTANT(PS2DE::SHAPE_RECTANGLE);
	BIND_ENUM_CONSTANT(PS2DE::SHAPE_CAPSULE);
	BIND_ENUM_CONSTANT(PS2DE::SHAPE_CONVEX_POLYGON);
	BIND_ENUM_CONSTANT(PS2DE::SHAPE_CONCAVE_POLYGON);
	BIND_ENUM_CONSTANT(PS2DE::SHAPE_CUSTOM);

	BIND_ENUM_CONSTANT(PS2DE::AREA_PARAM_GRAVITY_OVERRIDE_MODE);
	BIND_ENUM_CONSTANT(PS2DE::AREA_PARAM_GRAVITY);
	BIND_ENUM_CONSTANT(PS2DE::AREA_PARAM_GRAVITY_VECTOR);
	BIND_ENUM_CONSTANT(PS2DE::AREA_PARAM_GRAVITY_IS_POINT);
	BIND_ENUM_CONSTANT(PS2DE::AREA_PARAM_GRAVITY_POINT_UNIT_DISTANCE);
	BIND_ENUM_CONSTANT(PS2DE::AREA_PARAM_LINEAR_DAMP_OVERRIDE_MODE);
	BIND_ENUM_CONSTANT(PS2DE::AREA_PARAM_LINEAR_DAMP);
	BIND_ENUM_CONSTANT(PS2DE::AREA_PARAM_ANGULAR_DAMP_OVERRIDE_MODE);
	BIND_ENUM_CONSTANT(PS2DE::AREA_PARAM_ANGULAR_DAMP);
	BIND_ENUM_CONSTANT(PS2DE::AREA_PARAM_PRIORITY);

	BIND_ENUM_CONSTANT(PS2DE::AREA_SPACE_OVERRIDE_DISABLED);
	BIND_ENUM_CONSTANT(PS2DE::AREA_SPACE_OVERRIDE_COMBINE);
	BIND_ENUM_CONSTANT(PS2DE::AREA_SPACE_OVERRIDE_COMBINE_REPLACE);
	BIND_ENUM_CONSTANT(PS2DE::AREA_SPACE_OVERRIDE_REPLACE);
	BIND_ENUM_CONSTANT(PS2DE::AREA_SPACE_OVERRIDE_REPLACE_COMBINE);

	BIND_ENUM_CONSTANT(PS2DE::BODY_MODE_STATIC);
	BIND_ENUM_CONSTANT(PS2DE::BODY_MODE_KINEMATIC);
	BIND_ENUM_CONSTANT(PS2DE::BODY_MODE_RIGID);
	BIND_ENUM_CONSTANT(PS2DE::BODY_MODE_RIGID_LINEAR);

	BIND_ENUM_CONSTANT(PS2DE::BODY_PARAM_BOUNCE);
	BIND_ENUM_CONSTANT(PS2DE::BODY_PARAM_FRICTION);
	BIND_ENUM_CONSTANT(PS2DE::BODY_PARAM_MASS);
	BIND_ENUM_CONSTANT(PS2DE::BODY_PARAM_INERTIA);
	BIND_ENUM_CONSTANT(PS2DE::BODY_PARAM_CENTER_OF_MASS);
	BIND_ENUM_CONSTANT(PS2DE::BODY_PARAM_GRAVITY_SCALE);
	BIND_ENUM_CONSTANT(PS2DE::BODY_PARAM_LINEAR_DAMP_MODE);
	BIND_ENUM_CONSTANT(PS2DE::BODY_PARAM_ANGULAR_DAMP_MODE);
	BIND_ENUM_CONSTANT(PS2DE::BODY_PARAM_LINEAR_DAMP);
	BIND_ENUM_CONSTANT(PS2DE::BODY_PARAM_ANGULAR_DAMP);
	BIND_ENUM_CONSTANT(PS2DE::BODY_PARAM_MAX);

	BIND_ENUM_CONSTANT(PS2DE::BODY_DAMP_MODE_COMBINE);
	BIND_ENUM_CONSTANT(PS2DE::BODY_DAMP_MODE_REPLACE);

	BIND_ENUM_CONSTANT(PS2DE::BODY_STATE_TRANSFORM);
	BIND_ENUM_CONSTANT(PS2DE::BODY_STATE_LINEAR_VELOCITY);
	BIND_ENUM_CONSTANT(PS2DE::BODY_STATE_ANGULAR_VELOCITY);
	BIND_ENUM_CONSTANT(PS2DE::BODY_STATE_SLEEPING);
	BIND_ENUM_CONSTANT(PS2DE::BODY_STATE_CAN_SLEEP);

	BIND_ENUM_CONSTANT(PS2DE::JOINT_TYPE_PIN);
	BIND_ENUM_CONSTANT(PS2DE::JOINT_TYPE_GROOVE);
	BIND_ENUM_CONSTANT(PS2DE::JOINT_TYPE_DAMPED_SPRING);
	BIND_ENUM_CONSTANT(PS2DE::JOINT_TYPE_MAX);

	BIND_ENUM_CONSTANT(PS2DE::JOINT_PARAM_BIAS);
	BIND_ENUM_CONSTANT(PS2DE::JOINT_PARAM_MAX_BIAS);
	BIND_ENUM_CONSTANT(PS2DE::JOINT_PARAM_MAX_FORCE);

	BIND_ENUM_CONSTANT(PS2DE::PIN_JOINT_SOFTNESS);
	BIND_ENUM_CONSTANT(PS2DE::PIN_JOINT_LIMIT_UPPER);
	BIND_ENUM_CONSTANT(PS2DE::PIN_JOINT_LIMIT_LOWER);
	BIND_ENUM_CONSTANT(PS2DE::PIN_JOINT_MOTOR_TARGET_VELOCITY);

	BIND_ENUM_CONSTANT(PS2DE::PIN_JOINT_FLAG_ANGULAR_LIMIT_ENABLED);
	BIND_ENUM_CONSTANT(PS2DE::PIN_JOINT_FLAG_MOTOR_ENABLED);

	BIND_ENUM_CONSTANT(PS2DE::DAMPED_SPRING_REST_LENGTH);
	BIND_ENUM_CONSTANT(PS2DE::DAMPED_SPRING_STIFFNESS);
	BIND_ENUM_CONSTANT(PS2DE::DAMPED_SPRING_DAMPING);

	BIND_ENUM_CONSTANT(PS2DE::CCD_MODE_DISABLED);
	BIND_ENUM_CONSTANT(PS2DE::CCD_MODE_CAST_RAY);
	BIND_ENUM_CONSTANT(PS2DE::CCD_MODE_CAST_SHAPE);

	BIND_ENUM_CONSTANT(PS2DE::AREA_BODY_ADDED);
	BIND_ENUM_CONSTANT(PS2DE::AREA_BODY_REMOVED);

	BIND_ENUM_CONSTANT(PS2DE::INFO_ACTIVE_OBJECTS);
	BIND_ENUM_CONSTANT(PS2DE::INFO_COLLISION_PAIRS);
	BIND_ENUM_CONSTANT(PS2DE::INFO_ISLAND_COUNT);

#ifdef DEBUG_ENABLED
	ADD_SIGNAL(MethodInfo("_debug_changed"));
#endif
}

PhysicsSystem2D::PhysicsSystem2D() {
	singleton = this;

	// World2D physics space
	GLOBAL_DEF_BASIC(PropertyInfo(Variant::FLOAT, "physics/planar/default_gravity", PROPERTY_HINT_RANGE, U"-4096,4096,0.001,or_less,or_greater,suffix:px/s\u00B2"), 980.0);
	GLOBAL_DEF_BASIC(PropertyInfo(Variant::VECTOR2, "physics/planar/default_gravity_vector", PROPERTY_HINT_RANGE, "-10,10,0.001,or_less,or_greater"), Vector2(0, 1));
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/planar/default_linear_damp", PROPERTY_HINT_RANGE, "-1,100,0.001,or_greater"), 0.1);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/planar/default_angular_damp", PROPERTY_HINT_RANGE, "-1,100,0.001,or_greater"), 1.0);

	// PhysicsSystem2D
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/planar/sleep_threshold_linear", PROPERTY_HINT_RANGE, "0,10,0.001,or_greater,suffix:m/s"), 2.0);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/planar/sleep_threshold_angular", PROPERTY_HINT_RANGE, U"0,90,0.1,radians_as_degrees,suffix:°/s"), Math::deg_to_rad(8.0));
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/planar/time_before_sleep", PROPERTY_HINT_RANGE, "0,5,0.01,or_greater,suffix:s"), 0.5);
	GLOBAL_DEF(PropertyInfo(Variant::INT, "physics/planar/solver/solver_iterations", PROPERTY_HINT_RANGE, "1,32,1,or_greater"), 16);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/planar/solver/contact_recycle_radius", PROPERTY_HINT_RANGE, "0,10,0.01,or_greater,suffix:m"), 1.0);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/planar/solver/contact_max_separation", PROPERTY_HINT_RANGE, "0,10,0.01,or_greater,suffix:m"), 1.5);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/planar/solver/contact_max_allowed_penetration", PROPERTY_HINT_RANGE, "0.01,10,0.01,or_greater,suffix:m"), 0.3);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/planar/solver/default_contact_bias", PROPERTY_HINT_RANGE, "0,1,0.01"), 0.8);
	GLOBAL_DEF(PropertyInfo(Variant::FLOAT, "physics/planar/solver/default_constraint_bias", PROPERTY_HINT_RANGE, "0,1,0.01"), 0.2);
}

PhysicsSystem2D::~PhysicsSystem2D() {
	singleton = nullptr;
}
