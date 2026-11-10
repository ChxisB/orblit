/**************************************************************************/
/*  nir.h                                                                 */

#pragma once

#include "core/typedefs.h"

ORBLIT_GCC_WARNING_PUSH_AND_IGNORE("-Wmissing-field-initializers")
ORBLIT_CLANG_WARNING_PUSH_AND_IGNORE("-Wmissing-field-initializers")
ORBLIT_MSVC_WARNING_PUSH
ORBLIT_MSVC_WARNING_IGNORE(4200) // "nonstandard extension used: zero-sized array in struct/union".
ORBLIT_MSVC_WARNING_IGNORE(4806) // "'&': unsafe operation: no value of type 'bool' promoted to type 'uint32_t' can equal the given constant".

#include <nir_spirv.h>
#include <nir_to_dxil.h>
#include <spirv_to_dxil.h>
extern "C" {
#include <dxil_spirv_nir.h>
}

ORBLIT_GCC_WARNING_POP
ORBLIT_CLANG_WARNING_POP
ORBLIT_MSVC_WARNING_POP
