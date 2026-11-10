/**************************************************************************/
/*  webrtc_peer_connection_js.cpp                                         */

#include "webrtc_peer_connection_js.h"

#ifdef WEB_ENABLED

#include "webrtc_data_channel_js.h"

#include <emscripten.h>

void WebRTCPeerConnectionJS::_on_ice_candidate(void *p_obj, const char *p_mid_name, int p_mline_idx, const char *p_candidate) {
	WebRTCPeerConnectionJS *peer = static_cast<WebRTCPeerConnectionJS *>(p_obj);
	peer->emit_signal(SNAME("ice_candidate_created"), String(p_mid_name), p_mline_idx, String(p_candidate));
}

void WebRTCPeerConnectionJS::_on_session_created(void *p_obj, const char *p_type, const char *p_session) {
	WebRTCPeerConnectionJS *peer = static_cast<WebRTCPeerConnectionJS *>(p_obj);
	peer->emit_signal(SNAME("session_description_created"), String(p_type), String(p_session));
}

void WebRTCPeerConnectionJS::_on_connection_state_changed(void *p_obj, int p_state) {
	WebRTCPeerConnectionJS *peer = static_cast<WebRTCPeerConnectionJS *>(p_obj);
	peer->_conn_state = (ConnectionState)p_state;
}

void WebRTCPeerConnectionJS::_on_gathering_state_changed(void *p_obj, int p_state) {
	WebRTCPeerConnectionJS *peer = static_cast<WebRTCPeerConnectionJS *>(p_obj);
	peer->_gathering_state = (GatheringState)p_state;
}

void WebRTCPeerConnectionJS::_on_signaling_state_changed(void *p_obj, int p_state) {
	WebRTCPeerConnectionJS *peer = static_cast<WebRTCPeerConnectionJS *>(p_obj);
	peer->_signaling_state = (SignalingState)p_state;
}

void WebRTCPeerConnectionJS::_on_error(void *p_obj) {
	ERR_PRINT("RTCPeerConnection error!");
}

void WebRTCPeerConnectionJS::_on_data_channel(void *p_obj, int p_id) {
	WebRTCPeerConnectionJS *peer = static_cast<WebRTCPeerConnectionJS *>(p_obj);
	peer->emit_signal(SNAME("data_channel_received"), Ref<WebRTCDataChannel>(memnew(WebRTCDataChannelJS(p_id))));
}

void WebRTCPeerConnectionJS::close() {
	orblit_js_rtc_pc_close(_js_id);
	_conn_state = STATE_CLOSED;
}

Error WebRTCPeerConnectionJS::create_offer() {
	ERR_FAIL_COND_V(_conn_state != STATE_NEW, FAILED);

	_conn_state = STATE_CONNECTING;
	orblit_js_rtc_pc_offer_create(_js_id, this, &_on_session_created, &_on_error);
	return OK;
}

Error WebRTCPeerConnectionJS::set_local_description(const String &p_type, const String &p_sdp) {
	orblit_js_rtc_pc_local_description_set(_js_id, p_type.utf8().get_data(), p_sdp.utf8().get_data(), this, &_on_error);
	return OK;
}

Error WebRTCPeerConnectionJS::set_remote_description(const String &p_type, const String &p_sdp) {
	if (p_type == "offer") {
		ERR_FAIL_COND_V(_conn_state != STATE_NEW, FAILED);
		_conn_state = STATE_CONNECTING;
	}
	orblit_js_rtc_pc_remote_description_set(_js_id, p_type.utf8().get_data(), p_sdp.utf8().get_data(), this, &_on_session_created, &_on_error);
	return OK;
}

Error WebRTCPeerConnectionJS::add_ice_candidate(const String &p_sdp_mid_name, int p_sdp_mline_index_name, const String &p_sdp_name) {
	orblit_js_rtc_pc_ice_candidate_add(_js_id, p_sdp_mid_name.utf8().get_data(), p_sdp_mline_index_name, p_sdp_name.utf8().get_data());
	return OK;
}

Error WebRTCPeerConnectionJS::initialize(const Dictionary &p_config) {
	if (_js_id) {
		orblit_js_rtc_pc_destroy(_js_id);
		_js_id = 0;
	}
	_conn_state = STATE_NEW;

	String config = Variant(p_config).to_json_string();
	_js_id = orblit_js_rtc_pc_create(config.utf8().get_data(), this, &_on_connection_state_changed, &_on_gathering_state_changed, &_on_signaling_state_changed, &_on_ice_candidate, &_on_data_channel);
	return _js_id ? OK : FAILED;
}

Ref<WebRTCDataChannel> WebRTCPeerConnectionJS::create_data_channel(const String &p_channel, const Dictionary &p_channel_config) {
	ERR_FAIL_COND_V(_conn_state != STATE_NEW, nullptr);

	String config = Variant(p_channel_config).to_json_string();
	int id = orblit_js_rtc_pc_datachannel_create(_js_id, p_channel.utf8().get_data(), config.utf8().get_data());
	ERR_FAIL_COND_V(id == 0, nullptr);
	return memnew(WebRTCDataChannelJS(id));
}

Error WebRTCPeerConnectionJS::poll() {
	return OK;
}

WebRTCPeerConnection::GatheringState WebRTCPeerConnectionJS::get_gathering_state() const {
	return _gathering_state;
}

WebRTCPeerConnection::SignalingState WebRTCPeerConnectionJS::get_signaling_state() const {
	return _signaling_state;
}

WebRTCPeerConnection::ConnectionState WebRTCPeerConnectionJS::get_connection_state() const {
	return _conn_state;
}

WebRTCPeerConnectionJS::WebRTCPeerConnectionJS() {
	Dictionary config;
	initialize(config);
}

WebRTCPeerConnectionJS::~WebRTCPeerConnectionJS() {
	close();
	if (_js_id) {
		orblit_js_rtc_pc_destroy(_js_id);
		_js_id = 0;
	}
}
#endif
