/**************************************************************************/
/*  tcp_server.h                                                          */

#pragma once

#include "core/io/ip_address.h"
#include "core/io/socket_server.h"
#include "core/io/stream_peer_tcp.h"

class TCPServer : public SocketServer {
	ORBLIT_CLASS(TCPServer, SocketServer);

protected:
	static void register_members();

public:
	Error listen(uint16_t p_port, const IPAddress &p_bind_address = IPAddress("*"));
	int get_local_port() const;
	Ref<StreamPeerTCP> take_connection();
	Ref<StreamPeerSocket> take_socket_connection() override { return take_connection(); }
};
