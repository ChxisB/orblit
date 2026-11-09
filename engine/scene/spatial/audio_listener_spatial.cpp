/**************************************************************************/
/*  audio_listener_spatial.cpp                                                 */

#include "audio_listener_spatial.h"

#include "core/object/class_registry.h"
#include "scene/stage/viewport.h"

void AudioListener3D::_update_audio_listener_state() {
}

void AudioListener3D::_request_listener_update() {
	_update_listener();
}

bool AudioListener3D::_set(const StringName &p_name, const Variant &p_value) {
	if (p_name == "current") {
		if (p_value.operator bool()) {
			make_current();
		} else {
			clear_current();
		}
	} else {
		return false;
	}

	return true;
}

bool AudioListener3D::_get(const StringName &p_name, Variant &r_ret) const {
	if (p_name == "current") {
		if (is_part_of_edited_scene()) {
			r_ret = current;
		} else {
			r_ret = is_current();
		}
	} else {
		return false;
	}

	return true;
}

void AudioListener3D::_get_property_list(List<PropertyInfo> *p_list) const {
	p_list->push_back(PropertyInfo(Variant::BOOL, PNAME("current")));
}

void AudioListener3D::_update_listener() {
	if (is_inside_stage() && is_current()) {
		get_viewport()->_listener_transform_3d_changed_notify();
	}
}

void AudioListener3D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_WORLD: {
			bool first_listener = get_viewport()->_audio_listener_3d_add(this);
			if (!is_part_of_edited_scene() && (current || first_listener)) {
				make_current();
			}
		} break;

		case NOTIFICATION_TRANSFORM_CHANGED: {
			_request_listener_update();
			if (doppler_tracking != DOPPLER_TRACKING_DISABLED) {
				velocity_tracker->update_position(get_global_transform().origin);
			}
		} break;

		case NOTIFICATION_EXIT_WORLD: {
			if (!is_part_of_edited_scene()) {
				if (is_current()) {
					clear_current();
					current = true; //keep it true

				} else {
					current = false;
				}
			}

			get_viewport()->_audio_listener_3d_remove(this);
		} break;
	}
}

Transform3D AudioListener3D::get_listener_transform() const {
	return get_global_transform().orthonormalized();
}

void AudioListener3D::make_current() {
	current = true;

	if (!is_inside_stage()) {
		return;
	}

	get_viewport()->_audio_listener_3d_set(this);
}

void AudioListener3D::clear_current() {
	current = false;
	if (!is_inside_stage()) {
		return;
	}

	if (get_viewport()->get_audio_listener_3d() == this) {
		get_viewport()->_audio_listener_3d_set(nullptr);
		get_viewport()->_audio_listener_3d_make_next_current(this);
	}
}

bool AudioListener3D::is_current() const {
	if (is_inside_stage() && !is_part_of_edited_scene()) {
		return get_viewport()->get_audio_listener_3d() == this;
	} else {
		return current;
	}
}

void AudioListener3D::set_doppler_tracking(DopplerTracking p_tracking) {
	if (doppler_tracking == p_tracking) {
		return;
	}

	doppler_tracking = p_tracking;
	if (p_tracking != DOPPLER_TRACKING_DISABLED) {
		velocity_tracker->set_track_physics_step(doppler_tracking == DOPPLER_TRACKING_PHYSICS_STEP);
		if (is_inside_stage()) {
			velocity_tracker->reset(get_global_transform().origin);
		}
	}
}

AudioListener3D::DopplerTracking AudioListener3D::get_doppler_tracking() const {
	return doppler_tracking;
}

void AudioListener3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("make_current"), &AudioListener3D::make_current);
	ClassRegistry::bind_method(D_METHOD("clear_current"), &AudioListener3D::clear_current);
	ClassRegistry::bind_method(D_METHOD("is_current"), &AudioListener3D::is_current);
	ClassRegistry::bind_method(D_METHOD("get_listener_transform"), &AudioListener3D::get_listener_transform);
	ClassRegistry::bind_method(D_METHOD("set_doppler_tracking", "mode"), &AudioListener3D::set_doppler_tracking);
	ClassRegistry::bind_method(D_METHOD("get_doppler_tracking"), &AudioListener3D::get_doppler_tracking);

	ADD_PROPERTY(PropertyInfo(Variant::INT, "doppler_tracking", PROPERTY_HINT_ENUM, "Disabled,Idle,Physics"), "set_doppler_tracking", "get_doppler_tracking");

	BIND_ENUM_CONSTANT(DOPPLER_TRACKING_DISABLED);
	BIND_ENUM_CONSTANT(DOPPLER_TRACKING_IDLE_STEP);
	BIND_ENUM_CONSTANT(DOPPLER_TRACKING_PHYSICS_STEP);
}

Vector3 AudioListener3D::get_doppler_tracked_velocity() const {
	if (doppler_tracking != DOPPLER_TRACKING_DISABLED) {
		return velocity_tracker->get_tracked_linear_velocity();
	} else {
		return Vector3();
	}
}

AudioListener3D::AudioListener3D() {
	set_notify_transform(true);
	velocity_tracker.instantiate();
}

AudioListener3D::~AudioListener3D() {
}
