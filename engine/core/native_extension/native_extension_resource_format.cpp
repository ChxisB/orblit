/**************************************************************************/
/*  native_extension_resource_format.cpp                                  */

#include "native_extension_resource_format.h"

#include "core/native_extension/native_extension_manager.h"
#include "core/object/class_registry.h"

Ref<Resource> NativeExtensionResourceLoader::load(const String &p_path, const String &p_original_path, Error *r_error, bool p_use_sub_threads, float *r_progress, CacheMode p_cache_mode) {
	// We can't have two NativeExtension resource object representing the same library, because
	// loading (or unloading) a NativeExtension affects global data. So, we need reuse the same
	// object if one has already been loaded (even if caching is disabled at the resource
	// loader level).
	NativeExtensionManager *manager = NativeExtensionManager::get_singleton();
	if (manager->is_extension_loaded(p_path)) {
		return manager->get_extension(p_path);
	}

	Ref<NativeExtension> lib;
	Error err = load_native_extension_resource(p_path, lib);
	if (err != OK && r_error) {
		// Errors already logged in load_native_extension_resource().
		*r_error = err;
	}
	return lib;
}

void NativeExtensionResourceLoader::get_recognized_extensions(List<String> *p_extensions) const {
	p_extensions->push_back("oext");
}

bool NativeExtensionResourceLoader::handles_type(const String &p_type) const {
	return p_type == "NativeExtension";
}

String NativeExtensionResourceLoader::get_resource_type(const String &p_path) const {
	if (p_path.has_extension("oext")) {
		return "NativeExtension";
	}
	return "";
}

Error NativeExtensionResourceLoader::load_native_extension_resource(const String &p_path, Ref<NativeExtension> &p_extension) {
	ERR_FAIL_COND_V_MSG(p_extension.is_valid() && p_extension->is_library_open(), ERR_ALREADY_IN_USE, "Cannot load NativeExtension resource into already opened library.");

	NativeExtensionManager *extension_manager = NativeExtensionManager::get_singleton();

	NativeExtensionManager::LoadStatus status = extension_manager->load_extension(p_path);
	if (status != NativeExtensionManager::LOAD_STATUS_OK && status != NativeExtensionManager::LOAD_STATUS_ALREADY_LOADED) {
		// Errors already logged in load_extension().
		return FAILED;
	}

	p_extension = extension_manager->get_extension(p_path);
	return OK;
}

#ifdef TOOLS_ENABLED
void NativeExtensionResourceLoader::get_classes_used(const String &p_path, HashSet<StringName> *r_classes) {
	Ref<NativeExtension> gdext = ResourceLoader::load(p_path);
	if (gdext.is_null()) {
		return;
	}

	for (const StringName class_name : gdext->get_classes_used()) {
		if (ClassRegistry::class_exists(class_name)) {
			r_classes->insert(class_name);
		}
	}
}
#endif // TOOLS_ENABLED
