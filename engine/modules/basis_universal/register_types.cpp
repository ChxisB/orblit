/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_compress_basisu.h"

#include "core/config/project_settings.h"

void initialize_basis_universal_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	basis_universal_init();

#ifdef TOOLS_ENABLED
	GLOBAL_DEF(PropertyInfo(Variant::INT, "rendering/textures/basis_universal/rdo_dict_size", PROPERTY_HINT_RANGE, "64,65536,1"), 1024);
	GLOBAL_DEF(PropertyInfo(Variant::BOOL, "rendering/textures/basis_universal/zstd_supercompression"), true);
	GLOBAL_DEF(PropertyInfo(Variant::INT, "rendering/textures/basis_universal/zstd_supercompression_level", PROPERTY_HINT_RANGE, "1,22,1"), 6);

	Image::basis_universal_packer = basis_universal_packer;
#endif

	Image::basis_universal_unpacker = basis_universal_unpacker;
	Image::basis_universal_unpacker_ptr = basis_universal_unpacker_ptr;
}

void uninitialize_basis_universal_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

#ifdef TOOLS_ENABLED
	Image::basis_universal_packer = nullptr;
#endif

	Image::basis_universal_unpacker = nullptr;
	Image::basis_universal_unpacker_ptr = nullptr;
}
