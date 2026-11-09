/**************************************************************************/
/*  register_types.h                                                      */

#pragma once

#include "modules/register_module_types.h"

void initialize_mbedtls_module(ModuleInitializationLevel p_level);
void uninitialize_mbedtls_module(ModuleInitializationLevel p_level);
