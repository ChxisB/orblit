/**************************************************************************/
/*  physics_direct_body_state_spatial.cpp                                      */

#include "physics_direct_body_state_spatial.h"

#include "core/object/class_registry.h"

void PhysicsDirectBodyState3D::integrate_forces() {
	real_t step = get_step();
	Vector3 lv = get_linear_velocity();
	lv += get_total_gravity() * step;

	Vector3 av = get_angular_velocity();

	real_t linear_damp = 1.0 - step * get_total_linear_damp();

	if (linear_damp < 0) { // reached zero in the given time
		linear_damp = 0;
	}

	real_t angular_damp = 1.0 - step * get_total_angular_damp();

	if (angular_damp < 0) { // reached zero in the given time
		angular_damp = 0;
	}

	lv *= linear_damp;
	av *= angular_damp;

	set_linear_velocity(lv);
	set_angular_velocity(av);
}

Object *PhysicsDirectBodyState3D::get_contact_collider_object(int p_contact_idx) const {
	ObjectID objid = get_contact_collider_id(p_contact_idx);
	Object *obj = ObjectRegistry::get_instance(objid);
	return obj;
}

void PhysicsDirectBodyState3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_total_gravity"), &PhysicsDirectBodyState3D::get_total_gravity);
	ClassRegistry::bind_method(D_METHOD("get_total_linear_damp"), &PhysicsDirectBodyState3D::get_total_linear_damp);
	ClassRegistry::bind_method(D_METHOD("get_total_angular_damp"), &PhysicsDirectBodyState3D::get_total_angular_damp);

	ClassRegistry::bind_method(D_METHOD("get_center_of_mass"), &PhysicsDirectBodyState3D::get_center_of_mass);
	ClassRegistry::bind_method(D_METHOD("get_center_of_mass_local"), &PhysicsDirectBodyState3D::get_center_of_mass_local);
	ClassRegistry::bind_method(D_METHOD("get_principal_inertia_axes"), &PhysicsDirectBodyState3D::get_principal_inertia_axes);

	ClassRegistry::bind_method(D_METHOD("get_inverse_mass"), &PhysicsDirectBodyState3D::get_inverse_mass);
	ClassRegistry::bind_method(D_METHOD("get_inverse_inertia"), &PhysicsDirectBodyState3D::get_inverse_inertia);
	ClassRegistry::bind_method(D_METHOD("get_inverse_inertia_tensor"), &PhysicsDirectBodyState3D::get_inverse_inertia_tensor);

	ClassRegistry::bind_method(D_METHOD("set_linear_velocity", "velocity"), &PhysicsDirectBodyState3D::set_linear_velocity);
	ClassRegistry::bind_method(D_METHOD("get_linear_velocity"), &PhysicsDirectBodyState3D::get_linear_velocity);

	ClassRegistry::bind_method(D_METHOD("set_angular_velocity", "velocity"), &PhysicsDirectBodyState3D::set_angular_velocity);
	ClassRegistry::bind_method(D_METHOD("get_angular_velocity"), &PhysicsDirectBodyState3D::get_angular_velocity);

	ClassRegistry::bind_method(D_METHOD("set_transform", "transform"), &PhysicsDirectBodyState3D::set_transform);
	ClassRegistry::bind_method(D_METHOD("get_transform"), &PhysicsDirectBodyState3D::get_transform);

	ClassRegistry::bind_method(D_METHOD("get_velocity_at_local_position", "local_position"), &PhysicsDirectBodyState3D::get_velocity_at_local_position);

	ClassRegistry::bind_method(D_METHOD("apply_central_impulse", "impulse"), &PhysicsDirectBodyState3D::apply_central_impulse, Vector3());
	ClassRegistry::bind_method(D_METHOD("apply_impulse", "impulse", "position"), &PhysicsDirectBodyState3D::apply_impulse, Vector3());
	ClassRegistry::bind_method(D_METHOD("apply_torque_impulse", "impulse"), &PhysicsDirectBodyState3D::apply_torque_impulse);

	ClassRegistry::bind_method(D_METHOD("apply_central_force", "force"), &PhysicsDirectBodyState3D::apply_central_force, Vector3());
	ClassRegistry::bind_method(D_METHOD("apply_force", "force", "position"), &PhysicsDirectBodyState3D::apply_force, Vector3());
	ClassRegistry::bind_method(D_METHOD("apply_torque", "torque"), &PhysicsDirectBodyState3D::apply_torque);

	ClassRegistry::bind_method(D_METHOD("add_constant_central_force", "force"), &PhysicsDirectBodyState3D::add_constant_central_force, Vector3());
	ClassRegistry::bind_method(D_METHOD("add_constant_force", "force", "position"), &PhysicsDirectBodyState3D::add_constant_force, Vector3());
	ClassRegistry::bind_method(D_METHOD("add_constant_torque", "torque"), &PhysicsDirectBodyState3D::add_constant_torque);

	ClassRegistry::bind_method(D_METHOD("set_constant_force", "force"), &PhysicsDirectBodyState3D::set_constant_force);
	ClassRegistry::bind_method(D_METHOD("get_constant_force"), &PhysicsDirectBodyState3D::get_constant_force);

	ClassRegistry::bind_method(D_METHOD("set_constant_torque", "torque"), &PhysicsDirectBodyState3D::set_constant_torque);
	ClassRegistry::bind_method(D_METHOD("get_constant_torque"), &PhysicsDirectBodyState3D::get_constant_torque);

	ClassRegistry::bind_method(D_METHOD("set_sleep_state", "enabled"), &PhysicsDirectBodyState3D::set_sleep_state);
	ClassRegistry::bind_method(D_METHOD("is_sleeping"), &PhysicsDirectBodyState3D::is_sleeping);

	ClassRegistry::bind_method(D_METHOD("set_collision_layer", "layer"), &PhysicsDirectBodyState3D::set_collision_layer);
	ClassRegistry::bind_method(D_METHOD("get_collision_layer"), &PhysicsDirectBodyState3D::get_collision_layer);

	ClassRegistry::bind_method(D_METHOD("set_collision_mask", "mask"), &PhysicsDirectBodyState3D::set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("get_collision_mask"), &PhysicsDirectBodyState3D::get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("get_contact_count"), &PhysicsDirectBodyState3D::get_contact_count);

	ClassRegistry::bind_method(D_METHOD("get_contact_local_position", "contact_idx"), &PhysicsDirectBodyState3D::get_contact_local_position);
	ClassRegistry::bind_method(D_METHOD("get_contact_local_normal", "contact_idx"), &PhysicsDirectBodyState3D::get_contact_local_normal);
	ClassRegistry::bind_method(D_METHOD("get_contact_impulse", "contact_idx"), &PhysicsDirectBodyState3D::get_contact_impulse);
	ClassRegistry::bind_method(D_METHOD("get_contact_local_shape", "contact_idx"), &PhysicsDirectBodyState3D::get_contact_local_shape);
	ClassRegistry::bind_method(D_METHOD("get_contact_local_velocity_at_position", "contact_idx"), &PhysicsDirectBodyState3D::get_contact_local_velocity_at_position);
	ClassRegistry::bind_method(D_METHOD("get_contact_collider", "contact_idx"), &PhysicsDirectBodyState3D::get_contact_collider);
	ClassRegistry::bind_method(D_METHOD("get_contact_collider_position", "contact_idx"), &PhysicsDirectBodyState3D::get_contact_collider_position);
	ClassRegistry::bind_method(D_METHOD("get_contact_collider_id", "contact_idx"), &PhysicsDirectBodyState3D::get_contact_collider_id);
	ClassRegistry::bind_method(D_METHOD("get_contact_collider_object", "contact_idx"), &PhysicsDirectBodyState3D::get_contact_collider_object);
	ClassRegistry::bind_method(D_METHOD("get_contact_collider_shape", "contact_idx"), &PhysicsDirectBodyState3D::get_contact_collider_shape);
	ClassRegistry::bind_method(D_METHOD("get_contact_collider_velocity_at_position", "contact_idx"), &PhysicsDirectBodyState3D::get_contact_collider_velocity_at_position);
	ClassRegistry::bind_method(D_METHOD("get_step"), &PhysicsDirectBodyState3D::get_step);
	ClassRegistry::bind_method(D_METHOD("integrate_forces"), &PhysicsDirectBodyState3D::integrate_forces);
	ClassRegistry::bind_method(D_METHOD("get_space_state"), &PhysicsDirectBodyState3D::get_space_state);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "step"), "", "get_step");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "inverse_mass"), "", "get_inverse_mass");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "total_angular_damp"), "", "get_total_angular_damp");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "total_linear_damp"), "", "get_total_linear_damp");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "inverse_inertia"), "", "get_inverse_inertia");
	ADD_PROPERTY(PropertyInfo(Variant::BASIS, "inverse_inertia_tensor"), "", "get_inverse_inertia_tensor");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "total_gravity"), "", "get_total_gravity");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "center_of_mass"), "", "get_center_of_mass");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "center_of_mass_local"), "", "get_center_of_mass_local");
	ADD_PROPERTY(PropertyInfo(Variant::BASIS, "principal_inertia_axes"), "", "get_principal_inertia_axes");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "angular_velocity"), "set_angular_velocity", "get_angular_velocity");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR3, "linear_velocity"), "set_linear_velocity", "get_linear_velocity");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "sleeping"), "set_sleep_state", "is_sleeping");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collision_layer"), "set_collision_layer", "get_collision_layer");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collision_mask"), "set_collision_mask", "get_collision_mask");
	ADD_PROPERTY(PropertyInfo(Variant::TRANSFORM3D, "transform"), "set_transform", "get_transform");
}

PhysicsDirectBodyState3D::PhysicsDirectBodyState3D() {}
