/**************************************************************************/
/*  remote_debugger_peer_websocket.h                                      */

#pragma once

#include "websocket_peer.h"

#include "core/debugger/remote_debugger_peer.h"

class RemoteDebuggerPeerWebSocket : public RemoteDebuggerPeer {
	ORBLIT_SOFTCLASS(RemoteDebuggerPeerWebSocket, RemoteDebuggerPeer);

	Ref<WebSocketPeer> ws_peer;
	List<Array> in_queue;
	List<Array> out_queue;

	int max_queued_messages;

public:
	static Ref<RemoteDebuggerPeer> create(const String &p_uri);

	Error connect_to_host(const String &p_uri);

	bool is_peer_connected() override;
	int get_max_message_size() const override;
	bool has_message() override;
	Error put_message(const Array &p_arr) override;
	Array get_message() override;
	void close() override;
	void poll() override;
	bool can_block() const override;

	RemoteDebuggerPeerWebSocket(const Ref<WebSocketPeer> &p_peer = Ref<WebSocketPeer>());
};
