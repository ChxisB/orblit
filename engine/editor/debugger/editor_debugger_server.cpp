/**************************************************************************/
/*  editor_debugger_server.cpp                                            */

#include "editor_debugger_server.h"

#include "core/io/tcp_server.h"
#include "core/io/uds_server.h"
#include "core/os/thread.h"
#include "editor/editor_log.h"
#include "editor/editor_node.h"
#include "editor/settings/editor_settings.h"

template <typename T>
class EditorDebuggerServerSocket : public EditorDebuggerServer {
	ORBLIT_SOFTCLASS(EditorDebuggerServerSocket, EditorDebuggerServer);

protected:
	Ref<T> server;
	String endpoint;

public:
	virtual void poll() override {}
	virtual String get_uri() const override;
	virtual void stop() override;
	virtual bool is_active() const override;
	virtual bool is_connection_available() const override;
	virtual Ref<RemoteDebuggerPeer> take_connection() override;

	EditorDebuggerServerSocket();
};

class EditorDebuggerServerTCP : public EditorDebuggerServerSocket<TCPServer> {
public:
	static Ref<EditorDebuggerServer> create(const String &p_protocol);

	virtual Error start(const String &p_uri) override;
};

Ref<EditorDebuggerServer> EditorDebuggerServerTCP::create(const String &p_protocol) {
	ERR_FAIL_COND_V(p_protocol != "tcp://", nullptr);
	return memnew(EditorDebuggerServerTCP);
}

template <typename T>
EditorDebuggerServerSocket<T>::EditorDebuggerServerSocket() {
	server.instantiate();
}

template <typename T>
String EditorDebuggerServerSocket<T>::get_uri() const {
	return endpoint;
}

Error EditorDebuggerServerTCP::start(const String &p_uri) {
	// Default host and port
	String bind_host = (String)EDITOR_GET("network/debug/remote_host");
	int bind_port = (int)EDITOR_GET("network/debug/remote_port");

	// Optionally override
	if (!p_uri.is_empty() && p_uri != "tcp://") {
		String scheme, path, fragment;
		Error err = p_uri.parse_url(scheme, bind_host, bind_port, path, fragment);
		ERR_FAIL_COND_V(err != OK, ERR_INVALID_PARAMETER);
		ERR_FAIL_COND_V(!bind_host.is_valid_ip_address() && bind_host != "*", ERR_INVALID_PARAMETER);
	}

	// Try listening on ports
	const int max_attempts = 5;
	for (int attempt = 1;; ++attempt) {
		const Error err = server->listen(bind_port, bind_host);
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
	endpoint = vformat("tcp://%s:%d", bind_host, bind_port);

	return OK;
}

template <typename T>
void EditorDebuggerServerSocket<T>::stop() {
	server->stop();
}

template <typename T>
bool EditorDebuggerServerSocket<T>::is_active() const {
	return server->is_listening();
}

template <typename T>
bool EditorDebuggerServerSocket<T>::is_connection_available() const {
	return server->is_listening() && server->is_connection_available();
}

template <typename T>
Ref<RemoteDebuggerPeer> EditorDebuggerServerSocket<T>::take_connection() {
	const Ref<RemoteDebuggerPeer> out;
	ERR_FAIL_COND_V(!is_connection_available(), out);
	Ref<StreamPeerSocket> stream = server->take_socket_connection();
	ERR_FAIL_COND_V(stream.is_null(), out);
	return memnew(RemoteDebuggerPeerTCP(stream));
}

class EditorDebuggerServerUDS : public EditorDebuggerServerSocket<UDSServer> {
public:
	static Ref<EditorDebuggerServer> create(const String &p_protocol);

	virtual Error start(const String &p_uri) override;
};

Ref<EditorDebuggerServer> EditorDebuggerServerUDS::create(const String &p_protocol) {
	ERR_FAIL_COND_V(p_protocol != "unix://", nullptr);
	return memnew(EditorDebuggerServerUDS);
}

Error EditorDebuggerServerUDS::start(const String &p_uri) {
	String bind_path = p_uri.is_empty() ? String("/tmp/orblit_debugger.sock") : p_uri.replace("unix://", "");

	const Error err = server->listen(bind_path);
	if (err != OK) {
		EditorNode::get_log()->add_message(vformat("Cannot listen at path %s, remote debugging unavailable.", bind_path), EditorLog::MSG_TYPE_ERROR);
		return err;
	}
	endpoint = "unix://" + bind_path;
	return OK;
}

/// EditorDebuggerServer
HashMap<StringName, EditorDebuggerServer::CreateServerFunc> EditorDebuggerServer::protocols;

Ref<EditorDebuggerServer> EditorDebuggerServer::create(const String &p_protocol) {
	CreateServerFunc *create_fn = protocols.getptr(p_protocol);
	ERR_FAIL_NULL_V(create_fn, nullptr);
	return (*create_fn)(p_protocol);
}

void EditorDebuggerServer::register_protocol_handler(const String &p_protocol, CreateServerFunc p_func) {
	ERR_FAIL_COND(protocols.has(p_protocol));
	protocols[p_protocol] = p_func;
}

void EditorDebuggerServer::initialize() {
	register_protocol_handler("tcp://", EditorDebuggerServerTCP::create);
#if defined(UNIX_ENABLED)
	register_protocol_handler("unix://", EditorDebuggerServerUDS::create);
#endif
}

void EditorDebuggerServer::deinitialize() {
	protocols.clear();
}
