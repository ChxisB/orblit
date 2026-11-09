/**************************************************************************/
/*  translation_loader_po.h                                               */

#pragma once

#include "core/io/file_access.h"
#include "core/io/resource_loader.h"

class TranslationLoaderPO : public ResourceFormatLoader {
	ORBLIT_SOFTCLASS(TranslationLoaderPO, ResourceFormatLoader);

public:
	static Ref<Resource> load_translation(Ref<FileAccess> r_file, Error *r_error = nullptr);
	virtual Ref<Resource> load(const String &p_path, const String &p_original_path = "", Error *r_error = nullptr, bool p_use_sub_threads = false, float *r_progress = nullptr, CacheMode p_cache_mode = CACHE_MODE_REUSE) override;
	virtual void get_recognized_extensions(List<String> *p_extensions) const override;
	virtual bool handles_type(const String &p_type) const override;
	virtual String get_resource_type(const String &p_path) const override;

	// Treat translations as text/binary files, do not generate a `*.{po,mo}.uid` file.
	virtual ResourceUID::ID get_resource_uid(const String &p_path) const override { return ResourceUID::INVALID_ID; }
	virtual bool has_custom_uid_support() const override { return true; }
};
