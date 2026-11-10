/**************************************************************************/
/*  d3dx12.h                                                              */

#pragma once

#include "core/typedefs.h"

#if !defined(_MSC_VER) && !defined(__REQUIRED_RPCNDR_H_VERSION__)
// Match current version used by MinGW, MSVC and Direct3D 12 headers use 500.
#define __REQUIRED_RPCNDR_H_VERSION__ 475
#endif // !defined(_MSC_VER) && !defined(__REQUIRED_RPCNDR_H_VERSION__)

ORBLIT_GCC_WARNING_PUSH_AND_IGNORE("-Wnon-virtual-dtor")
ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wnon-virtual-dtor")

#include <thirdparty/directx_headers/include/directx/d3dx12.h> // IWYU pragma: export.

ORBLIT_GCC_WARNING_POP
ORBLIT_CLANG_WARNING_POP
