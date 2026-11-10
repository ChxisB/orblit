/**************************************************************************/
/*  animatable_body_planar.cpp                                                */

#include "animatable_body_planar.h"

#include "core/config/engine.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "systems/physics_planar/direct_states/physics_direct_body_state_planar.h"
#include "systems/physics_planar/physics_system_planar.h"

void AnimatableBody2D::set_sync_to_physics(bool p_enable) {
	if (sync_to_physics == p_enable) {
		return;
	}

	sync_to_physics = p_enable;

	_update_kinematic_motion();
}

bool AnimatableBody2D::is_sync_to_physics_enabled() const {
	return sync_to_physics;
}

void AnimatableBody2D::_update_kinematic_motion() {
#ifdef TOOLS_ENABLED
	if (Engine::get_singleton()->is_editor_hint()) {
		return;
	}
#endif

	if (sync_to_physics) {
		PhysicsSystem2D::get_singleton()->body_set_state_sync_callback(get_rid(), callable_mp(this, &AnimatableBody2D::_body_state_changed));
		set_only_update_transform_changes(true);
		set_notify_local_transform(true);
	} else {
		PhysicsSystem2D::get_singleton()->body_set_state_sync_callback(get_rid(), Callable());
		set_only_update_transform_changes(false);
		set_notify_local_transform(false);
	}
}

void AnimatableBody2D::_body_state_changed(PhysicsDirectBodyState2D *p_state) {
	if (!sync_to_physics) {
		return;
	}

	last_valid_transform = p_state->get_transform();
	set_notify_local_transform(false);
	set_global_transform(last_valid_transform);
	set_notify_local_transform(true);
}

void AnimatableBody2D::_notification(int p_what) {
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
			Transform2D new_transform = get_global_transform();

			PhysicsSystem2D::get_singleton()->body_set_state(get_rid(), PS2DE::BODY_STATE_TRANSFORM, new_transform);

			// ... but then revert changes.
			set_notify_local_transform(false);
			set_global_transform(last_valid_transform);
			set_notify_local_transform(true);
		} break;
	}
}

void AnimatableBody2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_sync_to_physics", "enable"), &AnimatableBody2D::set_sync_to_physics);
	ClassRegistry::bind_method(D_METHOD("is_sync_to_physics_enabled"), &AnimatableBody2D::is_sync_to_physics_enabled);

	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "sync_to_physics"), "set_sync_to_physics", "is_sync_to_physics_enabled");
}

AnimatableBody2D::AnimatableBody2D() :
		StaticBody2D(PS2DE::BODY_MODE_KINEMATIC) {
}
