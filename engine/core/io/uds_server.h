/**************************************************************************/
/*  uds_server.h                                                          */

#pragma once

#include "core/io/socket_server.h"
#include "core/io/stream_peer_uds.h"

class UDSServer : public SocketServer {
	ORBLIT_CLASS(UDSServer, SocketServer);

protected:
	static void register_members();

public:
	Error listen(const String &p_path);
	Ref<StreamPeerUDS> take_connection();
	Ref<StreamPeerSocket> take_socket_connection() override { return take_connection(); }
};
