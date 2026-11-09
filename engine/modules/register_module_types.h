/**************************************************************************/
/*  register_module_types.h                                               */

#pragma once

#include "core/native_extension/native_extension_interface.gen.h"

enum ModuleInitializationLevel {
	MODULE_INITIALIZATION_LEVEL_CORE = NATIVE_EXTENSION_INITIALIZATION_CORE,
	MODULE_INITIALIZATION_LEVEL_SYSTEMS = NATIVE_EXTENSION_INITIALIZATION_SYSTEMS,
	MODULE_INITIALIZATION_LEVEL_SCENE = NATIVE_EXTENSION_INITIALIZATION_SCENE,
	MODULE_INITIALIZATION_LEVEL_EDITOR = NATIVE_EXTENSION_INITIALIZATION_EDITOR
};

void initialize_modules(ModuleInitializationLevel p_level);
void uninitialize_modules(ModuleInitializationLevel p_level);
