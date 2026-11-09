/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_decompress_bcdec.h"

void initialize_bcdec_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	Image::_image_decompress_bc = image_decompress_bcdec;
	Image::_image_decompress_bptc = image_decompress_bcdec;
}

void uninitialize_bcdec_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
}
