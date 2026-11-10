/**************************************************************************/
/*  webrtc_peer_connection.cpp                                            */

#include "webrtc_peer_connection.h"

#ifdef WEB_ENABLED
#include "webrtc_peer_connection_js.h"
#endif

#include "webrtc_peer_connection_extension.h"

#include "core/object/class_registry.h"

StringName WebRTCPeerConnection::default_extension;

void WebRTCPeerConnection::set_default_extension(const StringName &p_extension) {
	ERR_FAIL_COND_MSG(!ClassRegistry::is_parent_class(p_extension, WebRTCPeerConnectionExtension::get_static_class_name()), vformat("Can't make %s the default WebRTC extension since it does not extend WebRTCPeerConnectionExtension.", p_extension));
	default_extension = StringName(p_extension, true);
}

WebRTCPeerConnection *WebRTCPeerConnection::create(bool p_notify_postinitialize) {
#ifdef WEB_ENABLED
	return static_cast<WebRTCPeerConnection *>(ClassRegistry::creator<WebRTCPeerConnectionJS>(p_notify_postinitialize));
#else
	if (default_extension == StringName()) {
		WARN_PRINT_ONCE("No default WebRTC extension configured.");
		return static_cast<WebRTCPeerConnection *>(ClassRegistry::creator<WebRTCPeerConnectionExtension>(p_notify_postinitialize));
	}
	Object *obj = nullptr;
	if (p_notify_postinitialize) {
		obj = ClassRegistry::instantiate(default_extension);
	} else {
		obj = ClassRegistry::instantiate_without_postinitialization(default_extension);
	}
	return Object::cast_to<WebRTCPeerConnectionExtension>(obj);
#endif
}

void WebRTCPeerConnection::register_members() {
	ClassRegistry::bind_static_method(get_static_class_name(), D_METHOD("set_default_extension", "extension_class"), &WebRTCPeerConnectionExtension::set_default_extension);

	ClassRegistry::bind_method(D_METHOD("initialize", "configuration"), &WebRTCPeerConnection::initialize, DEFVAL(Dictionary()));
	ClassRegistry::bind_method(D_METHOD("create_data_channel", "label", "options"), &WebRTCPeerConnection::create_data_channel, DEFVAL(Dictionary()));
	ClassRegistry::bind_method(D_METHOD("create_offer"), &WebRTCPeerConnection::create_offer);
	ClassRegistry::bind_method(D_METHOD("set_local_description", "type", "sdp"), &WebRTCPeerConnection::set_local_description);
	ClassRegistry::bind_method(D_METHOD("set_remote_description", "type", "sdp"), &WebRTCPeerConnection::set_remote_description);
	ClassRegistry::bind_method(D_METHOD("add_ice_candidate", "media", "index", "name"), &WebRTCPeerConnection::add_ice_candidate);
	ClassRegistry::bind_method(D_METHOD("poll"), &WebRTCPeerConnection::poll);
	ClassRegistry::bind_method(D_METHOD("close"), &WebRTCPeerConnection::close);

	ClassRegistry::bind_method(D_METHOD("get_connection_state"), &WebRTCPeerConnection::get_connection_state);
	ClassRegistry::bind_method(D_METHOD("get_gathering_state"), &WebRTCPeerConnection::get_gathering_state);
	ClassRegistry::bind_method(D_METHOD("get_signaling_state"), &WebRTCPeerConnection::get_signaling_state);

	ADD_SIGNAL(MethodInfo("session_description_created", PropertyInfo(Variant::STRING, "type"), PropertyInfo(Variant::STRING, "sdp")));
	ADD_SIGNAL(MethodInfo("ice_candidate_created", PropertyInfo(Variant::STRING, "media"), PropertyInfo(Variant::INT, "index"), PropertyInfo(Variant::STRING, "name")));
	ADD_SIGNAL(MethodInfo("data_channel_received", PropertyInfo(Variant::OBJECT, "channel", PROPERTY_HINT_RESOURCE_TYPE, WebRTCDataChannel::get_static_class_name())));

	BIND_ENUM_CONSTANT(STATE_NEW);
	BIND_ENUM_CONSTANT(STATE_CONNECTING);
	BIND_ENUM_CONSTANT(STATE_CONNECTED);
	BIND_ENUM_CONSTANT(STATE_DISCONNECTED);
	BIND_ENUM_CONSTANT(STATE_FAILED);
	BIND_ENUM_CONSTANT(STATE_CLOSED);

	BIND_ENUM_CONSTANT(GATHERING_STATE_NEW);
	BIND_ENUM_CONSTANT(GATHERING_STATE_GATHERING);
	BIND_ENUM_CONSTANT(GATHERING_STATE_COMPLETE);

	BIND_ENUM_CONSTANT(SIGNALING_STATE_STABLE);
	BIND_ENUM_CONSTANT(SIGNALING_STATE_HAVE_LOCAL_OFFER);
	BIND_ENUM_CONSTANT(SIGNALING_STATE_HAVE_REMOTE_OFFER);
	BIND_ENUM_CONSTANT(SIGNALING_STATE_HAVE_LOCAL_PRANSWER);
	BIND_ENUM_CONSTANT(SIGNALING_STATE_HAVE_REMOTE_PRANSWER);
	BIND_ENUM_CONSTANT(SIGNALING_STATE_CLOSED);
}

WebRTCPeerConnection::WebRTCPeerConnection() {
}

WebRTCPeerConnection::~WebRTCPeerConnection() {
}
