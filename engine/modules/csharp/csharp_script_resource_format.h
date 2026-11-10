/**************************************************************************/
/*  csharp_script_resource_format.h                                       */

#pragma once

#include "core/io/resource_loader.h"
#include "core/io/resource_saver.h"

class ResourceFormatLoaderCSharpScript : public ResourceFormatLoader {
	ORBLIT_SOFTCLASS(ResourceFormatLoaderCSharpScript, ResourceFormatLoader);

public:
	Ref<Resource> load(const String &p_path, const String &p_original_path = "", Error *r_error = nullptr, bool p_use_sub_threads = false, float *r_progress = nullptr, CacheMode p_cache_mode = CACHE_MODE_REUSE) override;
	void get_recognized_extensions(List<String> *p_extensions) const override;
	bool handles_type(const String &p_type) const override;
	String get_resource_type(const String &p_path) const override;
};

class ResourceFormatSaverCSharpScript : public ResourceFormatSaver {
	ORBLIT_SOFTCLASS(ResourceFormatSaverCSharpScript, ResourceFormatSaver);

public:
	Error save(const Ref<Resource> &p_resource, const String &p_path, uint32_t p_flags = 0) override;
	void get_recognized_extensions(const Ref<Resource> &p_resource, List<String> *p_extensions) const override;
	bool recognize(const Ref<Resource> &p_resource) const override;
};
