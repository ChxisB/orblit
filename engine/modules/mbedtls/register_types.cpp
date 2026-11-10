/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "crypto_mbedtls.h"
#include "dtls_server_mbedtls.h"
#include "packet_peer_mbed_dtls.h"
#include "stream_peer_mbedtls.h"

#include "core/config/project_settings.h"
#include "core/os/os.h"

#include <psa/crypto.h>

static bool orblit_mbedtls_initialized = false;

void initialize_mbedtls_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_CORE) {
		return;
	}

	GLOBAL_DEF("network/tls/enable_tls_v1.3", true);

#ifdef DEBUG_ENABLED
	if (OS::get_singleton()->is_stdout_verbose()) {
		mbedtls_debug_set_threshold(1);
	}
#endif

	orblit_mbedtls_initialized = true;

	CryptoMbedTLS::initialize_crypto();
	StreamPeerMbedTLS::initialize_tls();
	PacketPeerMbedDTLS::initialize_dtls();
	DTLSServerMbedTLS::initialize();
}

void uninitialize_mbedtls_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_CORE) {
		return;
	}

	if (!orblit_mbedtls_initialized) {
		return;
	}

	DTLSServerMbedTLS::finalize();
	PacketPeerMbedDTLS::finalize_dtls();
	StreamPeerMbedTLS::finalize_tls();
	CryptoMbedTLS::finalize_crypto();
}
