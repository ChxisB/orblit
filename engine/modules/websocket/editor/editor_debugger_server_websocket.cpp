/**************************************************************************/
/*  editor_debugger_server_websocket.cpp                                  */

#include "editor_debugger_server_websocket.h"

#include "../remote_debugger_peer_websocket.h"

#include "core/os/os.h"
#include "editor/editor_log.h"
#include "editor/editor_node.h"
#include "editor/settings/editor_settings.h"

void EditorDebuggerServerWebSocket::poll() {
	if (pending_peer.is_null() && tcp_server->is_connection_available()) {
		Ref<WebSocketPeer> peer = Ref<WebSocketPeer>(WebSocketPeer::create());
		ERR_FAIL_COND(peer.is_null()); // Bug.

		Vector<String> ws_protocols;
		ws_protocols.push_back("binary"); // Compatibility for emscripten TCP-to-WebSocket.
		peer->set_supported_protocols(ws_protocols);

		Error err = peer->accept_stream(tcp_server->take_connection());
		if (err == OK) {
			pending_timer = OS::get_singleton()->get_ticks_msec();
			pending_peer = peer;
		}
	}
	if (pending_peer.is_valid() && pending_peer->get_ready_state() != WebSocketPeer::STATE_OPEN) {
		pending_peer->poll();
		WebSocketPeer::State ready_state = pending_peer->get_ready_state();
		if (ready_state != WebSocketPeer::STATE_CONNECTING && ready_state != WebSocketPeer::STATE_OPEN) {
			pending_peer.unref(); // Failed.
		}
		if (ready_state == WebSocketPeer::STATE_CONNECTING && OS::get_singleton()->get_ticks_msec() - pending_timer > 3000) {
			pending_peer.unref(); // Timeout.
		}
	}
}

String EditorDebuggerServerWebSocket::get_uri() const {
	return endpoint;
}

Error EditorDebuggerServerWebSocket::start(const String &p_uri) {
	// Default host and port
	String bind_host = (String)EDITOR_GET("network/debug/remote_host");
	int bind_port = (int)EDITOR_GET("network/debug/remote_port");

	// Optionally override
	if (!p_uri.is_empty() && p_uri != "ws://") {
		String scheme, path, fragment;
		Error err = p_uri.parse_url(scheme, bind_host, bind_port, path, fragment);
		ERR_FAIL_COND_V(err != OK, ERR_INVALID_PARAMETER);
		ERR_FAIL_COND_V(!bind_host.is_valid_ip_address() && bind_host != "*", ERR_INVALID_PARAMETER);
	}

	// Try listening on ports
	const int max_attempts = 5;
	for (int attempt = 1;; ++attempt) {
		const Error err = tcp_server->listen(bind_port, bind_host);
		if (err == OK) {
			break;
		}
		if (attempt >= max_attempts) {
			EditorNode::get_log()->add_message(vformat("Cannot listen on port %d, remote debugging unavailable.", bind_port), EditorLog::MSG_TYPE_ERROR);
			return err;
		}
		int last_port = bind_port++;
		EditorNode::get_log()->add_message(vformat("Cannot listen on port %d, trying %d instead.", last_port, bind_port), EditorLog::MSG_TYPE_WARNING);
	}

	// Endpoint that the client should connect to
	endpoint = vformat("ws://%s:%d", bind_host, bind_port);

	return OK;
}

void EditorDebuggerServerWebSocket::stop() {
	pending_peer.unref();
	tcp_server->stop();
}

bool EditorDebuggerServerWebSocket::is_active() const {
	return tcp_server->is_listening();
}

bool EditorDebuggerServerWebSocket::is_connection_available() const {
	return pending_peer.is_valid() && pending_peer->get_ready_state() == WebSocketPeer::STATE_OPEN;
}

Ref<RemoteDebuggerPeer> EditorDebuggerServerWebSocket::take_connection() {
	ERR_FAIL_COND_V(!is_connection_available(), Ref<RemoteDebuggerPeer>());
	Ref<RemoteDebuggerPeer> peer = memnew(RemoteDebuggerPeerWebSocket(pending_peer));
	pending_peer.unref();
	return peer;
}

EditorDebuggerServerWebSocket::EditorDebuggerServerWebSocket() {
	tcp_server.instantiate();
}

EditorDebuggerServerWebSocket::~EditorDebuggerServerWebSocket() {
	stop();
}

Ref<EditorDebuggerServer> EditorDebuggerServerWebSocket::create(const String &p_protocol) {
	ERR_FAIL_COND_V(p_protocol != "ws://", nullptr);
	return memnew(EditorDebuggerServerWebSocket);
}
