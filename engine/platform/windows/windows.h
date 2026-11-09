/**************************************************************************/
/*  windows.h                                                             */

#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#ifndef NOMINMAX
#define NOMINMAX
#endif

#ifdef _MSC_VER
// HACK: MSVC lacks an `include_next` equivalent, so we'll instead utilize the relative path of
//  the system include by going up one directory and immediately returning.
#include <../um/windows.h>
#else
#include_next <windows.h>
#endif

// windows.h badly defines macros that clash with our own or types/enums.
#undef ERROR // wingdi.h
#undef DELETE // winnt.h
#undef MessageBox // winuser.h
#undef CONNECT_DEFERRED // winnetwk.h
#undef MONO_FONT // wingdi.h
