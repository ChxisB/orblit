/**************************************************************************/
/*  net_socket.cpp                                                        */

#include "net_socket.h"

Ref<NetSocket> (*NetSocket::_create)() = nullptr;

Ref<NetSocket> NetSocket::create() {
	if (_create) {
		return _create();
	}

	ERR_PRINT("Unable to create network socket, platform not supported");
	return nullptr;
}
