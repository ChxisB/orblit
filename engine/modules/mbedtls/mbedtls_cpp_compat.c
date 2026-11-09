/**************************************************************************/
/*  mbedtls_cpp_compat.c                                                  */

#include "mbedtls_cpp_compat.h"

int orblit_mbedtls_ecdsa_raw_to_der(size_t bits, const unsigned char *raw, size_t raw_len, unsigned char *der, size_t der_size, size_t *der_len) {
	return mbedtls_ecdsa_raw_to_der(bits, raw, raw_len, der, der_size, der_len);
}
int orblit_mbedtls_ecdsa_der_to_raw(size_t bits, const unsigned char *der, size_t der_len, unsigned char *raw, size_t raw_size, size_t *raw_len) {
	return mbedtls_ecdsa_der_to_raw(bits, der, der_len, raw, raw_size, raw_len);
}
