/**************************************************************************/
/*  macros.h                                                              */

#pragma once

#include "core/typedefs.h"

// unreachable

#if defined(_MSC_VER)
#define ORBLIT_UNREACHABLE() __assume(0)
#elif defined(__GNUC__) && (__GNUC__ * 100 + __GNUC_MINOR__) >= 405
#define ORBLIT_UNREACHABLE() __builtin_unreachable()
#else
#define ORBLIT_UNREACHABLE() \
	CRASH_NOW(); \
	do { \
	} while (true)
#endif

namespace dotnet_host {

template <typename F>
struct ScopeExit {
	ScopeExit(F p_exit_func) :
			exit_func(p_exit_func) {}
	~ScopeExit() { exit_func(); }
	F exit_func;
};

class ScopeExitAux {
public:
	template <typename F>
	ScopeExit<F> operator+(F p_exit_func) { return ScopeExit<F>(p_exit_func); }
};
} // namespace dotnet_host

#define SCOPE_EXIT \
	auto ORBLIT_UNIQUE_NAME(gd_scope_exit) = dotnet_host::ScopeExitAux() + [=]() -> void
