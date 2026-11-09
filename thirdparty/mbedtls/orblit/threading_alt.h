/**************************************************************************/
/*  threading_alt.h                                                       */

#pragma once

#ifdef __cplusplus
extern "C" {
#endif

typedef struct {
	void *mutex;
} mbedtls_platform_mutex_t;

typedef struct {
	void *cv;
} mbedtls_platform_condition_variable_t;

#ifdef __cplusplus
};
#endif
