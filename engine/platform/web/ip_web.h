/**************************************************************************/
/*  ip_web.h                                                              */

#pragma once

#include "core/io/ip.h"

class IPWeb : public IP {
	ORBLIT_SOFTCLASS(IPWeb, IP);

	virtual void _resolve_hostname(List<IPAddress> &r_addresses, const String &p_hostname, Type p_type = TYPE_ANY) const override {}

private:
	static IP *_create_web() {
		return memnew(IPWeb);
	}

public:
	virtual void get_local_interfaces(HashMap<String, Interface_Info> *r_interfaces) const override {}

	static void make_default() {
		_create = _create_web;
	}
};
