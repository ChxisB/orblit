/**************************************************************************/
/*  socket_server.cpp                                                     */

#include "socket_server.h"

#include "core/object/class_registry.h"

void SocketServer::register_members() {
	ClassRegistry::bind_method(D_METHOD("is_connection_available"), &SocketServer::is_connection_available);
	ClassRegistry::bind_method(D_METHOD("is_listening"), &SocketServer::is_listening);
	ClassRegistry::bind_method(D_METHOD("stop"), &SocketServer::stop);
	ClassRegistry::bind_method(D_METHOD("take_socket_connection"), &SocketServer::take_socket_connection);
}

Error SocketServer::_listen(const NetSocket::Address &p_addr) {
	DEV_ASSERT(_sock.is_valid());
	DEV_ASSERT(_sock->is_open());

	_sock->set_blocking_enabled(false);
	Error err = _sock->bind(p_addr);

	if (err != OK) {
		_sock->close();
		return ERR_ALREADY_IN_USE;
	}

	err = _sock->listen(MAX_PENDING_CONNECTIONS);

	if (err != OK) {
		_sock->close();
		return FAILED;
	}
	return OK;
}

bool SocketServer::is_listening() const {
	ERR_FAIL_COND_V(_sock.is_null(), false);

	return _sock->is_open();
}

bool SocketServer::is_connection_available() const {
	ERR_FAIL_COND_V(_sock.is_null(), false);

	if (!_sock->is_open()) {
		return false;
	}

	Error err = _sock->poll(NetSocket::POLL_TYPE_IN, 0);
	return (err == OK);
}

void SocketServer::stop() {
	if (_sock.is_valid()) {
		_sock->close();
	}
}

SocketServer::SocketServer() :
		_sock(NetSocket::create()) {
}

SocketServer::~SocketServer() {
	stop();
}
