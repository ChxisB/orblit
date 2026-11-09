/**************************************************************************/
/*  ip_unix.h                                                             */

#pragma once

#if defined(UNIX_ENABLED) && !defined(UNIX_SOCKET_UNAVAILABLE)

#include "core/io/ip.h"

class IPUnix : public IP {
	ORBLIT_SOFTCLASS(IPUnix, IP);

	virtual void _resolve_hostname(List<IPAddress> &r_addresses, const String &p_hostname, Type p_type = TYPE_ANY) const override;

	static IP *_create_unix();

public:
	virtual void get_local_interfaces(HashMap<String, Interface_Info> *r_interfaces) const override;

	static void make_default();
	IPUnix();
};

#endif // UNIX_ENABLED
