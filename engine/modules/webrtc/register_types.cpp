/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "webrtc_data_channel.h"
#include "webrtc_data_channel_extension.h"
#include "webrtc_multiplayer_peer.h"
#include "webrtc_peer_connection.h"
#include "webrtc_peer_connection_extension.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"

void initialize_webrtc_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
	GLOBAL_DEF(PropertyInfo(Variant::INT, "network/limits/webrtc/max_channel_in_buffer_kb", PROPERTY_HINT_RANGE, "2,4096,1,or_greater"), 64);

	ClassRegistry::register_custom_instance_class<WebRTCPeerConnection>();
	ORBLIT_REGISTER_CLASS(WebRTCPeerConnectionExtension);

	ORBLIT_REGISTER_ABSTRACT_CLASS(WebRTCDataChannel);
	ORBLIT_REGISTER_CLASS(WebRTCDataChannelExtension);

	ORBLIT_REGISTER_CLASS(WebRTCMultiplayerPeer);
}

void uninitialize_webrtc_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
}
