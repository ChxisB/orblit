/**************************************************************************/
/*  audio_listener_planar.cpp                                                 */

#include "audio_listener_planar.h"

#include "core/object/class_registry.h"
#include "scene/stage/viewport.h"

bool AudioListener2D::_set(const StringName &p_name, const Variant &p_value) {
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

bool AudioListener2D::_get(const StringName &p_name, Variant &r_ret) const {
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

void AudioListener2D::_get_property_list(List<PropertyInfo> *p_list) const {
	p_list->push_back(PropertyInfo(Variant::BOOL, PNAME("current")));
}

void AudioListener2D::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			if (!is_part_of_edited_scene() && current) {
				make_current();
			}
		} break;

		case NOTIFICATION_EXIT_STAGE: {
			if (!is_part_of_edited_scene()) {
				if (is_current()) {
					clear_current();
					current = true; // Keep it true.
				} else {
					current = false;
				}
			}
		} break;
	}
}

void AudioListener2D::make_current() {
	current = true;
	if (!is_inside_stage()) {
		return;
	}
	get_viewport()->_audio_listener_2d_set(this);
}

void AudioListener2D::clear_current() {
	current = false;
	if (!is_inside_stage()) {
		return;
	}
	get_viewport()->_audio_listener_2d_remove(this);
}

bool AudioListener2D::is_current() const {
	if (is_inside_stage() && !is_part_of_edited_scene()) {
		return get_viewport()->get_audio_listener_2d() == this;
	} else {
		return current;
	}
}

void AudioListener2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("make_current"), &AudioListener2D::make_current);
	ClassRegistry::bind_method(D_METHOD("clear_current"), &AudioListener2D::clear_current);
	ClassRegistry::bind_method(D_METHOD("is_current"), &AudioListener2D::is_current);
}

AudioListener2D::AudioListener2D() {
	set_hide_clip_children(true);
}
