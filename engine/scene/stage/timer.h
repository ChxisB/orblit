/**************************************************************************/
/*  timer.h                                                               */

#pragma once

#include "scene/stage/entity.h"

class Timer : public Entity {
	ORBLIT_CLASS(Timer, Entity);

	double wait_time = 1.0;
	bool one_shot = false;
	bool autostart = false;
	bool processing = false;
	bool paused = false;
	bool ignore_time_scale = false;

	double time_left = -1.0;

protected:
	void _notification(int p_what);
	static void register_members();

public:
	enum TimerProcessCallback {
		TIMER_PROCESS_PHYSICS,
		TIMER_PROCESS_IDLE,
	};

	void set_wait_time(double p_time);
	double get_wait_time() const;

	void set_one_shot(bool p_one_shot);
	bool is_one_shot() const;

	void set_autostart(bool p_start);
	bool has_autostart() const;

	void start(double p_time = -1);
	void stop();

	void set_paused(bool p_paused);
	bool is_paused() const;

	void set_ignore_time_scale(bool p_ignore);
	bool is_ignoring_time_scale();

	bool is_stopped() const;

	double get_time_left() const;

	PackedStringArray get_configuration_warnings() const override;

	void set_timer_process_callback(TimerProcessCallback p_callback);
	TimerProcessCallback get_timer_process_callback() const;

private:
	TimerProcessCallback timer_process_callback = TIMER_PROCESS_IDLE;
	void _set_process(bool p_process, bool p_force = false);
};

VARIANT_ENUM_CAST(Timer::TimerProcessCallback);
