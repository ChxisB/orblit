/**************************************************************************/
/*  animatable_body_spatial.cpp                                                */

#include "animatable_body_spatial.h"

#include "core/config/engine.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"

Vector3 AnimatableBody3D::get_linear_velocity() const {
	return linear_velocity;
}

Vector3 AnimatableBody3D::get_angular_velocity() const {
	return angular_velocity;
}

void AnimatableBody3D::set_sync_to_physics(bool p_enable) {
	if (sync_to_physics == p_enable) {
		return;
	}

	sync_to_physics = p_enable;

	_update_kinematic_motion();
}

bool AnimatableBody3D::is_sync_to_physics_enabled() const {
	return sync_to_physics;
}

void AnimatableBody3D::_update_kinematic_motion() {
#ifdef TOOLS_ENABLED
	if (Engine::get_singleton()->is_editor_hint()) {
		return;
	}
#endif

	if (sync_to_physics) {
		set_only_update_transform_changes(true);
		set_notify_local_transform(true);
	} else {
		set_only_update_transform_changes(false);
		set_notify_local_transform(false);
	}
}

void AnimatableBody3D::_body_state_changed(PhysicsDirectBodyState3D *p_state) {
	linear_velocity = p_state->get_linear_velocity();
	angular_velocity = p_state->get_angular_velocity();

	if (!sync_to_physics) {
		return;
	}

	last_valid_transform = p_state->get_transform();
	set_notify_local_transform(false);
	set_global_transform(last_valid_transform);
	set_notify_local_transform(true);
	_on_transform_changed();
}

void AnimatableBody3D::_notification(int p_what) {
#ifdef TOOLS_ENABLED
	if (Engine::get_singleton()->is_editor_hint()) {
		return;
	}
#endif
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			last_valid_transform = get_global_transform();
			_update_kinematic_motion();
		} break;

		case NOTIFICATION_EXIT_STAGE: {
			set_only_update_transform_changes(false);
			set_notify_local_transform(false);
		} break;

		case NOTIFICATION_LOCAL_TRANSFORM_CHANGED: {
			// Used by sync to physics, send the new transform to the physics...
			Transform3D new_transform = get_global_transform();

			PhysicsSystem3D::get_singleton()->body_set_state(get_rid(), PS3DE::BODY_STATE_TRANSFORM, new_transform);

			// ... but then revert changes.
			set_notify_local_transform(false);
			set_global_transform(last_valid_transform);
			set_notify_local_transform(true);
			_on_transform_changed();
		} break;
	}
}

void AnimatableBody3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_sync_to_physics", "enable"), &AnimatableBody3D::set_sync_to_physics);
	ClassRegistry::bind_method(D_METHOD("is_sync_to_physics_enabled"), &AnimatableBody3D::is_sync_to_physics_enabled);

	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "sync_to_physics"), "set_sync_to_physics", "is_sync_to_physics_enabled");
}

AnimatableBody3D::AnimatableBody3D() :
		StaticBody3D(PS3DE::BODY_MODE_KINEMATIC) {
	PhysicsSystem3D::get_singleton()->body_set_state_sync_callback(get_rid(), callable_mp(this, &AnimatableBody3D::_body_state_changed));
}
