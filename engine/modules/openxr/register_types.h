/**************************************************************************/
/*  register_types.h                                                      */

#pragma once

#define MODULE_OPENXR_HAS_PREREGISTER

#include "modules/register_module_types.h"

void initialize_openxr_module(ModuleInitializationLevel p_level);
void uninitialize_openxr_module(ModuleInitializationLevel p_level);
