/**************************************************************************/
/*  crash_handler_macos.h                                                 */

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
