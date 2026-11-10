/**************************************************************************/
/*  native_extension_loader.h                                             */

#pragma once

#include "core/object/ref_counted.h"

class NativeExtension;

class NativeExtensionLoader : public RefCounted {
	ORBLIT_SOFTCLASS(NativeExtensionLoader, RefCounted);

public:
	virtual Error open_library(const String &p_path) = 0;
	virtual Error initialize(NativeExtensionInterfaceGetProcAddress p_get_proc_address, const Ref<NativeExtension> &p_extension, NativeExtensionInitialization *r_initialization) = 0;
	virtual void close_library() = 0;
	virtual bool is_library_open() const = 0;
	virtual bool has_library_changed() const = 0;
	virtual bool library_exists() const = 0;
};
