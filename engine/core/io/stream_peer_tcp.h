/**************************************************************************/
/*  stream_peer_tcp.h                                                     */

#pragma once

#include "core/io/ip_address.h"
#include "core/io/stream_peer_socket.h"

class StreamPeerTCP : public StreamPeerSocket {
	ORBLIT_CLASS(StreamPeerTCP, StreamPeerSocket);

protected:
	Error _connect(const String &p_address, int p_port);

	static void register_members();

public:
	void accept_socket(Ref<NetSocket> p_sock, const NetSocket::Address &p_addr) override;

	Error bind(int p_port, const IPAddress &p_host);
	Error connect_to_host(const IPAddress &p_host, int p_port);
	IPAddress get_connected_host() const;
	int get_connected_port() const;
	int get_local_port() const;

	void set_no_delay(bool p_enabled);
};
