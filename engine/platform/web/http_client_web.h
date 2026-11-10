/**************************************************************************/
/*  http_client_web.h                                                     */

#pragma once

#include "core/io/http_client.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef enum {
	ORBLIT_JS_FETCH_STATE_REQUESTING = 0,
	ORBLIT_JS_FETCH_STATE_BODY = 1,
	ORBLIT_JS_FETCH_STATE_DONE = 2,
	ORBLIT_JS_FETCH_STATE_ERROR = -1,
} orblit_js_fetch_state_t;

extern int orblit_js_fetch_create(const char *p_method, const char *p_url, const char **p_headers, int p_headers_len, const uint8_t *p_body, int p_body_len);
extern int orblit_js_fetch_read_headers(int p_id, void (*parse_callback)(int p_size, const char **p_headers, void *p_ref), void *p_ref);
extern int orblit_js_fetch_read_chunk(int p_id, uint8_t *p_buf, int p_buf_size);
extern void orblit_js_fetch_free(int p_id);
extern orblit_js_fetch_state_t orblit_js_fetch_state_get(int p_id);
extern int orblit_js_fetch_http_status_get(int p_id);
extern int orblit_js_fetch_is_chunked(int p_id);

#ifdef __cplusplus
}
#endif

class HTTPClientWeb : public HTTPClient {
	ORBLIT_SOFTCLASS(HTTPClientWeb, HTTPClient);

private:
	int js_id = 0;
	Status status = STATUS_DISCONNECTED;

	// 64 KiB by default (favors fast download speeds at the cost of memory usage).
	int read_limit = 65536;

	String host;
	int port = -1;
	bool use_tls = false;

	int polled_response_code = 0;
	Vector<String> response_headers;
	Vector<uint8_t> response_buffer;

#ifdef DEBUG_ENABLED
	uint64_t last_polling_frame = 0;
#endif

	static void _parse_headers(int p_len, const char **p_headers, void *p_ref);

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
	HTTPClientWeb();
	~HTTPClientWeb();
};
