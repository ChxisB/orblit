/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_loader_hdr.h"

static Ref<ImageLoaderHDR> image_loader_hdr;

void initialize_hdr_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	image_loader_hdr.instantiate();
	ImageLoader::add_image_format_loader(image_loader_hdr);
}

void uninitialize_hdr_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	ImageLoader::remove_image_format_loader(image_loader_hdr);
	image_loader_hdr.unref();
}
