/**************************************************************************/
/*  stream_peer_uds.h                                                     */

#pragma once

#include "core/io/stream_peer_socket.h"

class StreamPeerUDS : public StreamPeerSocket {
	ORBLIT_CLASS(StreamPeerUDS, StreamPeerSocket);

protected:
	String peer_path;
	static void register_members();

public:
	void accept_socket(Ref<NetSocket> p_sock, const NetSocket::Address &p_addr) override;

	Error bind(const String &p_path);
	Error connect_to_host(const String &p_path);
	const String get_connected_path() const;
};
