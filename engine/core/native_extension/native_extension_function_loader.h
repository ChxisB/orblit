/**************************************************************************/
/*  native_extension_function_loader.h                                    */

#pragma once

#include "core/native_extension/native_extension_loader.h"

class NativeExtension;

class NativeExtensionFunctionLoader : public NativeExtensionLoader {
	friend class NativeExtensionManager;
	friend class NativeExtension;

	String library_path;
	NativeExtensionInitializationFunction initialization_function = nullptr;

public:
	virtual Error open_library(const String &p_path) override;
	virtual Error initialize(NativeExtensionInterfaceGetProcAddress p_get_proc_address, const Ref<NativeExtension> &p_extension, NativeExtensionInitialization *r_initialization) override;
	virtual void close_library() override;
	virtual bool is_library_open() const override;
	virtual bool has_library_changed() const override;
	virtual bool library_exists() const override;

	void set_initialization_function(NativeExtensionInitializationFunction p_initialization_function);
};
