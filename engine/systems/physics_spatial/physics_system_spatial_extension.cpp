/**************************************************************************/
/*  physics_system_spatial_extension.cpp                                       */

#include "physics_system_spatial_extension.h"

#include "core/object/class_registry.h"

bool PhysicsDirectSpaceState3DExtension::is_body_excluded_from_query(const RID &p_body) const {
	return exclude && exclude->has(p_body);
}

thread_local const HashSet<RID> *PhysicsDirectSpaceState3DExtension::exclude = nullptr;

void PhysicsDirectSpaceState3DExtension::register_members() {
	ClassRegistry::bind_method(D_METHOD("is_body_excluded_from_query", "body"), &PhysicsDirectSpaceState3DExtension::is_body_excluded_from_query);

	ORBLIT_VIRTUAL_BIND(_intersect_ray, "from", "to", "collision_mask", "collide_with_bodies", "collide_with_areas", "hit_from_inside", "hit_back_faces", "pick_ray", "r_result");
	ORBLIT_VIRTUAL_BIND(_intersect_point, "position", "collision_mask", "collide_with_bodies", "collide_with_areas", "r_results", "max_results");
	ORBLIT_VIRTUAL_BIND(_intersect_shape, "shape_rid", "transform", "motion", "margin", "collision_mask", "collide_with_bodies", "collide_with_areas", "r_result_count", "max_results");
	ORBLIT_VIRTUAL_BIND(_cast_motion, "shape_rid", "transform", "motion", "margin", "collision_mask", "collide_with_bodies", "collide_with_areas", "r_closest_safe", "r_closest_unsafe", "r_info");
	ORBLIT_VIRTUAL_BIND(_collide_shape, "shape_rid", "transform", "motion", "margin", "collision_mask", "collide_with_bodies", "collide_with_areas", "r_results", "max_results", "r_result_count");
	ORBLIT_VIRTUAL_BIND(_rest_info, "shape_rid", "transform", "motion", "margin", "collision_mask", "collide_with_bodies", "collide_with_areas", "r_rest_info");
	ORBLIT_VIRTUAL_BIND(_get_closest_point_to_object_volume, "object", "point");
}

PhysicsDirectSpaceState3DExtension::PhysicsDirectSpaceState3DExtension() {
}

void PhysicsDirectBodyState3DExtension::register_members() {
	ORBLIT_VIRTUAL_BIND(_get_total_gravity);
	ORBLIT_VIRTUAL_BIND(_get_total_linear_damp);
	ORBLIT_VIRTUAL_BIND(_get_total_angular_damp);

	ORBLIT_VIRTUAL_BIND(_get_center_of_mass);
	ORBLIT_VIRTUAL_BIND(_get_center_of_mass_local);
	ORBLIT_VIRTUAL_BIND(_get_principal_inertia_axes);

	ORBLIT_VIRTUAL_BIND(_get_inverse_mass);
	ORBLIT_VIRTUAL_BIND(_get_inverse_inertia);
	ORBLIT_VIRTUAL_BIND(_get_inverse_inertia_tensor);

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
	ORBLIT_VIRTUAL_BIND(_get_contact_impulse, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_local_shape, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_local_velocity_at_position, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_collider, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_collider_position, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_collider_id, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_collider_object, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_collider_shape, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_contact_collider_velocity_at_position, "contact_idx");
	ORBLIT_VIRTUAL_BIND(_get_step);
	ORBLIT_VIRTUAL_BIND(_integrate_forces);
	ORBLIT_VIRTUAL_BIND(_get_space_state);
}

PhysicsDirectBodyState3DExtension::PhysicsDirectBodyState3DExtension() {
}

thread_local const HashSet<RID> *PhysicsSystem3DExtension::exclude_bodies = nullptr;
thread_local const HashSet<ObjectID> *PhysicsSystem3DExtension::exclude_objects = nullptr;

bool PhysicsSystem3DExtension::body_test_motion_is_excluding_body(RID p_body) const {
	return exclude_bodies && exclude_bodies->has(p_body);
}

bool PhysicsSystem3DExtension::body_test_motion_is_excluding_object(ObjectID p_object) const {
	return exclude_objects && exclude_objects->has(p_object);
}

void PhysicsSystem3DExtension::register_members() {
	/* SHAPE API */

	ORBLIT_VIRTUAL_BIND(_world_boundary_shape_create);
	ORBLIT_VIRTUAL_BIND(_separation_ray_shape_create);
	ORBLIT_VIRTUAL_BIND(_sphere_shape_create);
	ORBLIT_VIRTUAL_BIND(_box_shape_create);
	ORBLIT_VIRTUAL_BIND(_capsule_shape_create);
	ORBLIT_VIRTUAL_BIND(_cylinder_shape_create);
	ORBLIT_VIRTUAL_BIND(_convex_polygon_shape_create);
	ORBLIT_VIRTUAL_BIND(_concave_polygon_shape_create);
	ORBLIT_VIRTUAL_BIND(_heightmap_shape_create);
	ORBLIT_VIRTUAL_BIND(_custom_shape_create);

	ORBLIT_VIRTUAL_BIND(_shape_set_data, "shape", "data");
	ORBLIT_VIRTUAL_BIND(_shape_set_custom_solver_bias, "shape", "bias");

	ORBLIT_VIRTUAL_BIND(_shape_set_margin, "shape", "margin");
	ORBLIT_VIRTUAL_BIND(_shape_get_margin, "shape");

	ORBLIT_VIRTUAL_BIND(_shape_get_type, "shape");
	ORBLIT_VIRTUAL_BIND(_shape_get_data, "shape");
	ORBLIT_VIRTUAL_BIND(_shape_get_custom_solver_bias, "shape");

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

	ORBLIT_VIRTUAL_BIND(_area_set_param, "area", "param", "value");
	ORBLIT_VIRTUAL_BIND(_area_set_transform, "area", "transform");

	ORBLIT_VIRTUAL_BIND(_area_get_param, "area", "param");
	ORBLIT_VIRTUAL_BIND(_area_get_transform, "area");

	ORBLIT_VIRTUAL_BIND(_area_set_collision_layer, "area", "layer");
	ORBLIT_VIRTUAL_BIND(_area_get_collision_layer, "area");

	ORBLIT_VIRTUAL_BIND(_area_set_collision_mask, "area", "mask");
	ORBLIT_VIRTUAL_BIND(_area_get_collision_mask, "area");

	ORBLIT_VIRTUAL_BIND(_area_set_monitorable, "area", "monitorable");
	ORBLIT_VIRTUAL_BIND(_area_set_ray_pickable, "area", "enable");

	ORBLIT_VIRTUAL_BIND(_area_set_monitor_callback, "area", "callback");
	ORBLIT_VIRTUAL_BIND(_area_set_area_monitor_callback, "area", "callback");

	/* BODY API */

	ClassRegistry::bind_method(D_METHOD("body_test_motion_is_excluding_body", "body"), &PhysicsSystem3DExtension::body_test_motion_is_excluding_body);
	ClassRegistry::bind_method(D_METHOD("body_test_motion_is_excluding_object", "object"), &PhysicsSystem3DExtension::body_test_motion_is_excluding_object);

	ORBLIT_VIRTUAL_BIND(_body_create);

	ORBLIT_VIRTUAL_BIND(_body_set_space, "body", "space");
	ORBLIT_VIRTUAL_BIND(_body_get_space, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_mode, "body", "mode");
	ORBLIT_VIRTUAL_BIND(_body_get_mode, "body");

	ORBLIT_VIRTUAL_BIND(_body_add_shape, "body", "shape", "transform", "disabled");
	ORBLIT_VIRTUAL_BIND(_body_set_shape, "body", "shape_idx", "shape");
	ORBLIT_VIRTUAL_BIND(_body_set_shape_transform, "body", "shape_idx", "transform");
	ORBLIT_VIRTUAL_BIND(_body_set_shape_disabled, "body", "shape_idx", "disabled");

	ORBLIT_VIRTUAL_BIND(_body_get_shape_count, "body");
	ORBLIT_VIRTUAL_BIND(_body_get_shape, "body", "shape_idx");
	ORBLIT_VIRTUAL_BIND(_body_get_shape_transform, "body", "shape_idx");

	ORBLIT_VIRTUAL_BIND(_body_remove_shape, "body", "shape_idx");
	ORBLIT_VIRTUAL_BIND(_body_clear_shapes, "body");

	ORBLIT_VIRTUAL_BIND(_body_attach_object_instance_id, "body", "id");
	ORBLIT_VIRTUAL_BIND(_body_get_object_instance_id, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_enable_continuous_collision_detection, "body", "enable");
	ORBLIT_VIRTUAL_BIND(_body_is_continuous_collision_detection_enabled, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_collision_layer, "body", "layer");
	ORBLIT_VIRTUAL_BIND(_body_get_collision_layer, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_collision_mask, "body", "mask");
	ORBLIT_VIRTUAL_BIND(_body_get_collision_mask, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_collision_priority, "body", "priority");
	ORBLIT_VIRTUAL_BIND(_body_get_collision_priority, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_user_flags, "body", "flags");
	ORBLIT_VIRTUAL_BIND(_body_get_user_flags, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_param, "body", "param", "value");
	ORBLIT_VIRTUAL_BIND(_body_get_param, "body", "param");

	ORBLIT_VIRTUAL_BIND(_body_reset_mass_properties, "body");

	ORBLIT_VIRTUAL_BIND(_body_set_state, "body", "state", "value");
	ORBLIT_VIRTUAL_BIND(_body_get_state, "body", "state");

	ORBLIT_VIRTUAL_BIND(_body_apply_central_impulse, "body", "impulse");
	ORBLIT_VIRTUAL_BIND(_body_apply_impulse, "body", "impulse", "position");
	ORBLIT_VIRTUAL_BIND(_body_apply_torque_impulse, "body", "impulse");

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

	ORBLIT_VIRTUAL_BIND(_body_set_axis_lock, "body", "axis", "lock");
	ORBLIT_VIRTUAL_BIND(_body_is_axis_locked, "body", "axis");

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

	ORBLIT_VIRTUAL_BIND(_body_set_ray_pickable, "body", "enable");

	ORBLIT_VIRTUAL_BIND(_body_test_motion, "body", "from", "motion", "margin", "max_collisions", "collide_separation_ray", "recovery_as_collision", "r_result");

	ORBLIT_VIRTUAL_BIND(_body_get_direct_state, "body");

	/* SOFT BODY API */

	ORBLIT_VIRTUAL_BIND(_soft_body_create);

	ORBLIT_VIRTUAL_BIND(_soft_body_update_rendering_system, "body", "rendering_system_handler");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_space, "body", "space");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_space, "body");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_ray_pickable, "body", "enable");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_collision_layer, "body", "layer");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_collision_layer, "body");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_collision_mask, "body", "mask");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_collision_mask, "body");

	ORBLIT_VIRTUAL_BIND(_soft_body_add_collision_exception, "body", "body_b");
	ORBLIT_VIRTUAL_BIND(_soft_body_remove_collision_exception, "body", "body_b");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_collision_exceptions, "body");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_state, "body", "state", "variant");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_state, "body", "state");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_transform, "body", "transform");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_simulation_precision, "body", "simulation_precision");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_simulation_precision, "body");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_total_mass, "body", "total_mass");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_total_mass, "body");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_linear_stiffness, "body", "linear_stiffness");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_linear_stiffness, "body");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_shrinking_factor, "body", "shrinking_factor");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_shrinking_factor, "body");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_pressure_coefficient, "body", "pressure_coefficient");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_pressure_coefficient, "body");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_damping_coefficient, "body", "damping_coefficient");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_damping_coefficient, "body");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_drag_coefficient, "body", "drag_coefficient");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_drag_coefficient, "body");

	ORBLIT_VIRTUAL_BIND(_soft_body_set_mesh, "body", "mesh");

	ORBLIT_VIRTUAL_BIND(_soft_body_get_bounds, "body");

	ORBLIT_VIRTUAL_BIND(_soft_body_move_point, "body", "point_index", "global_position");
	ORBLIT_VIRTUAL_BIND(_soft_body_get_point_global_position, "body", "point_index");

	ORBLIT_VIRTUAL_BIND(_soft_body_remove_all_pinned_points, "body");
	ORBLIT_VIRTUAL_BIND(_soft_body_pin_point, "body", "point_index", "pin");
	ORBLIT_VIRTUAL_BIND(_soft_body_is_point_pinned, "body", "point_index");

	ORBLIT_VIRTUAL_BIND(_soft_body_apply_point_impulse, "body", "point_index", "impulse");
	ORBLIT_VIRTUAL_BIND(_soft_body_apply_point_force, "body", "point_index", "force");
	ORBLIT_VIRTUAL_BIND(_soft_body_apply_central_impulse, "body", "impulse");
	ORBLIT_VIRTUAL_BIND(_soft_body_apply_central_force, "body", "force");

	/* JOINT API */

	ORBLIT_VIRTUAL_BIND(_joint_create);
	ORBLIT_VIRTUAL_BIND(_joint_clear, "joint");

	ORBLIT_VIRTUAL_BIND(_joint_make_pin, "joint", "body_A", "local_A", "body_B", "local_B");

	ORBLIT_VIRTUAL_BIND(_pin_joint_set_param, "joint", "param", "value");
	ORBLIT_VIRTUAL_BIND(_pin_joint_get_param, "joint", "param");

	ORBLIT_VIRTUAL_BIND(_pin_joint_set_local_a, "joint", "local_A");
	ORBLIT_VIRTUAL_BIND(_pin_joint_get_local_a, "joint");

	ORBLIT_VIRTUAL_BIND(_pin_joint_set_local_b, "joint", "local_B");
	ORBLIT_VIRTUAL_BIND(_pin_joint_get_local_b, "joint");

	ORBLIT_VIRTUAL_BIND(_joint_make_hinge, "joint", "body_A", "hinge_A", "body_B", "hinge_B");
	ORBLIT_VIRTUAL_BIND(_joint_make_hinge_simple, "joint", "body_A", "pivot_A", "axis_A", "body_B", "pivot_B", "axis_B");

	ORBLIT_VIRTUAL_BIND(_hinge_joint_set_param, "joint", "param", "value");
	ORBLIT_VIRTUAL_BIND(_hinge_joint_get_param, "joint", "param");

	ORBLIT_VIRTUAL_BIND(_hinge_joint_set_flag, "joint", "flag", "enabled");
	ORBLIT_VIRTUAL_BIND(_hinge_joint_get_flag, "joint", "flag");

	ORBLIT_VIRTUAL_BIND(_joint_make_slider, "joint", "body_A", "local_ref_A", "body_B", "local_ref_B");

	ORBLIT_VIRTUAL_BIND(_slider_joint_set_param, "joint", "param", "value");
	ORBLIT_VIRTUAL_BIND(_slider_joint_get_param, "joint", "param");

	ORBLIT_VIRTUAL_BIND(_joint_make_cone_twist, "joint", "body_A", "local_ref_A", "body_B", "local_ref_B");

	ORBLIT_VIRTUAL_BIND(_cone_twist_joint_set_param, "joint", "param", "value");
	ORBLIT_VIRTUAL_BIND(_cone_twist_joint_get_param, "joint", "param");

	ORBLIT_VIRTUAL_BIND(_joint_make_generic_6dof, "joint", "body_A", "local_ref_A", "body_B", "local_ref_B");

	ORBLIT_VIRTUAL_BIND(_generic_6dof_joint_set_param, "joint", "axis", "param", "value");
	ORBLIT_VIRTUAL_BIND(_generic_6dof_joint_get_param, "joint", "axis", "param");

	ORBLIT_VIRTUAL_BIND(_generic_6dof_joint_set_flag, "joint", "axis", "flag", "enable");
	ORBLIT_VIRTUAL_BIND(_generic_6dof_joint_get_flag, "joint", "axis", "flag");

	ORBLIT_VIRTUAL_BIND(_generic_6dof_joint_set_angular_target_rotation, "joint", "target_rotation");
	ORBLIT_VIRTUAL_BIND(_generic_6dof_joint_get_angular_target_rotation, "joint");

	ORBLIT_VIRTUAL_BIND(_joint_get_type, "joint");

	ORBLIT_VIRTUAL_BIND(_joint_set_solver_priority, "joint", "priority");
	ORBLIT_VIRTUAL_BIND(_joint_get_solver_priority, "joint");

	ORBLIT_VIRTUAL_BIND(_joint_disable_collisions_between_bodies, "joint", "disable");
	ORBLIT_VIRTUAL_BIND(_joint_is_disabled_collisions_between_bodies, "joint");

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

PhysicsSystem3DExtension::PhysicsSystem3DExtension() {
}

PhysicsSystem3DExtension::~PhysicsSystem3DExtension() {
}
