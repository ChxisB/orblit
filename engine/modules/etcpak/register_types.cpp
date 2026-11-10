/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_compress_etcpak.h"
#include "image_decompress_etcpak.h"

void initialize_etcpak_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

#ifdef ETCPAK_COMPRESS_ENABLED
	Image::_image_compress_etc1_func = _compress_etc1;
	Image::_image_compress_etc2_func = _compress_etc2;
	Image::_image_compress_bc_func = _compress_bc;
#endif

	Image::_image_decompress_etc1 = _decompress_etc;
	Image::_image_decompress_etc2 = _decompress_etc;
}

void uninitialize_etcpak_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
}
