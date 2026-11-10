/**************************************************************************/
/*  webrtc_peer_connection_extension.h                                    */

#pragma once

#include "webrtc_peer_connection.h"

#include "core/native_extension/ext_wrappers.gen.h"

class WebRTCPeerConnectionExtension : public WebRTCPeerConnection {
	ORBLIT_CLASS(WebRTCPeerConnectionExtension, WebRTCPeerConnection);

protected:
	static void register_members();

public:
	/** NativeExtension **/
	EXBIND0RC(ConnectionState, get_connection_state);
	EXBIND0RC(GatheringState, get_gathering_state);
	EXBIND0RC(SignalingState, get_signaling_state);
	EXBIND1R(Error, initialize, const Dictionary &);
	EXBIND2R(Ref<WebRTCDataChannel>, create_data_channel, const String &, const Dictionary &);
	EXBIND0R(Error, create_offer);
	EXBIND2R(Error, set_remote_description, const String &, const String &);
	EXBIND2R(Error, set_local_description, const String &, const String &);
	EXBIND3R(Error, add_ice_candidate, const String &, int, const String &);
	EXBIND0R(Error, poll);
	EXBIND0(close);

	WebRTCPeerConnectionExtension() {}
};
