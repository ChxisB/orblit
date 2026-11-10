/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_saver_dds.h"
#include "texture_loader_dds.h"

#include "core/io/resource_loader.h"
#include "core/object/class_registry.h"
#include "scene/resources/texture.h"

static Ref<ResourceFormatDDS> resource_loader_dds;

void initialize_dds_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	Image::save_dds_func = save_dds;
	Image::save_dds_buffer_func = save_dds_buffer;

	if constexpr (ORBLIT_IS_CLASS_ENABLED(Texture)) {
		resource_loader_dds.instantiate();
		ResourceLoader::add_resource_format_loader(resource_loader_dds);
	}
}

void uninitialize_dds_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	if constexpr (ORBLIT_IS_CLASS_ENABLED(Texture)) {
		ResourceLoader::remove_resource_format_loader(resource_loader_dds);
		resource_loader_dds.unref();
	}

	Image::save_dds_func = nullptr;
	Image::save_dds_buffer_func = nullptr;
}
