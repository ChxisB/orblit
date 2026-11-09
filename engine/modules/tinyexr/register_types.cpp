/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_loader_tinyexr.h"
#include "image_saver_tinyexr.h"

static Ref<ImageLoaderTinyEXR> image_loader_tinyexr;

void initialize_tinyexr_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	image_loader_tinyexr.instantiate();
	ImageLoader::add_image_format_loader(image_loader_tinyexr);

	Image::save_exr_func = save_exr;
	Image::save_exr_buffer_func = save_exr_buffer;
}

void uninitialize_tinyexr_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	ImageLoader::remove_image_format_loader(image_loader_tinyexr);
	image_loader_tinyexr.unref();

	Image::save_exr_func = nullptr;
}
