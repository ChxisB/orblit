/**************************************************************************/
/*  crash_handler_windows.h                                               */

#pragma once

#include <windows.h>

// Crash handler exception only enabled with MSVC
#if defined(DEBUG_ENABLED)
#define CRASH_HANDLER_EXCEPTION 1

#ifdef _MSC_VER
extern DWORD CrashHandlerException(EXCEPTION_POINTERS *ep);
#endif

#endif

class CrashHandler {
	bool disabled;

public:
	void initialize();

	void disable();
	bool is_disabled() const { return disabled; }

	CrashHandler();
	~CrashHandler();
};
