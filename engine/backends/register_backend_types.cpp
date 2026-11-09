/**************************************************************************/
/*  register_backend_types.cpp                                            */

#include "register_backend_types.h"

#include "core/io/resource_saver.h"
#include "backends/png/image_loader_png.h"
#include "backends/png/resource_saver_png.h"

#ifdef ACCESSKIT_ENABLED
#include "backends/accesskit/accessibility_system_accesskit.h"
#endif

static Ref<ImageLoaderPNG> image_loader_png;
static Ref<ResourceSaverPNG> resource_saver_png;

void register_core_driver_types() {
#ifdef ACCESSKIT_ENABLED
	AccessibilitySystemAccessKit::register_create_func();
#endif

	image_loader_png.instantiate();
	ImageLoader::add_image_format_loader(image_loader_png);

	resource_saver_png.instantiate();
	ResourceSaver::add_resource_format_saver(resource_saver_png);
}

void unregister_core_driver_types() {
	ImageLoader::remove_image_format_loader(image_loader_png);
	image_loader_png.unref();

	ResourceSaver::remove_resource_format_saver(resource_saver_png);
	resource_saver_png.unref();
}

void register_backend_types() {
}

void unregister_backend_types() {
}
