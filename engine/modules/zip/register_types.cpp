/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "zip_packer.h"
#include "zip_reader.h"

#include "core/object/class_registry.h"

void initialize_zip_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	ORBLIT_REGISTER_CLASS(ZIPPacker);
	ORBLIT_REGISTER_CLASS(ZIPReader);
}

void uninitialize_zip_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
}
