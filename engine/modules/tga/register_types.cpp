/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_loader_tga.h"

static Ref<ImageLoaderTGA> image_loader_tga;

void initialize_tga_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	image_loader_tga.instantiate();
	ImageLoader::add_image_format_loader(image_loader_tga);
}

void uninitialize_tga_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	ImageLoader::remove_image_format_loader(image_loader_tga);
	image_loader_tga.unref();
}
