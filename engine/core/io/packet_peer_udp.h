/**************************************************************************/
/*  packet_peer_udp.h                                                     */

#pragma once

#include "core/io/ip_address.h"
#include "core/io/net_socket.h"
#include "core/io/packet_peer.h"

class UDPServer;

class PacketPeerUDP : public PacketPeer {
	ORBLIT_CLASS(PacketPeerUDP, PacketPeer);

protected:
	enum {
		PACKET_BUFFER_SIZE = 65536
	};

	RingBuffer<uint8_t> rb;
	uint8_t recv_buffer[PACKET_BUFFER_SIZE];
	uint8_t packet_buffer[PACKET_BUFFER_SIZE];
	IPAddress packet_ip;
	int packet_port = 0;
	int queue_count = 0;

	IPAddress peer_addr;
	int peer_port = 0;
	bool connected = false;
	bool blocking = true;
	bool broadcast = false;
	UDPServer *udp_server = nullptr;
	Ref<NetSocket> _sock;

	static void register_members();

	String _get_packet_ip() const;

	Error _set_dest_address(const String &p_address, int p_port);
	Error _poll();

public:
	void set_blocking_mode(bool p_enable);

	Error bind(int p_port, const IPAddress &p_bind_address = IPAddress("*"), int p_recv_buffer_size = 65536);
	void close();
	Error wait();
	bool is_bound() const;

	Error connect_shared_socket(Ref<NetSocket> p_sock, IPAddress p_ip, uint16_t p_port, UDPServer *p_server); // Used by UDPServer
	void disconnect_shared_socket(); // Used by UDPServer
	Error store_packet(IPAddress p_ip, uint32_t p_port, uint8_t *p_buf, int p_buf_size); // Used internally and by UDPServer
	Error connect_to_host(const IPAddress &p_host, int p_port);
	bool is_socket_connected() const;

	IPAddress get_packet_address() const;
	int get_packet_port() const;
	int get_local_port() const;
	void set_dest_address(const IPAddress &p_address, int p_port);

	Error put_packet(const uint8_t *p_buffer, int p_buffer_size) override;
	Error get_packet(const uint8_t **r_buffer, int &r_buffer_size) override;
	int get_available_packet_count() const override;
	int get_max_packet_size() const override;
	void set_broadcast_enabled(bool p_enabled);
	Error join_multicast_group(IPAddress p_multi_address, const String &p_if_name);
	Error leave_multicast_group(IPAddress p_multi_address, const String &p_if_name);

	PacketPeerUDP();
	~PacketPeerUDP();
};
