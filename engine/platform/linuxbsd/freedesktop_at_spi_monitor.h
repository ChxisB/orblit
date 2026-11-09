/**************************************************************************/
/*  freedesktop_at_spi_monitor.h                                          */

#pragma once

#ifdef DBUS_ENABLED

#include "core/os/thread.h"
#include "core/containers/safe_refcount.h"

class FreeDesktopAtSPIMonitor {
private:
	Thread thread;

	SafeFlag exit_thread;
	SafeFlag ac_enabled;
	SafeFlag sr_enabled;
	SafeFlag supported;

	static void monitor_thread_func(void *p_userdata);

public:
	FreeDesktopAtSPIMonitor();
	~FreeDesktopAtSPIMonitor();

	bool is_supported() { return supported.is_set(); }
	bool is_active() { return sr_enabled.is_set() && ac_enabled.is_set(); }
};

#endif // DBUS_ENABLED
