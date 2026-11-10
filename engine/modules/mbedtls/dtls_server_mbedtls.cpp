/**************************************************************************/
/*  dtls_server_mbedtls.cpp                                               */

#include "dtls_server_mbedtls.h"

#include "packet_peer_mbed_dtls.h"

#include "core/object/class_registry.h"

Error DTLSServerMbedTLS::setup(Ref<TLSOptions> p_options) {
	ERR_FAIL_COND_V(p_options.is_null() || !p_options->is_server(), ERR_INVALID_PARAMETER);
	ERR_FAIL_COND_V(cookies->setup() != OK, ERR_ALREADY_IN_USE);
	tls_options = p_options;
	return OK;
}

void DTLSServerMbedTLS::stop() {
	cookies->clear();
}

Ref<PacketPeerDTLS> DTLSServerMbedTLS::take_connection(Ref<PacketPeerUDP> p_udp_peer) {
	Ref<PacketPeerMbedDTLS> out;

	ERR_FAIL_COND_V(tls_options.is_null(), out);
	ERR_FAIL_COND_V(p_udp_peer.is_null(), out);

	out.instantiate();
	out->accept_peer(p_udp_peer, tls_options, cookies);
	return out;
}

DTLSServer *DTLSServerMbedTLS::_create_func(bool p_notify_postinitialize) {
	return static_cast<DTLSServer *>(ClassRegistry::creator<DTLSServerMbedTLS>(p_notify_postinitialize));
}

void DTLSServerMbedTLS::initialize() {
	_create = _create_func;
	available = true;
}

void DTLSServerMbedTLS::finalize() {
	_create = nullptr;
	available = false;
}

DTLSServerMbedTLS::DTLSServerMbedTLS() {
	cookies.instantiate();
}

DTLSServerMbedTLS::~DTLSServerMbedTLS() {
	stop();
}
