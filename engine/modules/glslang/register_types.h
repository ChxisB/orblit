/**************************************************************************/
/*  register_types.h                                                      */

#pragma once

#define MODULE_GLSLANG_HAS_PREREGISTER

#include "modules/register_module_types.h"

void initialize_glslang_module(ModuleInitializationLevel p_level);
void uninitialize_glslang_module(ModuleInitializationLevel p_level);
