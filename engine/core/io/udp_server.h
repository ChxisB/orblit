/**************************************************************************/
/*  udp_server.h                                                          */

#pragma once

#include "core/io/net_socket.h"
#include "core/io/packet_peer_udp.h"

class UDPServer : public RefCounted {
	ORBLIT_CLASS(UDPServer, RefCounted);

protected:
	enum {
		PACKET_BUFFER_SIZE = 65536
	};

	struct Peer {
		// Weakly owned in peers, strongly owned in pending.
		// Peers are weakly owned such that it can self-destruct (and thus unregister itself)
		// when the user no longer needs it.
		PacketPeerUDP *peer;
		IPAddress ip;
		uint16_t port = 0;

		bool operator==(const Peer &p_other) const {
			return (ip == p_other.ip && port == p_other.port);
		}
	};
	uint8_t recv_buffer[PACKET_BUFFER_SIZE];

	List<Peer> peers;
	List<Peer> pending;
	int max_pending_connections = 16;

	Ref<NetSocket> _sock;
	static void register_members();

public:
	void remove_peer(IPAddress p_ip, int p_port);
	Error listen(uint16_t p_port, const IPAddress &p_bind_address = IPAddress("*"));
	Error poll();
	int get_local_port() const;
	bool is_listening() const;
	bool is_connection_available() const;
	void set_max_pending_connections(int p_max);
	int get_max_pending_connections() const;
	Ref<PacketPeerUDP> take_connection();

	void stop();

	UDPServer();
	~UDPServer();
};
