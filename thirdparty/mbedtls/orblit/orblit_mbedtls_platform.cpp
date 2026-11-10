/**************************************************************************/
/*  orblit_mbedtls_platform.cpp                                           */

#include "core/os/condition_variable.h"
#include "core/os/memory.h"
#include "core/os/mutex.h"
#include "core/os/os.h"

#include <mbedtls/build_info.h>
#include <mbedtls/threading.h>
#include <tf-psa-crypto/build_info.h>

extern "C" {
#ifdef MBEDTLS_PLATFORM_ZEROIZE_ALT
static void *(*const volatile memset_func)(void *, int, size_t) = memset;

void mbedtls_platform_zeroize(void *buf, size_t len) {
	memset_func(buf, 0, len);
}

void mbedtls_zeroize_and_free(void *buf, size_t len) {
	if (buf != NULL) {
		mbedtls_platform_zeroize(buf, len);
	}
	free(buf);
}
#endif
#if defined(MBEDTLS_PSA_DRIVER_GET_ENTROPY)
int mbedtls_platform_get_entropy(uint32_t p_flags, size_t *r_estimate_bits, unsigned char *r_output, size_t p_output_size) {
	ERR_FAIL_NULL_V(OS::get_singleton(), -1);
	Error err = OS::get_singleton()->get_entropy(r_output, p_output_size);
	ERR_FAIL_COND_V(err != OK, -1);
	*r_estimate_bits = p_output_size * 8;
	return 0;
}
#endif
#if defined(MBEDTLS_THREADING_ALT)
int orblit_mbedtls_mutex_init(mbedtls_platform_mutex_t *p_mutex) {
	ERR_FAIL_NULL_V(p_mutex, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	p_mutex->mutex = memnew(BinaryMutex);
	return 0;
}

void orblit_mbedtls_mutex_free(mbedtls_platform_mutex_t *p_mutex) {
	ERR_FAIL_NULL(p_mutex);
	ERR_FAIL_NULL(p_mutex->mutex);
	memdelete((BinaryMutex *)p_mutex->mutex);
}

int orblit_mbedtls_mutex_lock(mbedtls_platform_mutex_t *p_mutex) {
	ERR_FAIL_NULL_V(p_mutex, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	ERR_FAIL_NULL_V(p_mutex->mutex, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	((BinaryMutex *)p_mutex->mutex)->lock();
	return 0;
}

int orblit_mbedtls_mutex_unlock(mbedtls_platform_mutex_t *p_mutex) {
	ERR_FAIL_NULL_V(p_mutex, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	ERR_FAIL_NULL_V(p_mutex->mutex, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	((BinaryMutex *)p_mutex->mutex)->unlock();
	return 0;
}

int orblit_mbedtls_cond_init(mbedtls_platform_condition_variable_t *p_cv) {
	ERR_FAIL_NULL_V(p_cv, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	p_cv->cv = memnew(ConditionVariable);
	return 0;
}
void orblit_mbedtls_cond_destroy(mbedtls_platform_condition_variable_t *p_cv) {
	ERR_FAIL_NULL(p_cv);
	ERR_FAIL_NULL(p_cv->cv);
	memdelete((ConditionVariable *)p_cv->cv);
}
int orblit_mbedtls_cond_signal(mbedtls_platform_condition_variable_t *p_cv) {
	ERR_FAIL_NULL_V(p_cv, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	ERR_FAIL_NULL_V(p_cv->cv, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	((ConditionVariable *)p_cv->cv)->notify_one();
	return 0;
}
int orblit_mbedtls_cond_broadcast(mbedtls_platform_condition_variable_t *p_cv) {
	ERR_FAIL_NULL_V(p_cv, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	ERR_FAIL_NULL_V(p_cv->cv, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	((ConditionVariable *)p_cv->cv)->notify_all();
	return 0;
}
int orblit_mbedtls_cond_wait(mbedtls_platform_condition_variable_t *p_cv, mbedtls_platform_mutex_t *p_mutex) {
	// XXX: This function is not currently implemented but also **not used** by mbedTLS.
	// The library documentation states that this function is expected to be called with the mutex already locked.
	// This feels a bit strange (and not possible without changing Orblit core API).
	// Use a verbose error so we can catch it if a future mbedTLS version starts using it.
	ERR_FAIL_V(MBEDTLS_ERR_THREADING_USAGE_ERROR);
#if 0
	ERR_FAIL_NULL_V(p_mutex, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	ERR_FAIL_NULL_V(p_mutex->mutex, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	ERR_FAIL_NULL_V(p_cv, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	ERR_FAIL_NULL_V(p_cv->cv, MBEDTLS_ERR_THREADING_USAGE_ERROR);
	MutexLock lock(*((BinaryMutex *)p_mutex->mutex));
	((ConditionVariable *)p_cv->cv)->wait(lock);
	return 0;
#endif
}
#endif
void orblit_mbedtls_platform_init() {
#if defined(MBEDTLS_THREADING_ALT)
	mbedtls_threading_set_alt(
			orblit_mbedtls_mutex_init,
			orblit_mbedtls_mutex_free,
			orblit_mbedtls_mutex_lock,
			orblit_mbedtls_mutex_unlock,
			orblit_mbedtls_cond_init,
			orblit_mbedtls_cond_destroy,
			orblit_mbedtls_cond_signal,
			orblit_mbedtls_cond_broadcast,
			orblit_mbedtls_cond_wait);
#endif
}
void orblit_mbedtls_platform_free() {
#if defined(MBEDTLS_THREADING_ALT)
	mbedtls_threading_free_alt();
#endif
}
};
