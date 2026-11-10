/**************************************************************************/
/*  register_types.h                                                      */

#pragma once

#ifdef VISIONOS_ENABLED

#include "modules/register_module_types.h"

void initialize_visionos_xr_module(ModuleInitializationLevel p_level);
void uninitialize_visionos_xr_module(ModuleInitializationLevel p_level);

#endif
