/**************************************************************************/
/*  body_direct_state_planar.cpp                                              */

#include "body_direct_state_planar.h"

#include "body_planar.h"
#include "space_planar.h"

Vector2 OrblitPhysicsDirectBodyState2D::get_total_gravity() const {
	return body->gravity;
}

real_t OrblitPhysicsDirectBodyState2D::get_total_angular_damp() const {
	return body->total_angular_damp;
}

real_t OrblitPhysicsDirectBodyState2D::get_total_linear_damp() const {
	return body->total_linear_damp;
}

Vector2 OrblitPhysicsDirectBodyState2D::get_center_of_mass() const {
	return body->get_center_of_mass();
}

Vector2 OrblitPhysicsDirectBodyState2D::get_center_of_mass_local() const {
	return body->get_center_of_mass_local();
}

real_t OrblitPhysicsDirectBodyState2D::get_inverse_mass() const {
	return body->get_inv_mass();
}

real_t OrblitPhysicsDirectBodyState2D::get_inverse_inertia() const {
	return body->get_inv_inertia();
}

void OrblitPhysicsDirectBodyState2D::set_linear_velocity(const Vector2 &p_velocity) {
	body->wakeup();
	body->set_linear_velocity(p_velocity);
}

Vector2 OrblitPhysicsDirectBodyState2D::get_linear_velocity() const {
	return body->get_linear_velocity();
}

void OrblitPhysicsDirectBodyState2D::set_angular_velocity(real_t p_velocity) {
	body->wakeup();
	body->set_angular_velocity(p_velocity);
}

real_t OrblitPhysicsDirectBodyState2D::get_angular_velocity() const {
	return body->get_angular_velocity();
}

void OrblitPhysicsDirectBodyState2D::set_transform(const Transform2D &p_transform) {
	body->set_state(PS2DE::BODY_STATE_TRANSFORM, p_transform);
}

Transform2D OrblitPhysicsDirectBodyState2D::get_transform() const {
	return body->get_transform();
}

Vector2 OrblitPhysicsDirectBodyState2D::get_velocity_at_local_position(const Vector2 &p_position) const {
	return body->get_velocity_in_local_point(p_position);
}

void OrblitPhysicsDirectBodyState2D::apply_central_impulse(const Vector2 &p_impulse) {
	body->wakeup();
	body->apply_central_impulse(p_impulse);
}

void OrblitPhysicsDirectBodyState2D::apply_impulse(const Vector2 &p_impulse, const Vector2 &p_position) {
	body->wakeup();
	body->apply_impulse(p_impulse, p_position);
}

void OrblitPhysicsDirectBodyState2D::apply_torque_impulse(real_t p_torque) {
	body->wakeup();
	body->apply_torque_impulse(p_torque);
}

void OrblitPhysicsDirectBodyState2D::apply_central_force(const Vector2 &p_force) {
	body->wakeup();
	body->apply_central_force(p_force);
}

void OrblitPhysicsDirectBodyState2D::apply_force(const Vector2 &p_force, const Vector2 &p_position) {
	body->wakeup();
	body->apply_force(p_force, p_position);
}

void OrblitPhysicsDirectBodyState2D::apply_torque(real_t p_torque) {
	body->wakeup();
	body->apply_torque(p_torque);
}

void OrblitPhysicsDirectBodyState2D::add_constant_central_force(const Vector2 &p_force) {
	body->wakeup();
	body->add_constant_central_force(p_force);
}

void OrblitPhysicsDirectBodyState2D::add_constant_force(const Vector2 &p_force, const Vector2 &p_position) {
	body->wakeup();
	body->add_constant_force(p_force, p_position);
}

void OrblitPhysicsDirectBodyState2D::add_constant_torque(real_t p_torque) {
	body->wakeup();
	body->add_constant_torque(p_torque);
}

void OrblitPhysicsDirectBodyState2D::set_constant_force(const Vector2 &p_force) {
	if (!p_force.is_zero_approx()) {
		body->wakeup();
	}
	body->set_constant_force(p_force);
}

Vector2 OrblitPhysicsDirectBodyState2D::get_constant_force() const {
	return body->get_constant_force();
}

void OrblitPhysicsDirectBodyState2D::set_constant_torque(real_t p_torque) {
	if (!Math::is_zero_approx(p_torque)) {
		body->wakeup();
	}
	body->set_constant_torque(p_torque);
}

real_t OrblitPhysicsDirectBodyState2D::get_constant_torque() const {
	return body->get_constant_torque();
}

void OrblitPhysicsDirectBodyState2D::set_sleep_state(bool p_enable) {
	body->set_active(!p_enable);
}

bool OrblitPhysicsDirectBodyState2D::is_sleeping() const {
	return !body->is_active();
}

void OrblitPhysicsDirectBodyState2D::set_collision_layer(uint32_t p_layer) {
	body->set_collision_layer(p_layer);
}

uint32_t OrblitPhysicsDirectBodyState2D::get_collision_layer() const {
	return body->get_collision_layer();
}

void OrblitPhysicsDirectBodyState2D::set_collision_mask(uint32_t p_mask) {
	body->set_collision_mask(p_mask);
}

uint32_t OrblitPhysicsDirectBodyState2D::get_collision_mask() const {
	return body->get_collision_mask();
}

int OrblitPhysicsDirectBodyState2D::get_contact_count() const {
	return body->contact_count;
}

Vector2 OrblitPhysicsDirectBodyState2D::get_contact_local_position(int p_contact_idx) const {
	ERR_FAIL_INDEX_V(p_contact_idx, body->contact_count, Vector2());
	return body->contacts[p_contact_idx].local_pos;
}

Vector2 OrblitPhysicsDirectBodyState2D::get_contact_local_normal(int p_contact_idx) const {
	ERR_FAIL_INDEX_V(p_contact_idx, body->contact_count, Vector2());
	return body->contacts[p_contact_idx].local_normal;
}

int OrblitPhysicsDirectBodyState2D::get_contact_local_shape(int p_contact_idx) const {
	ERR_FAIL_INDEX_V(p_contact_idx, body->contact_count, -1);
	return body->contacts[p_contact_idx].local_shape;
}

Vector2 OrblitPhysicsDirectBodyState2D::get_contact_local_velocity_at_position(int p_contact_idx) const {
	ERR_FAIL_INDEX_V(p_contact_idx, body->contact_count, Vector2());
	return body->contacts[p_contact_idx].local_velocity_at_pos;
}

RID OrblitPhysicsDirectBodyState2D::get_contact_collider(int p_contact_idx) const {
	ERR_FAIL_INDEX_V(p_contact_idx, body->contact_count, RID());
	return body->contacts[p_contact_idx].collider;
}
Vector2 OrblitPhysicsDirectBodyState2D::get_contact_collider_position(int p_contact_idx) const {
	ERR_FAIL_INDEX_V(p_contact_idx, body->contact_count, Vector2());
	return body->contacts[p_contact_idx].collider_pos;
}

ObjectID OrblitPhysicsDirectBodyState2D::get_contact_collider_id(int p_contact_idx) const {
	ERR_FAIL_INDEX_V(p_contact_idx, body->contact_count, ObjectID());
	return body->contacts[p_contact_idx].collider_instance_id;
}

int OrblitPhysicsDirectBodyState2D::get_contact_collider_shape(int p_contact_idx) const {
	ERR_FAIL_INDEX_V(p_contact_idx, body->contact_count, 0);
	return body->contacts[p_contact_idx].collider_shape;
}

Vector2 OrblitPhysicsDirectBodyState2D::get_contact_collider_velocity_at_position(int p_contact_idx) const {
	ERR_FAIL_INDEX_V(p_contact_idx, body->contact_count, Vector2());
	return body->contacts[p_contact_idx].collider_velocity_at_pos;
}

Vector2 OrblitPhysicsDirectBodyState2D::get_contact_impulse(int p_contact_idx) const {
	ERR_FAIL_INDEX_V(p_contact_idx, body->contact_count, Vector2());
	return body->contacts[p_contact_idx].impulse;
}

RequiredResult<PhysicsDirectSpaceState2D> OrblitPhysicsDirectBodyState2D::get_space_state() {
	return body->get_space()->get_direct_state();
}

real_t OrblitPhysicsDirectBodyState2D::get_step() const {
	return body->get_space()->get_last_step();
}
