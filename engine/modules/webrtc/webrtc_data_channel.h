/**************************************************************************/
/*  webrtc_data_channel.h                                                 */

#pragma once

#include "core/io/packet_peer.h"

class WebRTCDataChannel : public PacketPeer {
	ORBLIT_CLASS(WebRTCDataChannel, PacketPeer);

public:
	enum WriteMode {
		WRITE_MODE_TEXT,
		WRITE_MODE_BINARY,
	};

	enum ChannelState {
		STATE_CONNECTING,
		STATE_OPEN,
		STATE_CLOSING,
		STATE_CLOSED
	};

protected:
	unsigned int _in_buffer_shift;

	static void register_members();

public:
	virtual void set_write_mode(WriteMode mode) = 0;
	virtual WriteMode get_write_mode() const = 0;
	virtual bool was_string_packet() const = 0;

	virtual ChannelState get_ready_state() const = 0;
	virtual String get_label() const = 0;
	virtual bool is_ordered() const = 0;
	virtual int get_id() const = 0;
	virtual int get_max_packet_life_time() const = 0;
	virtual int get_max_retransmits() const = 0;
	virtual String get_protocol() const = 0;
	virtual bool is_negotiated() const = 0;

	virtual int get_buffered_amount() const = 0;

	virtual Error poll() = 0;
	virtual void close() = 0;

	WebRTCDataChannel();
	~WebRTCDataChannel();
};

VARIANT_ENUM_CAST(WebRTCDataChannel::WriteMode);
VARIANT_ENUM_CAST(WebRTCDataChannel::ChannelState);
