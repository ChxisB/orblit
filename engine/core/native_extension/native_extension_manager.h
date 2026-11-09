/**************************************************************************/
/*  native_extension_manager.h                                            */

#pragma once

#include "core/native_extension/native_extension.h"
#include "core/variant/native_ptr.h"

ORBLIT_VIRTUAL_NATIVE_PTR(NativeExtensionInitializationFunction)

class NativeExtensionManager : public Object {
	ORBLIT_CLASS(NativeExtensionManager, Object);

	int32_t level = -1;
	HashMap<String, Ref<NativeExtension>> native_extension_map;
	HashMap<String, String> native_extension_class_icon_paths;

	bool startup_callback_called = false;
	bool shutdown_callback_called = false;

	static void register_members();

	static inline NativeExtensionManager *singleton = nullptr;

public:
	enum LoadStatus {
		LOAD_STATUS_OK,
		LOAD_STATUS_FAILED,
		LOAD_STATUS_ALREADY_LOADED,
		LOAD_STATUS_NOT_LOADED,
		LOAD_STATUS_NEEDS_RESTART,
	};

private:
	LoadStatus _load_extension_internal(const Ref<NativeExtension> &p_extension, bool p_first_load);
	void _finish_load_extension(const Ref<NativeExtension> &p_extension);
	LoadStatus _unload_extension_internal(const Ref<NativeExtension> &p_extension);

#ifdef TOOLS_ENABLED
	static void _reload_all_scripts();
#endif

public:
	LoadStatus load_extension(const String &p_path);
	LoadStatus load_extension_from_function(const String &p_path, NativeExtensionPtr<const NativeExtensionInitializationFunction> p_init_func);
	LoadStatus load_extension_with_loader(const String &p_path, const Ref<NativeExtensionLoader> &p_loader);
	LoadStatus reload_extension(const String &p_path);
	LoadStatus unload_extension(const String &p_path);
	bool is_extension_loaded(const String &p_path) const;
	Vector<String> get_loaded_extensions() const;
	Ref<NativeExtension> get_extension(const String &p_path);

	bool class_has_icon_path(const String &p_class) const;
	String class_get_icon_path(const String &p_class) const;

	void initialize_extensions(NativeExtension::InitializationLevel p_level);
	void deinitialize_extensions(NativeExtension::InitializationLevel p_level);

#ifdef TOOLS_ENABLED
	void track_instance_binding(void *p_token, Object *p_object);
	void untrack_instance_binding(void *p_token, Object *p_object);
#endif

	static NativeExtensionManager *get_singleton();

	void load_extensions();
	void reload_extensions();
	bool ensure_extensions_loaded(const HashSet<String> &p_extensions);

	void startup();
	void shutdown();
	void frame();

	NativeExtensionManager();
	~NativeExtensionManager();
};

VARIANT_ENUM_CAST(NativeExtensionManager::LoadStatus)
