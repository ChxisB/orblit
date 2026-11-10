/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "image_loader_svg.h"

#include <thorvg.h>

#ifdef THREADS_ENABLED
#define TVG_THREADS 1
#else
#define TVG_THREADS 0
#endif

static Ref<ImageLoaderSVG> image_loader_svg;

void initialize_svg_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	if (tvg::Initializer::init(TVG_THREADS) != tvg::Result::Success) {
		return;
	}

	image_loader_svg.instantiate();
	ImageLoader::add_image_format_loader(image_loader_svg);
}

void uninitialize_svg_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}

	if (image_loader_svg.is_null()) {
		// It failed to initialize so it was not added.
		return;
	}

	ImageLoader::remove_image_format_loader(image_loader_svg);
	image_loader_svg.unref();
	tvg::Initializer::term();
}
