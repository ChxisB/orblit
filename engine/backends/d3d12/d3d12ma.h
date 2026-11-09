/**************************************************************************/
/*  d3d12ma.h                                                             */

#pragma once

#include "core/typedefs.h"

ORBLIT_GCC_WARNING_PUSH_AND_IGNORE("-Wnon-virtual-dtor")
ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wnon-virtual-dtor")

#include <backends/d3d12/d3dx12.h>
#include <dxgi1_6.h>

#define D3D12MA_D3D12_HEADERS_ALREADY_INCLUDED
#include <thirdparty/d3d12ma/D3D12MemAlloc.h> // IWYU pragma: export.

ORBLIT_GCC_WARNING_POP
ORBLIT_CLANG_WARNING_POP
