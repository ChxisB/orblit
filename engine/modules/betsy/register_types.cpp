/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_compress_betsy.h"

void initialize_betsy_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	Image::_image_compress_bptc_rd_func = _betsy_compress_bptc;
	Image::_image_compress_bc_rd_func = _betsy_compress_s3tc;
}

void uninitialize_betsy_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	free_device();
}
