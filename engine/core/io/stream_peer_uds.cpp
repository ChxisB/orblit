/**************************************************************************/
/*  stream_peer_uds.cpp                                                   */

#include "stream_peer_uds.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"
#include "core/os/os.h"

void StreamPeerUDS::register_members() {
	ClassRegistry::bind_method(D_METHOD("bind", "path"), &StreamPeerUDS::bind);
	ClassRegistry::bind_method(D_METHOD("connect_to_host", "path"), &StreamPeerUDS::connect_to_host);
	ClassRegistry::bind_method(D_METHOD("get_connected_path"), &StreamPeerUDS::get_connected_path);
}

void StreamPeerUDS::accept_socket(Ref<NetSocket> p_sock, const NetSocket::Address &p_addr) {
	_sock = p_sock;
	_sock->set_blocking_enabled(false);

	timeout = OS::get_singleton()->get_ticks_msec() + (((uint64_t)GLOBAL_GET("network/limits/unix/connect_timeout_seconds")) * 1000);
	status = STATUS_CONNECTED;
}

Error StreamPeerUDS::bind(const String &p_path) {
	ERR_FAIL_COND_V(_sock.is_null(), ERR_UNAVAILABLE);
	ERR_FAIL_COND_V(_sock->is_open(), ERR_ALREADY_IN_USE);

	IP::Type ip_type = IP::TYPE_NONE;
	RETURN_IF_ERROR(_sock->open(NetSocket::Family::UNIX, NetSocket::TYPE_NONE, ip_type));
	_sock->set_blocking_enabled(false);
	NetSocket::Address addr(p_path);
	return _sock->bind(addr);
}

Error StreamPeerUDS::connect_to_host(const String &p_path) {
	ERR_FAIL_COND_V(_sock.is_null(), ERR_UNAVAILABLE);
	ERR_FAIL_COND_V(_sock->is_open(), ERR_ALREADY_IN_USE);
	ERR_FAIL_COND_V(p_path.is_empty(), ERR_INVALID_PARAMETER);

	if (!_sock->is_open()) {
		IP::Type ip_type = IP::TYPE_NONE;
		RETURN_IF_ERROR(_sock->open(NetSocket::Family::UNIX, NetSocket::TYPE_NONE, ip_type));
		_sock->set_blocking_enabled(false);
	}

	timeout = OS::get_singleton()->get_ticks_msec() + (((uint64_t)GLOBAL_GET("network/limits/unix/connect_timeout_seconds")) * 1000);
	NetSocket::Address addr(p_path);
	Error err = _sock->connect_to_host(addr);

	if (err == OK) {
		status = STATUS_CONNECTED;
	} else if (err == ERR_BUSY) {
		status = STATUS_CONNECTING;
	} else {
		ERR_PRINT("Connection to remote host failed!");
		disconnect_from_host();
		return FAILED;
	}

	peer_address = addr;
	peer_path = p_path;

	return OK;
}

const String StreamPeerUDS::get_connected_path() const {
	return String(peer_address.get_path().get_data());
}
