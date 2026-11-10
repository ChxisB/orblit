/**************************************************************************/
/*  jolt_physics_direct_body_state_spatial.h                                   */

#pragma once

#include "systems/physics_spatial/physics_system_spatial.h"

class JoltBody3D;

class JoltPhysicsDirectBodyState3D final : public PhysicsDirectBodyState3D {
	ORBLIT_CLASS(JoltPhysicsDirectBodyState3D, PhysicsDirectBodyState3D)

	JoltBody3D *body = nullptr;

	static void register_members() {}

public:
	JoltPhysicsDirectBodyState3D() = default;

	explicit JoltPhysicsDirectBodyState3D(JoltBody3D *p_body);

	virtual Vector3 get_total_gravity() const override;
	virtual real_t get_total_linear_damp() const override;
	virtual real_t get_total_angular_damp() const override;

	virtual Vector3 get_center_of_mass() const override;
	virtual Vector3 get_center_of_mass_local() const override;
	virtual Basis get_principal_inertia_axes() const override;

	virtual real_t get_inverse_mass() const override;
	virtual Vector3 get_inverse_inertia() const override;
	virtual Basis get_inverse_inertia_tensor() const override;

	virtual void set_linear_velocity(const Vector3 &p_velocity) override;
	virtual Vector3 get_linear_velocity() const override;

	virtual void set_angular_velocity(const Vector3 &p_velocity) override;
	virtual Vector3 get_angular_velocity() const override;

	virtual void set_transform(const Transform3D &p_transform) override;
	virtual Transform3D get_transform() const override;

	virtual Vector3 get_velocity_at_local_position(const Vector3 &p_local_position) const override;

	virtual void apply_central_impulse(const Vector3 &p_impulse) override;
	virtual void apply_impulse(const Vector3 &p_impulse, const Vector3 &p_position) override;
	virtual void apply_torque_impulse(const Vector3 &p_impulse) override;

	virtual void apply_central_force(const Vector3 &p_force) override;
	virtual void apply_force(const Vector3 &p_force, const Vector3 &p_position) override;
	virtual void apply_torque(const Vector3 &p_torque) override;

	virtual void add_constant_central_force(const Vector3 &p_force) override;
	virtual void add_constant_force(const Vector3 &p_force, const Vector3 &p_position) override;
	virtual void add_constant_torque(const Vector3 &p_torque) override;

	virtual void set_constant_force(const Vector3 &p_force) override;
	virtual Vector3 get_constant_force() const override;

	virtual void set_constant_torque(const Vector3 &p_torque) override;
	virtual Vector3 get_constant_torque() const override;

	virtual void set_sleep_state(bool p_enabled) override;
	virtual bool is_sleeping() const override;

	virtual void set_collision_layer(uint32_t p_layer) override;
	virtual uint32_t get_collision_layer() const override;

	virtual void set_collision_mask(uint32_t p_mask) override;
	virtual uint32_t get_collision_mask() const override;

	virtual int get_contact_count() const override;

	virtual Vector3 get_contact_local_position(int p_contact_idx) const override;
	virtual Vector3 get_contact_local_normal(int p_contact_idx) const override;
	virtual Vector3 get_contact_impulse(int p_contact_idx) const override;
	virtual int get_contact_local_shape(int p_contact_idx) const override;
	virtual Vector3 get_contact_local_velocity_at_position(int p_contact_idx) const override;

	virtual RID get_contact_collider(int p_contact_idx) const override;
	virtual Vector3 get_contact_collider_position(int p_contact_idx) const override;
	virtual ObjectID get_contact_collider_id(int p_contact_idx) const override;
	virtual Object *get_contact_collider_object(int p_contact_idx) const override;
	virtual int get_contact_collider_shape(int p_contact_idx) const override;
	virtual Vector3 get_contact_collider_velocity_at_position(int p_contact_idx) const override;

	virtual real_t get_step() const override;

	virtual void integrate_forces() override;

	virtual RequiredResult<PhysicsDirectSpaceState3D> get_space_state() override;
};
