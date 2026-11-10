/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_compress_astcenc.h"

void initialize_astcenc_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

#ifdef TOOLS_ENABLED
	Image::_image_compress_astc_func = _compress_astc;
#endif

	Image::_image_decompress_astc = _decompress_astc;
}

void uninitialize_astcenc_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
}
