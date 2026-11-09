/**************************************************************************/
/*  uds_server.cpp                                                        */

#include "uds_server.h"

#include "core/object/class_registry.h"

void UDSServer::register_members() {
	ClassRegistry::bind_method(D_METHOD("listen", "path"), &UDSServer::listen);
	ClassRegistry::bind_method(D_METHOD("take_connection"), &UDSServer::take_connection);
}

Error UDSServer::listen(const String &p_path) {
	ERR_FAIL_COND_V(_sock.is_null(), ERR_UNAVAILABLE);
	ERR_FAIL_COND_V(_sock->is_open(), ERR_ALREADY_IN_USE);
	ERR_FAIL_COND_V(p_path.is_empty(), ERR_INVALID_PARAMETER);

	IP::Type ip_type = IP::TYPE_NONE;
	Error err = _sock->open(NetSocket::Family::UNIX, NetSocket::TYPE_NONE, ip_type);
	ERR_FAIL_COND_V(err != OK, ERR_CANT_CREATE);

	return _listen(p_path);
}

Ref<StreamPeerUDS> UDSServer::take_connection() {
	return _take_connection<StreamPeerUDS>();
}
