/**************************************************************************/
/*  native_extension.h                                                    */

#pragma once

#include "core/io/resource.h"
#include "core/native_extension/native_extension_interface.gen.h"
#include "core/native_extension/native_extension_loader.h"

class NativeExtensionMethodBind;

class NativeExtension : public Resource {
	ORBLIT_CLASS(NativeExtension, Resource)

	friend class NativeExtensionManager;

	Ref<NativeExtensionLoader> loader;

	bool reloadable = false;

	struct Extension {
		ObjectNativeExtension native_extension;

#ifdef TOOLS_ENABLED
		bool is_reloading = false;
		HashMap<StringName, NativeExtensionMethodBind *> methods;
		HashSet<ObjectID> instances;

		struct InstanceState {
			List<Pair<String, Variant>> properties;
			bool is_placeholder = false;
		};
		HashMap<ObjectID, InstanceState> instance_state;
#endif
	};

	HashMap<StringName, Extension> extension_classes;

	static void _register_extension_class6(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, NativeExtensionConstStringNamePtr p_parent_class_name, const NativeExtensionClassCreationInfo6 *p_extension_funcs);
	static void _register_extension_class_method(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, const NativeExtensionClassMethodInfo *p_method_info);
	static void _register_extension_class_virtual_method(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, const NativeExtensionClassVirtualMethodInfo *p_method_info);
	static void _register_extension_class_integer_constant(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, NativeExtensionConstStringNamePtr p_enum_name, NativeExtensionConstStringNamePtr p_constant_name, NativeExtensionInt p_constant_value, NativeExtensionBool p_is_bitfield);
	static void _register_extension_class_property(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, const NativeExtensionPropertyInfo *p_info, NativeExtensionConstStringNamePtr p_setter, NativeExtensionConstStringNamePtr p_getter);
	static void _register_extension_class_property_indexed(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, const NativeExtensionPropertyInfo *p_info, NativeExtensionConstStringNamePtr p_setter, NativeExtensionConstStringNamePtr p_getter, NativeExtensionInt p_index);
	static void _register_extension_class_property_group(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, NativeExtensionConstStringNamePtr p_group_name, NativeExtensionConstStringNamePtr p_prefix);
	static void _register_extension_class_property_subgroup(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, NativeExtensionConstStringNamePtr p_subgroup_name, NativeExtensionConstStringNamePtr p_prefix);
	static void _register_extension_class_signal(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, NativeExtensionConstStringNamePtr p_signal_name, const NativeExtensionPropertyInfo *p_argument_info, NativeExtensionInt p_argument_count);
	static void _unregister_extension_class(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name);
	static void _get_library_path(NativeExtensionClassLibraryPtr p_library, NativeExtensionStringPtr r_path);
	static void _register_get_classes_used_callback(NativeExtensionClassLibraryPtr p_library, NativeExtensionEditorGetClassesUsedCallback p_callback);
	static void _register_main_loop_callbacks(NativeExtensionClassLibraryPtr p_library, const NativeExtensionMainLoopCallbacks *p_callbacks);

	NativeExtensionInitialization initialization;
	int32_t level_initialized = -1;

#ifdef TOOLS_ENABLED
	bool is_reloading = false;
	Vector<ObjectID> instance_bindings;
	NativeExtensionEditorGetClassesUsedCallback get_classes_used_callback = nullptr;

	static void _track_instance(void *p_user_data, void *p_instance);
	static void _untrack_instance(void *p_user_data, void *p_instance);

	void _clear_extension(Extension *p_extension);

	// Only called by NativeExtensionManager during the reload process.
	void prepare_reload();
	void finish_reload();
	void clear_instance_bindings();
#endif

	NativeExtensionMainLoopStartupCallback startup_callback = nullptr;
	NativeExtensionMainLoopShutdownCallback shutdown_callback = nullptr;
	NativeExtensionMainLoopFrameCallback frame_callback = nullptr;

	static inline HashMap<StringName, NativeExtensionInterfaceFunctionPtr> native_extension_interface_functions;

protected:
	static void register_members();

public:
	HashMap<String, String> class_icon_paths;

	virtual bool editor_can_reload_from_file() override { return false; } // Reloading is handled in a special way.

	static String get_extension_list_config_file();

	const Ref<NativeExtensionLoader> get_loader() const { return loader; }

	Error open_library(const String &p_path, const Ref<NativeExtensionLoader> &p_loader);
	void close_library();
	bool is_library_open() const;

	enum InitializationLevel {
		INITIALIZATION_LEVEL_CORE = NATIVE_EXTENSION_INITIALIZATION_CORE,
		INITIALIZATION_LEVEL_SYSTEMS = NATIVE_EXTENSION_INITIALIZATION_SYSTEMS,
		INITIALIZATION_LEVEL_SCENE = NATIVE_EXTENSION_INITIALIZATION_SCENE,
		INITIALIZATION_LEVEL_EDITOR = NATIVE_EXTENSION_INITIALIZATION_EDITOR
	};

protected:
public:
#ifdef TOOLS_ENABLED
	bool is_reloadable() const { return reloadable; }
	void set_reloadable(bool p_reloadable) { reloadable = p_reloadable; }

	bool has_library_changed() const;

	void track_instance_binding(Object *p_object);
	void untrack_instance_binding(Object *p_object);

	PackedStringArray get_classes_used() const;
#endif

	InitializationLevel get_minimum_library_initialization_level() const;
	void initialize_library(InitializationLevel p_level);
	void deinitialize_library(InitializationLevel p_level);

	static void register_interface_function(const StringName &p_function_name, NativeExtensionInterfaceFunctionPtr p_function_pointer);
	static NativeExtensionInterfaceFunctionPtr get_interface_function(const StringName &p_function_name);
	static void initialize_native_extensions();
	static void finalize_native_extensions();

	~NativeExtension();
};

VARIANT_ENUM_CAST(NativeExtension::InitializationLevel)

#ifdef TOOLS_ENABLED
class NativeExtensionEditorPlugins {
private:
	static inline Vector<StringName> extension_classes;

protected:
	friend class EditorNode;

	// Since this in core, we can't directly reference EditorNode, so it will
	// set these function pointers in its constructor.
	typedef void (*EditorPluginRegisterFunc)(const StringName &p_class_name);
	static inline EditorPluginRegisterFunc editor_node_add_plugin = nullptr;
	static inline EditorPluginRegisterFunc editor_node_remove_plugin = nullptr;

public:
	static void add_extension_class(const StringName &p_class_name);
	static void remove_extension_class(const StringName &p_class_name);

	static const Vector<StringName> &get_extension_classes() {
		return extension_classes;
	}
};

class NativeExtensionEditorHelp {
protected:
	friend class EditorHelp;

	// Similarly to EditorNode above, we need to be able to ask EditorHelp to parse
	// new documentation data. Note though that, differently from EditorHelp, this
	// is initialized even _before_ it gets instantiated, as we need to rely on
	// this method while initializing the engine.
	typedef void (*EditorHelpLoadXmlBufferFunc)(const uint8_t *p_buffer, int p_size);
	static inline EditorHelpLoadXmlBufferFunc editor_help_load_xml_buffer = nullptr;

	typedef void (*EditorHelpRemoveClassFunc)(const String &p_class);
	static inline EditorHelpRemoveClassFunc editor_help_remove_class = nullptr;

public:
	static void load_xml_buffer(const uint8_t *p_buffer, int p_size);
	static void remove_class(const String &p_class);
};

#endif // TOOLS_ENABLED
