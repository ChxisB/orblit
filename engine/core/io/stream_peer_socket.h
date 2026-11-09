/**************************************************************************/
/*  stream_peer_socket.h                                                  */

#pragma once

#include "core/io/net_socket.h"
#include "core/io/stream_peer.h"

class StreamPeerSocket : public StreamPeer {
	ORBLIT_CLASS(StreamPeerSocket, StreamPeer);

public:
	enum Status {
		STATUS_NONE,
		STATUS_CONNECTING,
		STATUS_CONNECTED,
		STATUS_ERROR,
	};

protected:

	Ref<NetSocket> _sock;
	uint64_t timeout = 0;
	Status status = STATUS_NONE;
	NetSocket::Address peer_address;

	Error write(const uint8_t *p_data, int p_bytes, int &r_sent, bool p_block);
	Error read(uint8_t *p_buffer, int p_bytes, int &r_received, bool p_block);

	static void register_members();

public:
	virtual void accept_socket(Ref<NetSocket> p_sock, const NetSocket::Address &p_addr) = 0;

	void disconnect_from_host();

	int get_available_bytes() const override;
	Status get_status() const;

	// Poll socket updating its state.
	Error poll();

	// Wait or check for writable, readable.
	Error wait(NetSocket::PollType p_type, int p_timeout = 0);

	// Read/Write from StreamPeer
	Error put_data(const uint8_t *p_data, int p_bytes) override;
	Error put_partial_data(const uint8_t *p_data, int p_bytes, int &r_sent) override;
	Error get_data(uint8_t *p_buffer, int p_bytes) override;
	Error get_partial_data(uint8_t *p_buffer, int p_bytes, int &r_received) override;

	StreamPeerSocket();
	virtual ~StreamPeerSocket();
};

VARIANT_ENUM_CAST(StreamPeerSocket::Status);
