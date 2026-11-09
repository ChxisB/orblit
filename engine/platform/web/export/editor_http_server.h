/**************************************************************************/
/*  editor_http_server.h                                                  */

#pragma once

#include "core/io/stream_peer_tls.h"
#include "core/io/tcp_server.h"

class EditorHTTPServer : public RefCounted {
	ORBLIT_SOFTCLASS(EditorHTTPServer, RefCounted);

private:
	Ref<TCPServer> server;
	HashMap<String, String> mimes;
	Ref<StreamPeerTCP> tcp;
	Ref<StreamPeerTLS> tls;
	Ref<StreamPeer> peer;
	Ref<CryptoKey> key;
	Ref<X509Certificate> cert;
	bool use_tls = false;
	uint64_t time = 0;
	uint8_t req_buf[4096];
	int req_pos = 0;

	SafeFlag server_quit;
	Mutex server_lock;
	Thread server_thread;

	void _clear_client();
	void _set_internal_certs(Ref<Crypto> p_crypto);
	void _send_response();
	void _poll();

	static void _server_thread_poll(void *data);

public:
	EditorHTTPServer();
	~EditorHTTPServer();

	void stop();
	Error listen(int p_port, IPAddress p_address, bool p_use_tls, String p_tls_key, String p_tls_cert);
	bool is_listening() const;
};
