/**************************************************************************/
/*  physics_direct_body_state_planar.cpp                                      */

#include "physics_direct_body_state_planar.h"

#include "core/object/class_registry.h"

void PhysicsDirectBodyState2D::integrate_forces() {
	real_t step = get_step();
	Vector2 lv = get_linear_velocity();
	lv += get_total_gravity() * step;

	real_t av = get_angular_velocity();

	real_t damp = 1.0 - step * get_total_linear_damp();

	if (damp < 0) { // reached zero in the given time
		damp = 0;
	}

	lv *= damp;

	damp = 1.0 - step * get_total_angular_damp();

	if (damp < 0) { // reached zero in the given time
		damp = 0;
	}

	av *= damp;

	set_linear_velocity(lv);
	set_angular_velocity(av);
}

Object *PhysicsDirectBodyState2D::get_contact_collider_object(int p_contact_idx) const {
	ObjectID objid = get_contact_collider_id(p_contact_idx);
	Object *obj = ObjectRegistry::get_instance(objid);
	return obj;
}

void PhysicsDirectBodyState2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_total_gravity"), &PhysicsDirectBodyState2D::get_total_gravity);
	ClassRegistry::bind_method(D_METHOD("get_total_linear_damp"), &PhysicsDirectBodyState2D::get_total_linear_damp);
	ClassRegistry::bind_method(D_METHOD("get_total_angular_damp"), &PhysicsDirectBodyState2D::get_total_angular_damp);

	ClassRegistry::bind_method(D_METHOD("get_center_of_mass"), &PhysicsDirectBodyState2D::get_center_of_mass);
	ClassRegistry::bind_method(D_METHOD("get_center_of_mass_local"), &PhysicsDirectBodyState2D::get_center_of_mass_local);
	ClassRegistry::bind_method(D_METHOD("get_inverse_mass"), &PhysicsDirectBodyState2D::get_inverse_mass);
	ClassRegistry::bind_method(D_METHOD("get_inverse_inertia"), &PhysicsDirectBodyState2D::get_inverse_inertia);

	ClassRegistry::bind_method(D_METHOD("set_linear_velocity", "velocity"), &PhysicsDirectBodyState2D::set_linear_velocity);
	ClassRegistry::bind_method(D_METHOD("get_linear_velocity"), &PhysicsDirectBodyState2D::get_linear_velocity);

	ClassRegistry::bind_method(D_METHOD("set_angular_velocity", "velocity"), &PhysicsDirectBodyState2D::set_angular_velocity);
	ClassRegistry::bind_method(D_METHOD("get_angular_velocity"), &PhysicsDirectBodyState2D::get_angular_velocity);

	ClassRegistry::bind_method(D_METHOD("set_transform", "transform"), &PhysicsDirectBodyState2D::set_transform);
	ClassRegistry::bind_method(D_METHOD("get_transform"), &PhysicsDirectBodyState2D::get_transform);

	ClassRegistry::bind_method(D_METHOD("get_velocity_at_local_position", "local_position"), &PhysicsDirectBodyState2D::get_velocity_at_local_position);

	ClassRegistry::bind_method(D_METHOD("apply_central_impulse", "impulse"), &PhysicsDirectBodyState2D::apply_central_impulse);
	ClassRegistry::bind_method(D_METHOD("apply_torque_impulse", "impulse"), &PhysicsDirectBodyState2D::apply_torque_impulse);
	ClassRegistry::bind_method(D_METHOD("apply_impulse", "impulse", "position"), &PhysicsDirectBodyState2D::apply_impulse, Vector2());

	ClassRegistry::bind_method(D_METHOD("apply_central_force", "force"), &PhysicsDirectBodyState2D::apply_central_force, Vector2());
	ClassRegistry::bind_method(D_METHOD("apply_force", "force", "position"), &PhysicsDirectBodyState2D::apply_force, Vector2());
	ClassRegistry::bind_method(D_METHOD("apply_torque", "torque"), &PhysicsDirectBodyState2D::apply_torque);

	ClassRegistry::bind_method(D_METHOD("add_constant_central_force", "force"), &PhysicsDirectBodyState2D::add_constant_central_force, Vector2());
	ClassRegistry::bind_method(D_METHOD("add_constant_force", "force", "position"), &PhysicsDirectBodyState2D::add_constant_force, Vector2());
	ClassRegistry::bind_method(D_METHOD("add_constant_torque", "torque"), &PhysicsDirectBodyState2D::add_constant_torque);

	ClassRegistry::bind_method(D_METHOD("set_constant_force", "force"), &PhysicsDirectBodyState2D::set_constant_force);
	ClassRegistry::bind_method(D_METHOD("get_constant_force"), &PhysicsDirectBodyState2D::get_constant_force);

	ClassRegistry::bind_method(D_METHOD("set_constant_torque", "torque"), &PhysicsDirectBodyState2D::set_constant_torque);
	ClassRegistry::bind_method(D_METHOD("get_constant_torque"), &PhysicsDirectBodyState2D::get_constant_torque);

	ClassRegistry::bind_method(D_METHOD("set_sleep_state", "enabled"), &PhysicsDirectBodyState2D::set_sleep_state);
	ClassRegistry::bind_method(D_METHOD("is_sleeping"), &PhysicsDirectBodyState2D::is_sleeping);

	ClassRegistry::bind_method(D_METHOD("set_collision_layer", "layer"), &PhysicsDirectBodyState2D::set_collision_layer);
	ClassRegistry::bind_method(D_METHOD("get_collision_layer"), &PhysicsDirectBodyState2D::get_collision_layer);

	ClassRegistry::bind_method(D_METHOD("set_collision_mask", "mask"), &PhysicsDirectBodyState2D::set_collision_mask);
	ClassRegistry::bind_method(D_METHOD("get_collision_mask"), &PhysicsDirectBodyState2D::get_collision_mask);

	ClassRegistry::bind_method(D_METHOD("get_contact_count"), &PhysicsDirectBodyState2D::get_contact_count);

	ClassRegistry::bind_method(D_METHOD("get_contact_local_position", "contact_idx"), &PhysicsDirectBodyState2D::get_contact_local_position);
	ClassRegistry::bind_method(D_METHOD("get_contact_local_normal", "contact_idx"), &PhysicsDirectBodyState2D::get_contact_local_normal);
	ClassRegistry::bind_method(D_METHOD("get_contact_local_shape", "contact_idx"), &PhysicsDirectBodyState2D::get_contact_local_shape);
	ClassRegistry::bind_method(D_METHOD("get_contact_local_velocity_at_position", "contact_idx"), &PhysicsDirectBodyState2D::get_contact_local_velocity_at_position);
	ClassRegistry::bind_method(D_METHOD("get_contact_collider", "contact_idx"), &PhysicsDirectBodyState2D::get_contact_collider);
	ClassRegistry::bind_method(D_METHOD("get_contact_collider_position", "contact_idx"), &PhysicsDirectBodyState2D::get_contact_collider_position);
	ClassRegistry::bind_method(D_METHOD("get_contact_collider_id", "contact_idx"), &PhysicsDirectBodyState2D::get_contact_collider_id);
	ClassRegistry::bind_method(D_METHOD("get_contact_collider_object", "contact_idx"), &PhysicsDirectBodyState2D::get_contact_collider_object);
	ClassRegistry::bind_method(D_METHOD("get_contact_collider_shape", "contact_idx"), &PhysicsDirectBodyState2D::get_contact_collider_shape);
	ClassRegistry::bind_method(D_METHOD("get_contact_collider_velocity_at_position", "contact_idx"), &PhysicsDirectBodyState2D::get_contact_collider_velocity_at_position);
	ClassRegistry::bind_method(D_METHOD("get_contact_impulse", "contact_idx"), &PhysicsDirectBodyState2D::get_contact_impulse);
	ClassRegistry::bind_method(D_METHOD("get_step"), &PhysicsDirectBodyState2D::get_step);
	ClassRegistry::bind_method(D_METHOD("integrate_forces"), &PhysicsDirectBodyState2D::integrate_forces);
	ClassRegistry::bind_method(D_METHOD("get_space_state"), &PhysicsDirectBodyState2D::get_space_state);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "step"), "", "get_step");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "inverse_mass"), "", "get_inverse_mass");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "inverse_inertia"), "", "get_inverse_inertia");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "total_angular_damp"), "", "get_total_angular_damp");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "total_linear_damp"), "", "get_total_linear_damp");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "total_gravity"), "", "get_total_gravity");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "center_of_mass"), "", "get_center_of_mass");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "center_of_mass_local"), "", "get_center_of_mass_local");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "angular_velocity"), "set_angular_velocity", "get_angular_velocity");
	ADD_PROPERTY(PropertyInfo(Variant::VECTOR2, "linear_velocity"), "set_linear_velocity", "get_linear_velocity");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "sleeping"), "set_sleep_state", "is_sleeping");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collision_layer"), "set_collision_layer", "get_collision_layer");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "collision_mask"), "set_collision_mask", "get_collision_mask");
	ADD_PROPERTY(PropertyInfo(Variant::TRANSFORM2D, "transform"), "set_transform", "get_transform");
}

PhysicsDirectBodyState2D::PhysicsDirectBodyState2D() {}
