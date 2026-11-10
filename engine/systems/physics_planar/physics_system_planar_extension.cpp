/**************************************************************************/
/*  physics_system_planar_extension.cpp                                       */

#include "physics_system_planar_extension.h"

#include "core/object/class_registry.h"

bool PhysicsDirectSpaceState2DExtension::is_body_excluded_from_query(const RID &p_body) const {
	return exclude && exclude->has(p_body);
}

thread_local const HashSet<RID> *PhysicsDirectSpaceState2DExtension::exclude = nullptr;

void PhysicsDirectSpaceState2DExtension::register_members() {
	ClassRegistry::bind_method(D_METHOD("is_body_excluded_from_query", "body"), &PhysicsDirectSpaceState2DExtension::is_body_excluded_from_query);

	ORBLIT_VIRTUAL_BIND(_intersect_ray, "from", "to", "collision_mask", "collide_with_bodies", "collide_with_areas", "hit_from_inside", "r_result");
	ORBLIT_VIRTUAL_BIND(_intersect_point, "position", "canvas_instance_id", "collision_mask", "collide_with_bodies", "collide_with_areas", "r_results", "max_results");
	ORBLIT_VIRTUAL_BIND(_intersect_shape, "shape_rid", "transform", "motion", "margin", "collision_mask", "collide_with_bodies", "collide_with_areas", "r_result", "max_results");
	ORBLIT_VIRTUAL_BIND(_cast_motion, "shape_rid", "transform", "motion", "margin", "collision_mask", "collide_with_bodies", "collide_with_areas", "r_closest_safe", "r_closest_unsafe");
	ORBLIT_VIRTUAL_BIND(_collide_shape, "shape_rid", "transform", "motion", "margin", "collision_mask", "collide_with_bodies", "collide_with_areas", "r_results", "max_results", "r_result_count");
	ORBLIT_VIRTUAL_BIND(_rest_info, "shape_rid", "transform", "motion", "margin", "collision_mask", "collide_with_bodies", "collide_with_areas", "r_rest_info");
}

PhysicsDirectSpaceState2DExtension::PhysicsDirectSpaceState2DExtension() {
}

void PhysicsDirectBodyState2DExtension::register_members() {
	ORBLIT_VIRTUAL_BIND(_get_total_gravity);
	ORBLIT_VIRTUAL_BIND(_get_total_linear_damp);
	ORBLIT_VIRTUAL_BIND(_get_total_angular_damp);

	ORBLIT_VIRTUAL_BIND(_get_center_of_mass);
	ORBLIT_VIRTUAL_BIND(_get_center_of_mass_local);
	ORBLIT_VIRTUAL_BIND(_get_inverse_mass);
	ORBLIT_VIRTUAL_BIND(_get_inverse_inertia);

	ORBLIT_VIRTUAL_BIND(_set_linear_velocity, "velocity");
	ORBLIT_VIRTUAL_BIND(_get_linear_velocity);

	ORBLIT_VIRTUAL_BIND(_set_angular_velocity, "velocity");
	ORBLIT_VIRTUAL_BIND(_get_angular_velocity);

	ORBLIT_VIRTUAL_BIND(_set_transform, "transform");
	ORBLIT_VIRTUAL_BIND(_get_transform);

	ORBLIT_VIRTUAL_BIND(_get_velocity_at_local_position, "local_position");

	ORBLIT_VIRTUAL_BIND(_apply_central_impulse, "impulse");
	ORBLIT_VIRTUAL_BIND(_apply_impulse, "impulse", "position");
	ORBLIT_VIRTUAL_BIND(_apply_torque_impulse, "impulse");

	ORBLIT_VIRTUAL_BIND(_apply_central_force, "force");
	ORBLIT_VIRTUAL_BIND(_apply_force, "force", "position");
	ORBLIT_VIRTUAL_BIND(_apply_torque, "torque");

	ORBLIT_VIRTUAL_BIND(_add_constant_central_force, "force");
	ORBLIT_VIRTUAL_BIND(_add_constant_force, "force", "position");
	ORBLIT_VIRTUAL_BIND(_add_constant_torque, "torque");

	ORBLIT_VIRTUAL_BIND(_set_constant_force, "force");
	ORBLIT_VIRTUAL_BIND(_get_constant_force);

	ORBLIT_VIRTUAL_BIND(_set_constant_torque, "torque");
	ORBLIT_VIRTUAL_BIND(_get_constant_torque);

	ORBLIT_VIRTUAL_BIND(_set_sleep_state, "enabled");
	ORBLIT_VIRTUAL_BIND(_is_sleeping);

	ORBLIT_VIRTUAL_BIND(_set_collision_layer, "layer");
	ORBLIT_VIRTUAL_BIND(_get_collision_layer);

	ORBLIT_VIRTUAL_BIND(_set_collision_mask, "mask");
	ORBLIT_VIRTUAL_BIND(_get_collision_mask);

	ORBLIT_VIRTUAL_BIND(_get_contact_count);

	ORBLIT_VIRTUAL_BIND(_get_contact_local_position, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_local_normal, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_local_shape, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_local_velocity_at_position, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_collider, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_collider_position, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_collider_id, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_collider_object, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_collider_shape, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_collider_velocity_at_position, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_impulse, "contact_idx");

	ORBLIT_VIRTUAL_BIND(_get_step);
	ORBLIT_VIRTUAL_BIND(_integrate_forces);

	ORBLIT_VIRTUAL_BIND(_get_space_state);
}

PhysicsDirectBodyState2DExtension::PhysicsDirectBodyState2DExtension() {
}

thread_local const HashSet<RID> *PhysicsSystem2DExtension::exclude_bodies = nullptr;
thread_local const HashSet<ObjectID> *PhysicsSystem2DExtension::exclude_objects = nullptr;

bool PhysicsSystem2DExtension::body_test_motion_is_excluding_body(RID p_body) const {
	return exclude_bodies && exclude_bodies->has(p_body);
}

bool PhysicsSystem2DExtension::body_test_motion_is_excluding_object(ObjectID p_object) const {
	return exclude_objects && exclude_objects->has(p_object);
}

void PhysicsSystem2DExtension::register_members() {
	/* SHAPE API */

	ORBLIT_VIRTUAL_BIND(_world_boundary_shape_create);
	ORBLIT_VIRTUAL_BIND(_separation_ray_shape_create);
	ORBLIT_VIRTUAL_BIND(_segment_shape_create);
	ORBLIT_VIRTUAL_BIND(_circle_shape_create);
	ORBLIT_VIRTUAL_BIND(_rectangle_shape_create);
	ORBLIT_VIRTUAL_BIND(_capsule_shape_create);
	ORBLIT_VIRTUAL_BIND(_convex_polygon_shape_create);
	ORBLIT_VIRTUAL_BIND(_concave_polygon_shape_create);

	ORBLIT_VIRTUAL_BIND(_shape_set_data, "shape", "data");
	ORBLIT_VIRTUAL_BIND(_shape_set_custom_solver_bias, "shape", "bias");

	ORBLIT_VIRTUAL_BIND(_shape_get_type, "shape");
	ORBLIT_VIRTUAL_BIND(_shape_get_data, "shape");
	ORBLIT_VIRTUAL_BIND(_shape_get_custom_solver_bias, "shape");
	ORBLIT_VIRTUAL_BIND(_shape_collide, "shape_A", "xform_A", "motion_A", "shape_B", "xform_B", "motion_B", "r_results", "result_max", "r_result_count");

	/* SPACE API */

	ORBLIT_VIRTUAL_BIND(_space_create);
	ORBLIT_VIRTUAL_BIND(_space_set_active, "space", "active");
	ORBLIT_VIRTUAL_BIND(_space_is_active, "space");

	ORBLIT_VIRTUAL_BIND(_space_set_param, "space", "param", "value");
	ORBLIT_VIRTUAL_BIND(_space_get_param, "space", "param");

	ORBLIT_VIRTUAL_BIND(_space_get_direct_state, "space");

	ORBLIT_VIRTUAL_BIND(_space_set_debug_contacts, "space", "max_contacts");
	ORBLIT_VIRTUAL_BIND(_space_get_contacts, "space");
	ORBLIT_VIRTUAL_BIND(_space_get_contact_count, "space");

	/* AREA API */

	ORBLIT_VIRTUAL_BIND(_area_create);

	ORBLIT_VIRTUAL_BIND(_area_set_space, "area", "space");
	ORBLIT_VIRTUAL_BIND(_area_get_space, "area");

	ORBLIT_VIRTUAL_BIND(_area_add_shape, "area", "shape", "transform", "disabled");
	ORBLIT_VIRTUAL_BIND(_area_set_shape, "area", "shape_idx", "shape");
	ORBLIT_VIRTUAL_BIND(_area_set_shape_transform, "area", "shape_idx", "transform");
	ORBLIT_VIRTUAL_BIND(_area_set_shape_disabled, "area", "shape_idx", "disabled");

	ORBLIT_VIRTUAL_BIND(_area_get_shape_count, "area");
	ORBLIT_VIRTUAL_BIND(_area_get_shape, "area", "shape_idx");
	ORBLIT_VIRTUAL_BIND(_area_get_shape_transform, "area", "shape_idx");

	ORBLIT_VIRTUAL_BIND(_area_remove_shape, "area", "shape_idx");
	ORBLIT_VIRTUAL_BIND(_area_clear_shapes, "area");

	ORBLIT_VIRTUAL_BIND(_area_attach_object_instance_id, "area", "id");
	ORBLIT_VIRTUAL_BIND(_area_get_object_instance_id, "area");

	ORBLIT_VIRTUAL_BIND(_area_attach_canvas_instance_id, "area", "id");
	ORBLIT_VIRTUAL_BIND(_area_get_canvas_instance_id, "area");

	ORBLIT_VIRTUAL_BIND(_area_set_param, "area", "param", "value");
	ORBLIT_VIRTUAL_BIND(_area_set_transform, "area", "transform");

	ORBLIT_VIRTUAL_BIND(_area_get_param, "area", "param");
	ORBLIT_VIRTUAL_BIND(_area_get_transform, "area");

	ORBLIT_VIRTUAL_BIND(_area_set_collision_layer, "area", "layer");
	ORBLIT_VIRTUAL_BIND(_area_get_collision_layer, "area");

	ORBLIT_VIRTUAL_BIND(_area_set_collision_mask, "area", "mask");
	ORBLIT_VIRTUAL_BIND(_area_get_collision_mask, "area");

	ORBLIT_VIRTUAL_BIND(_area_set_monitorable, "area", "monitorable");
	ORBLIT_VIRTUAL_BIND(_area_set_pickable, "area", "pickable");

	ORBLIT_VIRTUAL_BIND(_area_set_monitor_callback, "area", "callback");
	ORBLIT_VIRTUAL_BIND(_area_set_area_monitor_callback, "area", "callback");

	/* BODY API */

	ClassRegistry::bind_method(D_METHOD("body_test_motion_is_excluding_body", "body"), &PhysicsSystem2DExtension::body_test_motion_is_excluding_body);
	ClassRegistry::bind_method(D_METHOD("body_test_motion_is_excluding_object", "object"), &PhysicsSystem2DExtension::body_test_motion_is_excluding_object);

	ORBLIT_VIRTUAL_BIND(_body_create);

	ORBLIT_VIRTUAL_BIND(_body_set_space, "body", "space");
	ORBLIT_VIRTUAL_BIND(_body_get_space, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_mode, "body", "mode");
	ORBLIT_VIRTUAL_BIND(_body_get_mode, "body");

	ORBLIT_VIRTUAL_BIND(_body_add_shape, "body", "shape", "transform", "disabled");
	ORBLIT_VIRTUAL_BIND(_body_set_shape, "body", "shape_idx", "shape");
	ORBLIT_VIRTUAL_BIND(_body_set_shape_transform, "body", "shape_idx", "transform");

	ORBLIT_VIRTUAL_BIND(_body_get_shape_count, "body");
	ORBLIT_VIRTUAL_BIND(_body_get_shape, "body", "shape_idx");
	ORBLIT_VIRTUAL_BIND(_body_get_shape_transform, "body", "shape_idx");

	ORBLIT_VIRTUAL_BIND(_body_set_shape_disabled, "body", "shape_idx", "disabled");
	ORBLIT_VIRTUAL_BIND(_body_set_shape_as_one_way_collision, "body", "shape_idx", "enable", "margin", "direction");

	ORBLIT_VIRTUAL_BIND(_body_remove_shape, "body", "shape_idx");
	ORBLIT_VIRTUAL_BIND(_body_clear_shapes, "body");

	ORBLIT_VIRTUAL_BIND(_body_attach_object_instance_id, "body", "id");
	ORBLIT_VIRTUAL_BIND(_body_get_object_instance_id, "body");

	ORBLIT_VIRTUAL_BIND(_body_attach_canvas_instance_id, "body", "id");
	ORBLIT_VIRTUAL_BIND(_body_get_canvas_instance_id, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_continuous_collision_detection_mode, "body", "mode");
	ORBLIT_VIRTUAL_BIND(_body_get_continuous_collision_detection_mode, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_collision_layer, "body", "layer");
	ORBLIT_VIRTUAL_BIND(_body_get_collision_layer, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_collision_mask, "body", "mask");
	ORBLIT_VIRTUAL_BIND(_body_get_collision_mask, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_collision_priority, "body", "priority");
	ORBLIT_VIRTUAL_BIND(_body_get_collision_priority, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_param, "body", "param", "value");
	ORBLIT_VIRTUAL_BIND(_body_get_param, "body", "param");

	ORBLIT_VIRTUAL_BIND(_body_reset_mass_properties, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_state, "body", "state", "value");
	ORBLIT_VIRTUAL_BIND(_body_get_state, "body", "state");

	ORBLIT_VIRTUAL_BIND(_body_apply_central_impulse, "body", "impulse");
	ORBLIT_VIRTUAL_BIND(_body_apply_torque_impulse, "body", "impulse");
	ORBLIT_VIRTUAL_BIND(_body_apply_impulse, "body", "impulse", "position");

	ORBLIT_VIRTUAL_BIND(_body_apply_central_force, "body", "force");
	ORBLIT_VIRTUAL_BIND(_body_apply_force, "body", "force", "position");
	ORBLIT_VIRTUAL_BIND(_body_apply_torque, "body", "torque");

	ORBLIT_VIRTUAL_BIND(_body_add_constant_central_force, "body", "force");
	ORBLIT_VIRTUAL_BIND(_body_add_constant_force, "body", "force", "position");
	ORBLIT_VIRTUAL_BIND(_body_add_constant_torque, "body", "torque");

	ORBLIT_VIRTUAL_BIND(_body_set_constant_force, "body", "force");
	ORBLIT_VIRTUAL_BIND(_body_get_constant_force, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_constant_torque, "body", "torque");
	ORBLIT_VIRTUAL_BIND(_body_get_constant_torque, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_axis_velocity, "body", "axis_velocity");

	ORBLIT_VIRTUAL_BIND(_body_add_collision_exception, "body", "excepted_body");
	ORBLIT_VIRTUAL_BIND(_body_remove_collision_exception, "body", "excepted_body");
	ORBLIT_VIRTUAL_BIND(_body_get_collision_exceptions, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_max_contacts_reported, "body", "amount");
	ORBLIT_VIRTUAL_BIND(_body_get_max_contacts_reported, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_contacts_reported_depth_threshold, "body", "threshold");
	ORBLIT_VIRTUAL_BIND(_body_get_contacts_reported_depth_threshold, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_omit_force_integration, "body", "enable");
	ORBLIT_VIRTUAL_BIND(_body_is_omitting_force_integration, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_state_sync_callback, "body", "callable");
	ORBLIT_VIRTUAL_BIND(_body_set_force_integration_callback, "body", "callable", "userdata");

	ORBLIT_VIRTUAL_BIND(_body_collide_shape, "body", "body_shape", "shape", "shape_xform", "motion", "r_results", "result_max", "r_result_count");

	ORBLIT_VIRTUAL_BIND(_body_set_pickable, "body", "pickable");

	ORBLIT_VIRTUAL_BIND(_body_get_direct_state, "body");

	ORBLIT_VIRTUAL_BIND(_body_test_motion, "body", "from", "motion", "margin", "collide_separation_ray", "recovery_as_collision", "r_result");

	/* JOINT API */

	ORBLIT_VIRTUAL_BIND(_joint_create);
	ORBLIT_VIRTUAL_BIND(_joint_clear, "joint");

	ORBLIT_VIRTUAL_BIND(_joint_set_param, "joint", "param", "value");
	ORBLIT_VIRTUAL_BIND(_joint_get_param, "joint", "param");

	ORBLIT_VIRTUAL_BIND(_joint_disable_collisions_between_bodies, "joint", "disable");
	ORBLIT_VIRTUAL_BIND(_joint_is_disabled_collisions_between_bodies, "joint");

	ORBLIT_VIRTUAL_BIND(_joint_make_pin, "joint", "anchor", "body_a", "body_b");
	ORBLIT_VIRTUAL_BIND(_joint_make_groove, "joint", "a_groove1", "a_groove2", "b_anchor", "body_a", "body_b");
	ORBLIT_VIRTUAL_BIND(_joint_make_damped_spring, "joint", "anchor_a", "anchor_b", "body_a", "body_b");

	ORBLIT_VIRTUAL_BIND(_pin_joint_set_flag, "joint", "flag", "enabled");
	ORBLIT_VIRTUAL_BIND(_pin_joint_get_flag, "joint", "flag");

	ORBLIT_VIRTUAL_BIND(_pin_joint_set_param, "joint", "param", "value");
	ORBLIT_VIRTUAL_BIND(_pin_joint_get_param, "joint", "param");

	ORBLIT_VIRTUAL_BIND(_damped_spring_joint_set_param, "joint", "param", "value");
	ORBLIT_VIRTUAL_BIND(_damped_spring_joint_get_param, "joint", "param");

	ORBLIT_VIRTUAL_BIND(_joint_get_type, "joint");

	/* MISC */

	ORBLIT_VIRTUAL_BIND(_free_rid, "rid");

	ORBLIT_VIRTUAL_BIND(_set_active, "active");

	ORBLIT_VIRTUAL_BIND(_init);
	ORBLIT_VIRTUAL_BIND(_step, "step");
	ORBLIT_VIRTUAL_BIND(_sync);
	ORBLIT_VIRTUAL_BIND(_flush_queries);
	ORBLIT_VIRTUAL_BIND(_end_sync);
	ORBLIT_VIRTUAL_BIND(_finish);

	ORBLIT_VIRTUAL_BIND(_is_flushing_queries);
	ORBLIT_VIRTUAL_BIND(_get_process_info, "process_info");
}

PhysicsSystem2DExtension::PhysicsSystem2DExtension() {
}

PhysicsSystem2DExtension::~PhysicsSystem2DExtension() {
}
