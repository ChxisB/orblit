/**************************************************************************/
/*  freedesktop_screensaver.h                                             */

#pragma once

#ifdef DBUS_ENABLED

#include <cstdint>

class FreeDesktopScreenSaver {
private:
	uint32_t cookie = 0;
	bool unsupported = false;

public:
	FreeDesktopScreenSaver();
	void inhibit();
	void uninhibit();
};

#endif // DBUS_ENABLED
