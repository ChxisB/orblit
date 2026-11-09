/**************************************************************************/
/*  http_client_tcp.h                                                     */

#pragma once

#include "core/crypto/crypto.h"
#include "core/io/http_client.h"
#include "core/io/ip.h"

class StreamPeerTCP;

class HTTPClientTCP : public HTTPClient {
	ORBLIT_SOFTCLASS(HTTPClientTCP, HTTPClient);

private:
	Status status = STATUS_DISCONNECTED;
	IP::ResolverID resolving = IP::RESOLVER_INVALID_ID;
	Array ip_candidates;
	int conn_port = -1; // Server to make requests to.
	String conn_host;
	int server_port = -1; // Server to connect to (might be a proxy server).
	String server_host;
	int http_proxy_port = -1; // Proxy server for http requests.
	String http_proxy_host;
	int https_proxy_port = -1; // Proxy server for https requests.
	String https_proxy_host;
	bool blocking = false;
	bool handshaking = false;
	bool head_request = false;
	Ref<TLSOptions> tls_options;

	Vector<uint8_t> response_str;

	bool chunked = false;
	Vector<uint8_t> chunk;
	int chunk_left = 0;
	bool chunk_trailer_part = false;
	int64_t body_size = -1;
	int64_t body_left = 0;
	bool read_until_eof = false;

	Ref<StreamPeerBuffer> request_buffer;
	Ref<StreamPeerTCP> tcp_connection;
	Ref<StreamPeer> connection;
	Ref<HTTPClientTCP> proxy_client; // Negotiate with proxy server.

	int response_num = 0;
	Vector<String> response_headers;
	// 64 KiB by default (favors fast download speeds at the cost of memory usage).
	int read_chunk_size = 65536;

	Error _get_http_data(uint8_t *p_buffer, int p_bytes, int &r_received);

public:
	static HTTPClient *_create_func(bool p_notify_postinitialize);

	Error request(Method p_method, const String &p_url, const Vector<String> &p_headers, const uint8_t *p_body, int p_body_size) override;

	Error connect_to_host(const String &p_host, int p_port = -1, Ref<TLSOptions> p_tls_options = Ref<TLSOptions>()) override;
	void set_connection(const Ref<StreamPeer> &p_connection) override;
	Ref<StreamPeer> get_connection() const override;
	void close() override;
	Status get_status() const override;
	bool has_response() const override;
	bool is_response_chunked() const override;
	int get_response_code() const override;
	Error get_response_headers(List<String> *r_response) override;
	int64_t get_response_body_length() const override;
	PackedByteArray read_response_body_chunk() override;
	void set_blocking_mode(bool p_enable) override;
	bool is_blocking_mode_enabled() const override;
	void set_read_chunk_size(int p_size) override;
	int get_read_chunk_size() const override;
	Error poll() override;
	void set_http_proxy(const String &p_host, int p_port) override;
	void set_https_proxy(const String &p_host, int p_port) override;
	HTTPClientTCP();
};
