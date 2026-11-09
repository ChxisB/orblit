/**************************************************************************/
/*  dtls_server_mbedtls.h                                                 */

#pragma once

#include "tls_context_mbedtls.h"

#include "core/io/dtls_server.h"

class DTLSServerMbedTLS : public DTLSServer {
private:
	static DTLSServer *_create_func(bool p_notify_postinitialize);
	Ref<TLSOptions> tls_options;
	Ref<CookieContextMbedTLS> cookies;

public:
	static void initialize();
	static void finalize();

	Error setup(Ref<TLSOptions> p_options) override;
	void stop() override;
	Ref<PacketPeerDTLS> take_connection(Ref<PacketPeerUDP> p_peer) override;

	DTLSServerMbedTLS();
	~DTLSServerMbedTLS() override;
};
