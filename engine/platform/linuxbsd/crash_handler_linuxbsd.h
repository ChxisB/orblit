/**************************************************************************/
/*  crash_handler_linuxbsd.h                                              */

#pragma once

class CrashHandler {
	bool disabled;

public:
	void initialize();

	void disable();
	bool is_disabled() const { return disabled; }

	CrashHandler();
	~CrashHandler();
};
