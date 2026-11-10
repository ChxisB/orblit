/**************************************************************************/
/*  tcp_server.cpp                                                        */

#include "tcp_server.h"

#include "core/object/class_registry.h"

void TCPServer::register_members() {
	ClassRegistry::bind_method(D_METHOD("listen", "port", "bind_address"), &TCPServer::listen, DEFVAL("*"));
	ClassRegistry::bind_method(D_METHOD("get_local_port"), &TCPServer::get_local_port);
	ClassRegistry::bind_method(D_METHOD("take_connection"), &TCPServer::take_connection);
}

Error TCPServer::listen(uint16_t p_port, const IPAddress &p_bind_address) {
	ERR_FAIL_COND_V(_sock.is_null(), ERR_UNAVAILABLE);
	ERR_FAIL_COND_V(_sock->is_open(), ERR_ALREADY_IN_USE);
	ERR_FAIL_COND_V(!p_bind_address.is_valid() && !p_bind_address.is_wildcard(), ERR_INVALID_PARAMETER);

	Error err;
	IP::Type ip_type = IP::TYPE_ANY;

	// If the bind address is valid use its type as the socket type
	if (p_bind_address.is_valid()) {
		ip_type = p_bind_address.is_ipv4() ? IP::TYPE_IPV4 : IP::TYPE_IPV6;
	}

	err = _sock->open(NetSocket::Family::INET, NetSocket::TYPE_TCP, ip_type);

	ERR_FAIL_COND_V(err != OK, ERR_CANT_CREATE);

	_sock->set_reuse_address_enabled(true);

	return _listen(NetSocket::Address(p_bind_address, p_port));
}

int TCPServer::get_local_port() const {
	NetSocket::Address addr;
	_sock->get_socket_address(&addr);
	return addr.port();
}

Ref<StreamPeerTCP> TCPServer::take_connection() {
	return _take_connection<StreamPeerTCP>();
}
