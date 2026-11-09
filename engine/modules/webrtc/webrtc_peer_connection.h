/**************************************************************************/
/*  webrtc_peer_connection.h                                              */

#pragma once

#include "webrtc_data_channel.h"

class WebRTCPeerConnection : public RefCounted {
	ORBLIT_CLASS(WebRTCPeerConnection, RefCounted);

public:
	enum ConnectionState {
		STATE_NEW,
		STATE_CONNECTING,
		STATE_CONNECTED,
		STATE_DISCONNECTED,
		STATE_FAILED,
		STATE_CLOSED
	};

	enum GatheringState {
		GATHERING_STATE_NEW,
		GATHERING_STATE_GATHERING,
		GATHERING_STATE_COMPLETE,
	};

	enum SignalingState {
		SIGNALING_STATE_STABLE,
		SIGNALING_STATE_HAVE_LOCAL_OFFER,
		SIGNALING_STATE_HAVE_REMOTE_OFFER,
		SIGNALING_STATE_HAVE_LOCAL_PRANSWER,
		SIGNALING_STATE_HAVE_REMOTE_PRANSWER,
		SIGNALING_STATE_CLOSED,
	};

private:
	static StringName default_extension;

protected:
	static void register_members();

public:
	static void set_default_extension(const StringName &p_name);

	virtual ConnectionState get_connection_state() const = 0;
	virtual GatheringState get_gathering_state() const = 0;
	virtual SignalingState get_signaling_state() const = 0;

	virtual Error initialize(const Dictionary &p_config = Dictionary()) = 0;
	virtual Ref<WebRTCDataChannel> create_data_channel(const String &p_label, const Dictionary &p_options = Dictionary()) = 0;
	virtual Error create_offer() = 0;
	virtual Error set_remote_description(const String &p_type, const String &p_sdp) = 0;
	virtual Error set_local_description(const String &p_type, const String &p_sdp) = 0;
	virtual Error add_ice_candidate(const String &p_sdp_mid_name, int p_sdp_mline_index_name, const String &p_sdp_name) = 0;
	virtual Error poll() = 0;
	virtual void close() = 0;

	static WebRTCPeerConnection *create(bool p_notify_postinitialize = true);

	WebRTCPeerConnection();
	~WebRTCPeerConnection();
};

VARIANT_ENUM_CAST(WebRTCPeerConnection::ConnectionState);
VARIANT_ENUM_CAST(WebRTCPeerConnection::GatheringState);
VARIANT_ENUM_CAST(WebRTCPeerConnection::SignalingState);
