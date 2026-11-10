/**************************************************************************/
/*  stream_peer_tls.cpp                                                   */

#include "stream_peer_tls.h"

#include "core/object/class_registry.h"

StreamPeerTLS *(*StreamPeerTLS::_create)(bool p_notify_postinitialize) = nullptr;

StreamPeerTLS *StreamPeerTLS::create(bool p_notify_postinitialize) {
	if (_create) {
		return _create(p_notify_postinitialize);
	}
	return nullptr;
}

bool StreamPeerTLS::is_available() {
	return _create != nullptr;
}

void StreamPeerTLS::register_members() {
	ClassRegistry::bind_method(D_METHOD("poll"), &StreamPeerTLS::poll);
	ClassRegistry::bind_method(D_METHOD("accept_stream", "stream", "server_options"), &StreamPeerTLS::accept_stream);
	ClassRegistry::bind_method(D_METHOD("connect_to_stream", "stream", "common_name", "client_options"), &StreamPeerTLS::connect_to_stream, DEFVAL(Ref<TLSOptions>()));
	ClassRegistry::bind_method(D_METHOD("get_status"), &StreamPeerTLS::get_status);
	ClassRegistry::bind_method(D_METHOD("get_stream"), &StreamPeerTLS::get_stream);
	ClassRegistry::bind_method(D_METHOD("disconnect_from_stream"), &StreamPeerTLS::disconnect_from_stream);

	BIND_ENUM_CONSTANT(STATUS_DISCONNECTED);
	BIND_ENUM_CONSTANT(STATUS_HANDSHAKING);
	BIND_ENUM_CONSTANT(STATUS_CONNECTED);
	BIND_ENUM_CONSTANT(STATUS_ERROR);
	BIND_ENUM_CONSTANT(STATUS_ERROR_HOSTNAME_MISMATCH);
}
