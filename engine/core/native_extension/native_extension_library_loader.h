/**************************************************************************/
/*  native_extension_library_loader.h                                     */

#pragma once

#include "core/io/config_file.h"
#include "core/native_extension/native_extension_loader.h"
#include "core/os/shared_object.h"

#include <functional>

class NativeExtensionLibraryLoader : public NativeExtensionLoader {
	ORBLIT_SOFTCLASS(NativeExtensionLibraryLoader, NativeExtensionLoader);

	friend class NativeExtensionManager;
	friend class NativeExtension;

private:
	String resource_path;

	void *library = nullptr; // pointer if valid.
	String library_path;
	String entry_symbol;

#ifdef TOOLS_ENABLED
	bool is_reloadable = false;
#endif

	Vector<SharedObject> library_dependencies;

	HashMap<String, String> class_icon_paths;

#ifdef TOOLS_ENABLED
	uint64_t resource_last_modified_time = 0;
	uint64_t library_last_modified_time = 0;

	void update_last_modified_time(uint64_t p_resource_last_modified_time, uint64_t p_library_last_modified_time) {
		resource_last_modified_time = p_resource_last_modified_time;
		library_last_modified_time = p_library_last_modified_time;
	}
#endif

public:
	static bool match_all_tags(PackedStringArray p_tags, std::function<bool(String)> p_has_feature);
	static String find_extension_library(const String &p_path, Ref<ConfigFile> p_config, std::function<bool(String)> p_has_feature, PackedStringArray *r_tags = nullptr);
	static Vector<SharedObject> find_extension_dependencies(const String &p_path, Ref<ConfigFile> p_config, std::function<bool(String)> p_has_feature);

	virtual Error open_library(const String &p_path) override;
	virtual Error initialize(NativeExtensionInterfaceGetProcAddress p_get_proc_address, const Ref<NativeExtension> &p_extension, NativeExtensionInitialization *r_initialization) override;
	virtual void close_library() override;
	virtual bool is_library_open() const override;
	virtual bool has_library_changed() const override;
	virtual bool library_exists() const override;

	Error parse_native_extension_file(const String &p_path);
};
