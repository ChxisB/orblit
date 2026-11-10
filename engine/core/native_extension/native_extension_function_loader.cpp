/**************************************************************************/
/*  native_extension_function_loader.cpp                                  */

#include "native_extension_function_loader.h"

#include "core/native_extension/native_extension.h"

Error NativeExtensionFunctionLoader::open_library(const String &p_path) {
	ERR_FAIL_COND_V_MSG(!p_path.begins_with("liborblit://"), ERR_FILE_NOT_FOUND, "Function based NativeExtensions should have a path starting with liborblit://");
	ERR_FAIL_COND_V_MSG(!initialization_function, ERR_DOES_NOT_EXIST, "Initialization function is required for function based NativeExtensions.");

	library_path = p_path;

	return OK;
}

Error NativeExtensionFunctionLoader::initialize(NativeExtensionInterfaceGetProcAddress p_get_proc_address, const Ref<NativeExtension> &p_extension, NativeExtensionInitialization *r_initialization) {
	ERR_FAIL_COND_V_MSG(!initialization_function, ERR_DOES_NOT_EXIST, "Initialization function is required for function based NativeExtensions.");
	NativeExtensionBool ret = initialization_function(p_get_proc_address, p_extension.ptr(), r_initialization);

	if (ret) {
		return OK;
	} else {
		ERR_FAIL_V_MSG(FAILED, "NativeExtension initialization function for '" + library_path + "' returned an error.");
	}
}

void NativeExtensionFunctionLoader::close_library() {
	initialization_function = nullptr;
	library_path.clear();
}

bool NativeExtensionFunctionLoader::is_library_open() const {
	return !library_path.is_empty();
}

bool NativeExtensionFunctionLoader::has_library_changed() const {
	return false;
}

bool NativeExtensionFunctionLoader::library_exists() const {
	return true;
}

void NativeExtensionFunctionLoader::set_initialization_function(NativeExtensionInitializationFunction p_initialization_function) {
	initialization_function = p_initialization_function;
}
