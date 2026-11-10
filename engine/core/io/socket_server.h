/**************************************************************************/
/*  socket_server.h                                                       */

#pragma once

#include "core/io/net_socket.h"
#include "core/io/stream_peer_socket.h"

class SocketServer : public RefCounted {
	ORBLIT_CLASS(SocketServer, RefCounted);

protected:
	enum {
		MAX_PENDING_CONNECTIONS = 8,
	};

	Ref<NetSocket> _sock;
	static void register_members();

	Error _listen(const NetSocket::Address &p_addr);

	template <typename T>
	Ref<T> _take_connection() {
		Ref<T> conn;
		if (!is_connection_available()) {
			return conn;
		}

		Ref<NetSocket> ns;
		NetSocket::Address addr;
		ns = _sock->accept(addr);
		if (ns.is_null()) {
			return conn;
		}

		conn.instantiate();
		conn->accept_socket(ns, addr);
		return conn;
	}

public:
	bool is_listening() const;
	bool is_connection_available() const;
	virtual Ref<StreamPeerSocket> take_socket_connection() = 0;

	void stop(); // Stop listening

	SocketServer();
	~SocketServer();
};
