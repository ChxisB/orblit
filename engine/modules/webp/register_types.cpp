/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_loader_webp.h"
#include "resource_saver_webp.h"

#include "core/io/resource_saver.h"

static Ref<ImageLoaderWebP> image_loader_webp;
static Ref<ResourceSaverWebP> resource_saver_webp;

void initialize_webp_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	image_loader_webp.instantiate();
	ImageLoader::add_image_format_loader(image_loader_webp);

	resource_saver_webp.instantiate();
	ResourceSaver::add_resource_format_saver(resource_saver_webp);
}

void uninitialize_webp_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	ImageLoader::remove_image_format_loader(image_loader_webp);
	image_loader_webp.unref();

	ResourceSaver::remove_resource_format_saver(resource_saver_webp);
	resource_saver_webp.unref();
}
