/**************************************************************************/
/*  editor_debugger_server_websocket.h                                    */

#pragma once

#include "../websocket_peer.h"

#include "core/io/tcp_server.h"
#include "editor/debugger/editor_debugger_server.h"

class EditorDebuggerServerWebSocket : public EditorDebuggerServer {
	ORBLIT_CLASS(EditorDebuggerServerWebSocket, EditorDebuggerServer);

private:
	Ref<TCPServer> tcp_server;
	Ref<WebSocketPeer> pending_peer;
	uint64_t pending_timer = 0;
	String endpoint;

public:
	static Ref<EditorDebuggerServer> create(const String &p_protocol);

	void _peer_connected(int p_peer, const String &p_protocol);
	void _peer_disconnected(int p_peer, bool p_was_clean);

	virtual void poll() override;
	virtual String get_uri() const override;
	virtual Error start(const String &p_uri = "") override;
	virtual void stop() override;
	virtual bool is_active() const override;
	virtual bool is_connection_available() const override;
	virtual Ref<RemoteDebuggerPeer> take_connection() override;

	EditorDebuggerServerWebSocket();
	~EditorDebuggerServerWebSocket();
};
