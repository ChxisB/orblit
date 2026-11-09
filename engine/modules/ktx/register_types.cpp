/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "texture_loader_ktx.h"

#include "core/io/resource_loader.h"
#include "core/object/class_registry.h"
#include "scene/resources/image_texture.h"

static Ref<ResourceFormatKTX> resource_loader_ktx;

void initialize_ktx_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(ImageTexture)) {
		resource_loader_ktx.instantiate();
		ResourceLoader::add_resource_format_loader(resource_loader_ktx);
	}
}

void uninitialize_ktx_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(ImageTexture)) {
		ResourceLoader::remove_resource_format_loader(resource_loader_ktx);
		resource_loader_ktx.unref();
	}
}
