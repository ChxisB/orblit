/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_loader_libjpeg_turbo.h"
#include "movie_writer_mjpeg.h"

#include "core/object/class_registry.h"

static Ref<ImageLoaderLibJPEGTurbo> image_loader_libjpeg_turbo;
static MovieWriterMJPEG *writer_mjpeg = nullptr;

void initialize_jpg_module(ModuleInitializationLevel p_level) {
	switch (p_level) {
		case MODULE_INITIALIZATION_LEVEL_SYSTEMS: {
			if constexpr (ORBLIT_IS_CLASS_ENABLED(MovieWriterMJPEG)) {
				writer_mjpeg = memnew(MovieWriterMJPEG);
				MovieWriter::add_writer(writer_mjpeg);
			}
		} break;

		case MODULE_INITIALIZATION_LEVEL_SCENE: {
			image_loader_libjpeg_turbo.instantiate();
			ImageLoader::add_image_format_loader(image_loader_libjpeg_turbo);
		} break;

		default:
			break;
	}
}

void uninitialize_jpg_module(ModuleInitializationLevel p_level) {
	switch (p_level) {
		case MODULE_INITIALIZATION_LEVEL_SCENE: {
			ImageLoader::remove_image_format_loader(image_loader_libjpeg_turbo);
			image_loader_libjpeg_turbo.unref();
		} break;

		case MODULE_INITIALIZATION_LEVEL_SYSTEMS: {
			if constexpr (ORBLIT_IS_CLASS_ENABLED(MovieWriterMJPEG)) {
				memdelete(writer_mjpeg);
			}
		} break;

		default:
			break;
	}
}
