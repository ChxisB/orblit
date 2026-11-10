/**************************************************************************/
/*  websocket_peer.h                                                      */

#pragma once

#include "core/crypto/crypto.h"
#include "core/io/packet_peer.h"

class WebSocketPeer : public PacketPeer {
	ORBLIT_CLASS(WebSocketPeer, PacketPeer);

public:
	enum State {
		STATE_CONNECTING,
		STATE_OPEN,
		STATE_CLOSING,
		STATE_CLOSED
	};

	enum WriteMode {
		WRITE_MODE_TEXT,
		WRITE_MODE_BINARY,
	};

	enum {
		DEFAULT_BUFFER_SIZE = 65535,
	};

private:
	virtual Error _send_bind(const PackedByteArray &p_data, WriteMode p_mode = WRITE_MODE_BINARY);

protected:
	static WebSocketPeer *(*_create)(bool p_notify_postinitialize);

	static void register_members();

	Vector<String> supported_protocols;
	Vector<String> handshake_headers;

	Vector<String> _get_supported_protocols() const;
	Vector<String> _get_handshake_headers() const;

	int outbound_buffer_size = DEFAULT_BUFFER_SIZE;
	int inbound_buffer_size = DEFAULT_BUFFER_SIZE;
	int max_queued_packets = 4096;
	uint64_t heartbeat_interval_msec = 0;

public:
	static WebSocketPeer *create(bool p_notify_postinitialize = true) {
		if (!_create) {
			return nullptr;
		}
		return _create(p_notify_postinitialize);
	}

	virtual Error connect_to_url(const String &p_url, const Ref<TLSOptions> &p_options = Ref<TLSOptions>()) = 0;
	virtual Error accept_stream(const Ref<StreamPeer> &p_stream) = 0;

	virtual Error send(const uint8_t *p_buffer, int p_buffer_size, WriteMode p_mode) = 0;
	virtual void close(int p_code = 1000, const String &p_reason = "") = 0;

	virtual IPAddress get_connected_host() const = 0;
	virtual uint16_t get_connected_port() const = 0;
	virtual bool was_string_packet() const = 0;
	virtual void set_no_delay(bool p_enabled) = 0;
	virtual int get_current_outbound_buffered_amount() const = 0;
	virtual String get_selected_protocol() const = 0;
	virtual String get_requested_url() const = 0;

	virtual void poll() = 0;
	virtual State get_ready_state() const = 0;
	virtual int get_close_code() const = 0;
	virtual String get_close_reason() const = 0;

	Error send_text(const String &p_text);

	void set_supported_protocols(const Vector<String> &p_protocols);
	const Vector<String> get_supported_protocols() const;

	void set_handshake_headers(const Vector<String> &p_headers);
	const Vector<String> get_handshake_headers() const;

	void set_outbound_buffer_size(int p_buffer_size);
	int get_outbound_buffer_size() const;

	void set_inbound_buffer_size(int p_buffer_size);
	int get_inbound_buffer_size() const;

	void set_max_queued_packets(int p_max_queued_packets);
	int get_max_queued_packets() const;

	double get_heartbeat_interval() const;
	void set_heartbeat_interval(double p_interval);

	WebSocketPeer();
	~WebSocketPeer();
};

VARIANT_ENUM_CAST(WebSocketPeer::WriteMode);
VARIANT_ENUM_CAST(WebSocketPeer::State);
