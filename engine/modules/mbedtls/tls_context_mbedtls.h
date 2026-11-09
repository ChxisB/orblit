/**************************************************************************/
/*  tls_context_mbedtls.h                                                 */

#pragma once

#include "crypto_mbedtls.h"

#include "core/object/ref_counted.h"

#include <mbedtls/debug.h>
#include <mbedtls/ssl.h>
#include <mbedtls/ssl_cookie.h>

class TLSContextMbedTLS;

class CookieContextMbedTLS : public RefCounted {
	ORBLIT_SOFTCLASS(CookieContextMbedTLS, RefCounted);

	friend class TLSContextMbedTLS;

protected:
	bool inited = false;
	mbedtls_ssl_cookie_ctx cookie_ctx;

public:
	Error setup();
	void clear();

	CookieContextMbedTLS();
	~CookieContextMbedTLS() override;
};

class TLSContextMbedTLS : public RefCounted {
	ORBLIT_SOFTCLASS(TLSContextMbedTLS, RefCounted);

private:
	bool inited = false;
	mbedtls_pk_context pk;
	Ref<X509CertificateMbedTLS> certs;
	Ref<CookieContextMbedTLS> cookies;
	mbedtls_ssl_context tls;
	mbedtls_ssl_config conf;

	Error _setup(int p_endpoint, int p_transport, int p_authmode);

public:
	static void print_mbedtls_error(int p_ret);

	Error init_server(int p_transport, Ref<TLSOptions> p_options, Ref<CookieContextMbedTLS> p_cookies = Ref<CookieContextMbedTLS>());
	Error init_client(int p_transport, const String &p_hostname, Ref<TLSOptions> p_options);
	void clear();

	mbedtls_ssl_context *get_context();

	TLSContextMbedTLS();
	~TLSContextMbedTLS() override;
};
