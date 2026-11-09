/**************************************************************************/
/*  timer.cpp                                                             */

#include "timer.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"

void Timer::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_START: {
			if (autostart) {
#ifdef TOOLS_ENABLED
				if (is_part_of_edited_scene()) {
					break;
				}
#endif
				start();
				autostart = false;
			}
		} break;

		case NOTIFICATION_INTERNAL_FRAME: {
			if (!processing || timer_process_callback == TIMER_PROCESS_PHYSICS || !is_internal_frame_enabled()) {
				return;
			}
			if (ignore_time_scale) {
				time_left -= Engine::get_singleton()->get_process_step();
			} else {
				time_left -= get_frame_delta();
			}

			if (time_left < 0) {
				if (!one_shot) {
					time_left += wait_time;
				} else {
					stop();
				}

				emit_signal(SNAME("timeout"));
			}
		} break;

		case NOTIFICATION_INTERNAL_STEP: {
			if (!processing || timer_process_callback == TIMER_PROCESS_IDLE || !is_internal_step_enabled()) {
				return;
			}
			if (ignore_time_scale) {
				time_left -= Engine::get_singleton()->get_process_step();
			} else {
				time_left -= get_step_delta();
			}

			if (time_left < 0) {
				if (!one_shot) {
					time_left += wait_time;
				} else {
					stop();
				}
				emit_signal(SNAME("timeout"));
			}
		} break;
	}
}

void Timer::set_wait_time(double p_time) {
	ERR_FAIL_COND_MSG(p_time <= 0, "Time should be greater than zero.");
	wait_time = p_time;
	update_configuration_warnings();
}

double Timer::get_wait_time() const {
	return wait_time;
}

void Timer::set_one_shot(bool p_one_shot) {
	one_shot = p_one_shot;
}

bool Timer::is_one_shot() const {
	return one_shot;
}

void Timer::set_autostart(bool p_start) {
	autostart = p_start;
}

bool Timer::has_autostart() const {
	return autostart;
}

void Timer::start(double p_time) {
	ERR_FAIL_COND_MSG(!is_inside_stage(), "Unable to start the timer because it's not inside the scene tree. Either add it or set autostart to true.");

	if (p_time > 0) {
		set_wait_time(p_time);
	}
	time_left = wait_time;
	_set_process(true);
}

void Timer::stop() {
	time_left = -1;
	_set_process(false);
	autostart = false;
}

void Timer::set_paused(bool p_paused) {
	if (paused == p_paused) {
		return;
	}

	paused = p_paused;
	_set_process(processing);
}

bool Timer::is_paused() const {
	return paused;
}

void Timer::set_ignore_time_scale(bool p_ignore) {
	ignore_time_scale = p_ignore;
}

bool Timer::is_ignoring_time_scale() {
	return ignore_time_scale;
}

bool Timer::is_stopped() const {
	return get_time_left() <= 0;
}

double Timer::get_time_left() const {
	return time_left > 0 ? time_left : 0;
}

void Timer::set_timer_process_callback(TimerProcessCallback p_callback) {
	if (timer_process_callback == p_callback) {
		return;
	}

	switch (timer_process_callback) {
		case TIMER_PROCESS_PHYSICS:
			if (is_internal_step_enabled()) {
				set_internal_step_enabled(false);
				set_internal_frame_enabled(true);
			}
			break;
		case TIMER_PROCESS_IDLE:
			if (is_internal_frame_enabled()) {
				set_internal_frame_enabled(false);
				set_internal_step_enabled(true);
			}
			break;
	}
	timer_process_callback = p_callback;
}

Timer::TimerProcessCallback Timer::get_timer_process_callback() const {
	return timer_process_callback;
}

void Timer::_set_process(bool p_process, bool p_force) {
	switch (timer_process_callback) {
		case TIMER_PROCESS_PHYSICS:
			set_internal_step_enabled(p_process && !paused);
			break;
		case TIMER_PROCESS_IDLE:
			set_internal_frame_enabled(p_process && !paused);
			break;
	}
	processing = p_process;
}

PackedStringArray Timer::get_configuration_warnings() const {
	PackedStringArray warnings = Entity::get_configuration_warnings();

	if (wait_time < 0.05 - CMP_EPSILON) {
		warnings.push_back(RTR("Very low timer wait times (< 0.05 seconds) may behave in significantly different ways depending on the rendered or physics frame rate.\nConsider using a script's process loop instead of relying on a Timer for very low wait times."));
	}

	return warnings;
}

void Timer::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_wait_time", "time_sec"), &Timer::set_wait_time);
	ClassRegistry::bind_method(D_METHOD("get_wait_time"), &Timer::get_wait_time);

	ClassRegistry::bind_method(D_METHOD("set_one_shot", "enable"), &Timer::set_one_shot);
	ClassRegistry::bind_method(D_METHOD("is_one_shot"), &Timer::is_one_shot);

	ClassRegistry::bind_method(D_METHOD("set_autostart", "enable"), &Timer::set_autostart);
	ClassRegistry::bind_method(D_METHOD("has_autostart"), &Timer::has_autostart);

	ClassRegistry::bind_method(D_METHOD("start", "time_sec"), &Timer::start, DEFVAL(-1));
	ClassRegistry::bind_method(D_METHOD("stop"), &Timer::stop);

	ClassRegistry::bind_method(D_METHOD("set_paused", "paused"), &Timer::set_paused);
	ClassRegistry::bind_method(D_METHOD("is_paused"), &Timer::is_paused);

	ClassRegistry::bind_method(D_METHOD("set_ignore_time_scale", "ignore"), &Timer::set_ignore_time_scale);
	ClassRegistry::bind_method(D_METHOD("is_ignoring_time_scale"), &Timer::is_ignoring_time_scale);

	ClassRegistry::bind_method(D_METHOD("is_stopped"), &Timer::is_stopped);

	ClassRegistry::bind_method(D_METHOD("get_time_left"), &Timer::get_time_left);

	ClassRegistry::bind_method(D_METHOD("set_timer_process_callback", "callback"), &Timer::set_timer_process_callback);
	ClassRegistry::bind_method(D_METHOD("get_timer_process_callback"), &Timer::get_timer_process_callback);

	ADD_SIGNAL(MethodInfo("timeout"));

	ADD_PROPERTY(PropertyInfo(Variant::INT, "process_callback", PROPERTY_HINT_ENUM, "Physics,Idle"), "set_timer_process_callback", "get_timer_process_callback");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "wait_time", PROPERTY_HINT_RANGE, "0.001,4096,0.001,or_greater,exp,suffix:s"), "set_wait_time", "get_wait_time");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "one_shot"), "set_one_shot", "is_one_shot");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "autostart"), "set_autostart", "has_autostart");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "paused", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NONE), "set_paused", "is_paused");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "ignore_time_scale"), "set_ignore_time_scale", "is_ignoring_time_scale");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "time_left", PROPERTY_HINT_NONE, "suffix:s", PROPERTY_USAGE_NONE), "", "get_time_left");

	BIND_ENUM_CONSTANT(TIMER_PROCESS_PHYSICS);
	BIND_ENUM_CONSTANT(TIMER_PROCESS_IDLE);
}
