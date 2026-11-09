/**************************************************************************/
/*  mbedtls_cpp_compat.h                                                  */

#pragma once

#include <mbedtls/psa_util.h>

#ifdef MBEDTLS_ECDSA_DER_MAX_LEN
#define ORBLIT_MBEDTLS_ECDSA_DER_MAX_LEN MBEDTLS_ECDSA_DER_MAX_LEN
#else
#define ORBLIT_MBEDTLS_ECDSA_DER_MAX_LEN 192
#endif

// The compatibility functions defined in <mbedtls/psa_util.h> are not guarded by
// __cplusplus ifdefs which results in incorrect symbols when included from C++.
// For this reason, (and to avoid problems if this is fixed in future versions of
// the header) we define our own wrappers implemented in pure C.
#ifdef __cplusplus
extern "C" {
#endif
int orblit_mbedtls_ecdsa_raw_to_der(size_t bits, const unsigned char *raw, size_t raw_len, unsigned char *der, size_t der_size, size_t *der_len);
int orblit_mbedtls_ecdsa_der_to_raw(size_t bits, const unsigned char *der, size_t der_len, unsigned char *raw, size_t raw_size, size_t *raw_len);
#ifdef __cplusplus
}
#endif
