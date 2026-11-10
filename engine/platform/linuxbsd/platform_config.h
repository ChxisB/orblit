/**************************************************************************/
/*  platform_config.h                                                     */

#pragma once

#ifdef __linux__
#include <alloca.h>
#endif

#if defined(__FreeBSD__) || defined(__OpenBSD__) || defined(__NetBSD__)
#include <cstdlib> // alloca
// FreeBSD and OpenBSD use pthread_set_name_np, while other platforms,
// include NetBSD, use pthread_setname_np. NetBSD's version however requires
// a different format, we handle this directly in thread_posix.
#ifdef __NetBSD__
#define PTHREAD_NETBSD_SET_NAME
#else
#define PTHREAD_BSD_SET_NAME
#endif
#endif
