/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_compress_cvtt.h"

void initialize_cvtt_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	Image::_image_compress_bptc_func = image_compress_cvtt;
}

void uninitialize_cvtt_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
}
