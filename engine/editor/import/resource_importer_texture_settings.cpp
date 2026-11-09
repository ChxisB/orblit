/**************************************************************************/
/*  resource_importer_texture_settings.cpp                                */

#include "resource_importer_texture_settings.h"

#include "core/config/project_settings.h"
#include "core/os/os.h"

// ResourceImporterTextureSettings contains code used by
// multiple texture importers and the export dialog.
bool ResourceImporterTextureSettings::should_import_s3tc_bptc() {
	if (GLOBAL_GET("rendering/textures/vram_compression/import_s3tc_bptc")) {
		return true;
	}
	// If the project settings override is not enabled, import
	// S3TC/BPTC only when the host operating system needs it.
	return OS::get_singleton()->get_preferred_texture_format() == OS::PREFERRED_TEXTURE_FORMAT_S3TC_BPTC;
}

bool ResourceImporterTextureSettings::should_import_etc2_astc() {
	if (GLOBAL_GET("rendering/textures/vram_compression/import_etc2_astc")) {
		return true;
	}
	// If the project settings override is not enabled, import
	// ETC2/ASTC only when the host operating system needs it.
	return OS::get_singleton()->get_preferred_texture_format() == OS::PREFERRED_TEXTURE_FORMAT_ETC2_ASTC;
}
