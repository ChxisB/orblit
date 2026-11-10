/**************************************************************************/
/*  platform_config.h                                                     */

#pragma once

#include <alloca.h>

#define PLATFORM_THREAD_OVERRIDE

#define PTHREAD_RENAME_SELF

#define _weakify(var) __weak typeof(var) GDWeak_##var = var;
#define _strongify(var) \
	_Pragma("clang diagnostic push") \
			_Pragma("clang diagnostic ignored \"-Wshadow\"") \
					__strong typeof(var) var = GDWeak_##var; \
	_Pragma("clang diagnostic pop")
