/**************************************************************************/
/*  net_socket_web.h                                                      */

#pragma once

#include "core/io/net_socket.h"

#include <sys/socket.h>

class NetSocketWeb : public NetSocket {
	ORBLIT_SOFTCLASS(NetSocketWeb, NetSocket);

protected:
	static Ref<NetSocket> _create_func() {
		return memnew(NetSocketWeb);
	}

public:
	static void make_default() {
		_create = _create_func;
	}

	virtual Error open(Family p_family, Type p_sock_type, IP::Type &ip_type) override { return ERR_UNAVAILABLE; }
	virtual void close() override {}
	virtual Error bind(Address p_addr) override { return ERR_UNAVAILABLE; }
	virtual Error listen(int p_max_pending) override { return ERR_UNAVAILABLE; }
	virtual Error connect_to_host(Address p_addr) override { return ERR_UNAVAILABLE; }
	virtual Error poll(PollType p_type, int timeout) const override { return ERR_UNAVAILABLE; }
	virtual Error recv(uint8_t *p_buffer, int p_len, int &r_read) override { return ERR_UNAVAILABLE; }
	virtual Error recvfrom(uint8_t *p_buffer, int p_len, int &r_read, IPAddress &r_ip, uint16_t &r_port, bool p_peek = false) override { return ERR_UNAVAILABLE; }
	virtual Error send(const uint8_t *p_buffer, int p_len, int &r_sent) override { return ERR_UNAVAILABLE; }
	virtual Error sendto(const uint8_t *p_buffer, int p_len, int &r_sent, IPAddress p_ip, uint16_t p_port) override { return ERR_UNAVAILABLE; }
	virtual Ref<NetSocket> accept(Address &r_addr) override { return Ref<NetSocket>(); }

	virtual bool is_open() const override { return false; }
	virtual int get_available_bytes() const override { return -1; }
	virtual Error get_socket_address(Address *r_addr) const override { return ERR_UNAVAILABLE; }

	virtual Error set_broadcasting_enabled(bool p_enabled) override { return ERR_UNAVAILABLE; }
	virtual void set_blocking_enabled(bool p_enabled) override {}
	virtual void set_ipv6_only_enabled(bool p_enabled) override {}
	virtual void set_tcp_no_delay_enabled(bool p_enabled) override {}
	virtual void set_reuse_address_enabled(bool p_enabled) override {}
	virtual Error join_multicast_group(const IPAddress &p_multi_address, const String &p_if_name) override { return ERR_UNAVAILABLE; }
	virtual Error leave_multicast_group(const IPAddress &p_multi_address, const String &p_if_name) override { return ERR_UNAVAILABLE; }
};
