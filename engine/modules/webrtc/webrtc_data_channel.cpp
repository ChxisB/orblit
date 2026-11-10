/**************************************************************************/
/*  webrtc_data_channel.cpp                                               */

#include "webrtc_data_channel.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"

void WebRTCDataChannel::register_members() {
	ClassRegistry::bind_method(D_METHOD("poll"), &WebRTCDataChannel::poll);
	ClassRegistry::bind_method(D_METHOD("close"), &WebRTCDataChannel::close);

	ClassRegistry::bind_method(D_METHOD("was_string_packet"), &WebRTCDataChannel::was_string_packet);
	ClassRegistry::bind_method(D_METHOD("set_write_mode", "write_mode"), &WebRTCDataChannel::set_write_mode);
	ClassRegistry::bind_method(D_METHOD("get_write_mode"), &WebRTCDataChannel::get_write_mode);
	ClassRegistry::bind_method(D_METHOD("get_ready_state"), &WebRTCDataChannel::get_ready_state);
	ClassRegistry::bind_method(D_METHOD("get_label"), &WebRTCDataChannel::get_label);
	ClassRegistry::bind_method(D_METHOD("is_ordered"), &WebRTCDataChannel::is_ordered);
	ClassRegistry::bind_method(D_METHOD("get_id"), &WebRTCDataChannel::get_id);
	ClassRegistry::bind_method(D_METHOD("get_max_packet_life_time"), &WebRTCDataChannel::get_max_packet_life_time);
	ClassRegistry::bind_method(D_METHOD("get_max_retransmits"), &WebRTCDataChannel::get_max_retransmits);
	ClassRegistry::bind_method(D_METHOD("get_protocol"), &WebRTCDataChannel::get_protocol);
	ClassRegistry::bind_method(D_METHOD("is_negotiated"), &WebRTCDataChannel::is_negotiated);
	ClassRegistry::bind_method(D_METHOD("get_buffered_amount"), &WebRTCDataChannel::get_buffered_amount);

	ADD_PROPERTY(PropertyInfo(Variant::INT, "write_mode", PROPERTY_HINT_ENUM), "set_write_mode", "get_write_mode");

	BIND_ENUM_CONSTANT(WRITE_MODE_TEXT);
	BIND_ENUM_CONSTANT(WRITE_MODE_BINARY);

	BIND_ENUM_CONSTANT(STATE_CONNECTING);
	BIND_ENUM_CONSTANT(STATE_OPEN);
	BIND_ENUM_CONSTANT(STATE_CLOSING);
	BIND_ENUM_CONSTANT(STATE_CLOSED);
}

WebRTCDataChannel::WebRTCDataChannel() {
	_in_buffer_shift = Math::nearest_shift(uint32_t((int)GLOBAL_GET("network/limits/webrtc/max_channel_in_buffer_kb") - 1)) + (uint32_t)10;
}

WebRTCDataChannel::~WebRTCDataChannel() {
}
