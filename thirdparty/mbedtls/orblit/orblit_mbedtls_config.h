/**************************************************************************/
/*  orblit_mbedtls_config.h                                               */

#pragma once

#include "platform_config.h"

#if defined(ORBLIT_MBEDTLS_INCLUDE_H)

// Allow platforms to customize the mbedTLS configuration.
#include ORBLIT_MBEDTLS_INCLUDE_H

#else

#if !defined(ORBLIT_MBEDTLS_LIGHT)
// Include default mbedTLS config.
#include <mbedtls/mbedtls_config.h>
#endif

#endif // ORBLIT_MBEDTLS_INCLUDE_H
