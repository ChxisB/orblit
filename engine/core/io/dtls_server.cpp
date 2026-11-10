/**************************************************************************/
/*  dtls_server.cpp                                                       */

#include "dtls_server.h"

#include "core/object/class_registry.h"

DTLSServer *DTLSServer::create(bool p_notify_postinitialize) {
	if (_create) {
		return _create(p_notify_postinitialize);
	}
	return nullptr;
}

bool DTLSServer::is_available() {
	return available;
}

void DTLSServer::register_members() {
	ClassRegistry::bind_method(D_METHOD("setup", "server_options"), &DTLSServer::setup);
	ClassRegistry::bind_method(D_METHOD("take_connection", "udp_peer"), &DTLSServer::take_connection);
}
