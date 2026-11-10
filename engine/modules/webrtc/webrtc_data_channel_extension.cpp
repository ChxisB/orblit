/**************************************************************************/
/*  webrtc_data_channel_extension.cpp                                     */

#include "webrtc_data_channel_extension.h"

#include "core/object/class_registry.h" // IWYU pragma: keep. `ORBLIT_VIRTUAL_BIND` macro.

void WebRTCDataChannelExtension::register_members() {
	ADD_PROPERTY_DEFAULT("write_mode", WRITE_MODE_BINARY);

	ORBLIT_VIRTUAL_BIND(_get_packet, "r_buffer", "r_buffer_size");
	ORBLIT_VIRTUAL_BIND(_put_packet, "buffer", "buffer_size");
	ORBLIT_VIRTUAL_BIND(_get_available_packet_count);
	ORBLIT_VIRTUAL_BIND(_get_max_packet_size);

	ORBLIT_VIRTUAL_BIND(_poll);
	ORBLIT_VIRTUAL_BIND(_close);

	ORBLIT_VIRTUAL_BIND(_set_write_mode, "write_mode");
	ORBLIT_VIRTUAL_BIND(_get_write_mode);

	ORBLIT_VIRTUAL_BIND(_was_string_packet);
	ORBLIT_VIRTUAL_BIND(_get_ready_state);
	ORBLIT_VIRTUAL_BIND(_get_label);
	ORBLIT_VIRTUAL_BIND(_is_ordered);
	ORBLIT_VIRTUAL_BIND(_get_id);
	ORBLIT_VIRTUAL_BIND(_get_max_packet_life_time);
	ORBLIT_VIRTUAL_BIND(_get_max_retransmits);
	ORBLIT_VIRTUAL_BIND(_get_protocol);
	ORBLIT_VIRTUAL_BIND(_is_negotiated);
	ORBLIT_VIRTUAL_BIND(_get_buffered_amount);
}

Error WebRTCDataChannelExtension::get_packet(const uint8_t **r_buffer, int &r_buffer_size) {
	Error err;
	if (ORBLIT_VIRTUAL_CALL(_get_packet, r_buffer, &r_buffer_size, err)) {
		return err;
	}
	WARN_PRINT_ONCE("WebRTCDataChannelExtension::_get_packet_native is unimplemented!");
	return FAILED;
}

Error WebRTCDataChannelExtension::put_packet(const uint8_t *p_buffer, int p_buffer_size) {
	Error err;
	if (ORBLIT_VIRTUAL_CALL(_put_packet, p_buffer, p_buffer_size, err)) {
		return err;
	}
	WARN_PRINT_ONCE("WebRTCDataChannelExtension::_put_packet_native is unimplemented!");
	return FAILED;
}
