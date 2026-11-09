/**************************************************************************/
/*  ip_windows.h                                                          */

#pragma once

#if defined(WINDOWS_ENABLED)

#include "core/io/ip.h"

class IPWindows : public IP {
	ORBLIT_SOFTCLASS(IPWindows, IP);

	virtual void _resolve_hostname(List<IPAddress> &r_addresses, const String &p_hostname, Type p_type = TYPE_ANY) const override;

	static IP *_create_unix();

public:
	virtual void get_local_interfaces(HashMap<String, Interface_Info> *r_interfaces) const override;

	static void make_default();
	IPWindows();
};

#endif // WINDOWS_ENABLED
