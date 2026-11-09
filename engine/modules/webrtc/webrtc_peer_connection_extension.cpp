/**************************************************************************/
/*  webrtc_peer_connection_extension.cpp                                  */

#include "webrtc_peer_connection_extension.h"

#include "core/object/class_registry.h" // IWYU pragma: keep. `ORBLIT_VIRTUAL_BIND` macro.

void WebRTCPeerConnectionExtension::register_members() {
	ORBLIT_VIRTUAL_BIND(_get_connection_state);
	ORBLIT_VIRTUAL_BIND(_get_gathering_state);
	ORBLIT_VIRTUAL_BIND(_get_signaling_state);
	ORBLIT_VIRTUAL_BIND(_initialize, "config");
	ORBLIT_VIRTUAL_BIND(_create_data_channel, "label", "config");
	ORBLIT_VIRTUAL_BIND(_create_offer);
	ORBLIT_VIRTUAL_BIND(_set_remote_description, "type", "sdp");
	ORBLIT_VIRTUAL_BIND(_set_local_description, "type", "sdp");
	ORBLIT_VIRTUAL_BIND(_add_ice_candidate, "sdp_mid_name", "sdp_mline_index", "sdp_name");
	ORBLIT_VIRTUAL_BIND(_poll);
	ORBLIT_VIRTUAL_BIND(_close);
}
