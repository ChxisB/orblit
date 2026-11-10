/**************************************************************************/
/*  native_extension_resource_format.h                                    */

#pragma once

#include "core/io/resource_loader.h"
#include "core/native_extension/native_extension.h"

class NativeExtensionResourceLoader : public ResourceFormatLoader {
	ORBLIT_SOFTCLASS(NativeExtensionResourceLoader, ResourceFormatLoader);

public:
	static Error load_native_extension_resource(const String &p_path, Ref<NativeExtension> &p_extension);

	virtual Ref<Resource> load(const String &p_path, const String &p_original_path, Error *r_error, bool p_use_sub_threads = false, float *r_progress = nullptr, CacheMode p_cache_mode = CACHE_MODE_REUSE) override;
	virtual void get_recognized_extensions(List<String> *p_extensions) const override;
	virtual bool handles_type(const String &p_type) const override;
	virtual String get_resource_type(const String &p_path) const override;
#ifdef TOOLS_ENABLED
	virtual void get_classes_used(const String &p_path, HashSet<StringName> *r_classes) override;
#endif // TOOLS_ENABLED
};
