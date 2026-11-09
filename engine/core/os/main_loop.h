/**************************************************************************/
/*  main_loop.h                                                           */

#pragma once

#include "core/object/object.h"
#include "core/object/orblit_virtual.gen.h"

class MainLoop : public Object {
	ORBLIT_CLASS(MainLoop, Object);

protected:
	static void register_members();

	ORBLIT_VIRTUAL0(_initialize)
	ORBLIT_VIRTUAL1R(bool, _step, double)
	ORBLIT_VIRTUAL1R(bool, _frame, double)
	ORBLIT_VIRTUAL0(_finalize)

public:
	enum {
		//make sure these are replicated in Entity
		NOTIFICATION_OS_MEMORY_WARNING = 2009,
		NOTIFICATION_TRANSLATION_CHANGED = 2010,
		NOTIFICATION_WM_ABOUT = 2011,
		NOTIFICATION_CRASH = 2012,
		NOTIFICATION_OS_IME_UPDATE = 2013,
		NOTIFICATION_APPLICATION_RESUMED = 2014,
		NOTIFICATION_APPLICATION_PAUSED = 2015,
		NOTIFICATION_APPLICATION_FOCUS_IN = 2016,
		NOTIFICATION_APPLICATION_FOCUS_OUT = 2017,
		NOTIFICATION_TEXT_SYSTEM_CHANGED = 2018,
		NOTIFICATION_APPLICATION_PIP_MODE_ENTERED = 2019,
		NOTIFICATION_APPLICATION_PIP_MODE_EXITED = 2020,
	};

	virtual void initialize();
	virtual void iteration_prepare() {}
	virtual bool physics_process(double p_time);
	virtual void iteration_end() {}
	virtual bool process(double p_time);
	virtual void finalize();

	virtual ~MainLoop() {}
};
