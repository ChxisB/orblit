/**************************************************************************/
/*  animatable_body_spatial.h                                                  */

#pragma once

#include "scene/spatial/physics/static_body_spatial.h"

class AnimatableBody3D : public StaticBody3D {
	ORBLIT_CLASS(AnimatableBody3D, StaticBody3D);

private:
	Vector3 linear_velocity;
	Vector3 angular_velocity;

	bool sync_to_physics = true;

	Transform3D last_valid_transform;

	static void _body_state_changed_callback(void *p_instance, PhysicsDirectBodyState3D *p_state);
	void _body_state_changed(PhysicsDirectBodyState3D *p_state);

protected:
	void _notification(int p_what);
	static void register_members();

public:
	virtual Vector3 get_linear_velocity() const override;
	virtual Vector3 get_angular_velocity() const override;

	AnimatableBody3D();

private:
	void _update_kinematic_motion();

	void set_sync_to_physics(bool p_enable);
	bool is_sync_to_physics_enabled() const;
};
