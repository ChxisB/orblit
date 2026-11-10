/**************************************************************************/
/*  native_extension.cpp                                                  */

#include "native_extension.h"

#include "core/config/project_settings.h"
#include "core/native_extension/native_extension_library_loader.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "core/object/method_bind.h"

extern void native_extension_setup_interface();
extern NativeExtensionInterfaceFunctionPtr native_extension_get_proc_address(const char *p_name);

typedef NativeExtensionBool (*NativeExtensionLegacyInitializationFunction)(void *p_interface, NativeExtensionClassLibraryPtr p_library, NativeExtensionInitialization *r_initialization);

String NativeExtension::get_extension_list_config_file() {
	return ProjectSettings::get_singleton()->get_project_data_path().path_join("extension_list.cfg");
}

class NativeExtensionMethodBind : public MethodBind {
	NativeExtensionClassMethodCall call_func;
	NativeExtensionClassMethodValidatedCall validated_call_func;
	NativeExtensionClassMethodPtrCall ptrcall_func;
	void *method_userdata;
	bool vararg;
	uint32_t argument_count;
	PropertyInfo return_value_info;
	OrblitTypeInfo::Metadata return_value_metadata;
	List<PropertyInfo> arguments_info;
	List<OrblitTypeInfo::Metadata> arguments_metadata;

#ifdef TOOLS_ENABLED
	friend class NativeExtension;

	StringName name;
	bool is_reloading = false;
	bool valid = true;
#endif

protected:
	virtual Variant::Type _gen_argument_type(int p_arg) const override {
		if (p_arg < 0) {
			return return_value_info.type;
		} else {
			return arguments_info.get(p_arg).type;
		}
	}
	virtual PropertyInfo _gen_argument_type_info(int p_arg) const override {
		if (p_arg < 0) {
			return return_value_info;
		} else {
			return arguments_info.get(p_arg);
		}
	}

public:
#ifdef TOOLS_ENABLED
	virtual bool is_valid() const override { return valid; }
#endif

#ifdef DEBUG_ENABLED
	virtual OrblitTypeInfo::Metadata get_argument_meta(int p_arg) const override {
		if (p_arg < 0) {
			return return_value_metadata;
		} else {
			return arguments_metadata.get(p_arg);
		}
	}
#endif // DEBUG_ENABLED

	virtual Variant call(Object *p_object, const Variant **p_args, int p_arg_count, Callable::CallError &r_error) const override {
#ifdef TOOLS_ENABLED
		ERR_FAIL_COND_V_MSG(!valid, Variant(), vformat("Cannot call invalid NativeExtension method bind '%s'. It's probably cached - you may need to restart Orblit.", name));
		ERR_FAIL_COND_V_MSG(p_object && p_object->is_extension_placeholder(), Variant(), vformat("Cannot call NativeExtension method bind '%s' on placeholder instance.", name));
#endif
		Variant ret;
		NativeExtensionClassInstancePtr extension_instance = is_static() ? nullptr : p_object->_get_extension_instance();
		NativeExtensionCallError ce{ NATIVE_EXTENSION_CALL_OK, 0, 0 };
		call_func(method_userdata, extension_instance, reinterpret_cast<NativeExtensionConstVariantPtr *>(p_args), p_arg_count, (NativeExtensionVariantPtr)&ret, &ce);
		r_error.error = Callable::CallError::Error(ce.error);
		r_error.argument = ce.argument;
		r_error.expected = ce.expected;
		return ret;
	}
	virtual void validated_call(Object *p_object, const Variant **p_args, Variant *r_ret) const override {
#ifdef TOOLS_ENABLED
		ERR_FAIL_COND_MSG(!valid, vformat("Cannot call invalid NativeExtension method bind '%s'. It's probably cached - you may need to restart Orblit.", name));
		ERR_FAIL_COND_MSG(p_object && p_object->is_extension_placeholder(), vformat("Cannot call NativeExtension method bind '%s' on placeholder instance.", name));
#endif
		ERR_FAIL_COND_MSG(vararg, "Vararg methods don't have validated call support. This is most likely an engine bug.");
		NativeExtensionClassInstancePtr extension_instance = is_static() ? nullptr : p_object->_get_extension_instance();

		if (validated_call_func) {
			// This is added here, but it's unlikely to be provided by most extensions.
			validated_call_func(method_userdata, extension_instance, reinterpret_cast<NativeExtensionConstVariantPtr *>(p_args), (NativeExtensionVariantPtr)r_ret);
		} else {
			// If not provided, go via ptrcall, which is faster than resorting to regular call.
			const void **argptrs = (const void **)alloca(argument_count * sizeof(void *));
			for (uint32_t i = 0; i < argument_count; i++) {
				argptrs[i] = VariantInternal::get_opaque_pointer(p_args[i]);
			}

			void *ret_opaque = nullptr;
			if (r_ret) {
				VariantInternal::initialize(r_ret, return_value_info.type);
				ret_opaque = r_ret->get_type() == Variant::NIL ? r_ret : VariantInternal::get_opaque_pointer(r_ret);
			}

			ptrcall_func(method_userdata, extension_instance, reinterpret_cast<NativeExtensionConstTypePtr *>(argptrs), (NativeExtensionTypePtr)ret_opaque);

			if (r_ret && r_ret->get_type() == Variant::OBJECT) {
				VariantInternal::update_object_id(r_ret);
			}
		}
	}

	virtual void ptrcall(Object *p_object, const void **p_args, void *r_ret) const override {
#ifdef TOOLS_ENABLED
		ERR_FAIL_COND_MSG(!valid, vformat("Cannot call invalid NativeExtension method bind '%s'. It's probably cached - you may need to restart Orblit.", name));
		ERR_FAIL_COND_MSG(p_object && p_object->is_extension_placeholder(), vformat("Cannot call NativeExtension method bind '%s' on placeholder instance.", name));
#endif
		ERR_FAIL_COND_MSG(vararg, "Vararg methods don't have ptrcall support. This is most likely an engine bug.");
		NativeExtensionClassInstancePtr extension_instance = is_static() ? nullptr : p_object->_get_extension_instance();
		ptrcall_func(method_userdata, extension_instance, reinterpret_cast<NativeExtensionConstTypePtr *>(p_args), (NativeExtensionTypePtr)r_ret);
	}

	virtual bool is_vararg() const override {
		return vararg;
	}

#ifdef TOOLS_ENABLED
	bool try_update(const NativeExtensionClassMethodInfo *p_method_info) {
		if (is_static() != (bool)(p_method_info->method_flags & NATIVE_EXTENSION_METHOD_FLAG_STATIC)) {
			return false;
		}

		if (vararg != (bool)(p_method_info->method_flags & NATIVE_EXTENSION_METHOD_FLAG_VARARG)) {
			return false;
		}

		if (has_return() != (bool)p_method_info->has_return_value) {
			return false;
		}

		if (has_return() && return_value_info.type != (Variant::Type)p_method_info->return_value_info->type) {
			return false;
		}

		if (argument_count != p_method_info->argument_count) {
			return false;
		}

		List<PropertyInfo>::ConstIterator itr = arguments_info.begin();
		for (uint32_t i = 0; i < p_method_info->argument_count; ++itr, ++i) {
			if (itr->type != (Variant::Type)p_method_info->arguments_info[i].type) {
				return false;
			}
		}

		update(p_method_info);
		return true;
	}
#endif

	void update(const NativeExtensionClassMethodInfo *p_method_info) {
#ifdef TOOLS_ENABLED
		name = *reinterpret_cast<StringName *>(p_method_info->name);
#endif
		method_userdata = p_method_info->method_userdata;
		call_func = p_method_info->call_func;
		validated_call_func = nullptr;
		ptrcall_func = p_method_info->ptrcall_func;
		set_name(*reinterpret_cast<StringName *>(p_method_info->name));

		if (p_method_info->has_return_value) {
			return_value_info = PropertyInfo(*p_method_info->return_value_info);
			return_value_metadata = OrblitTypeInfo::Metadata(p_method_info->return_value_metadata);
		}

		arguments_info.clear();
		arguments_metadata.clear();
		for (uint32_t i = 0; i < p_method_info->argument_count; i++) {
			arguments_info.push_back(PropertyInfo(p_method_info->arguments_info[i]));
			arguments_metadata.push_back(OrblitTypeInfo::Metadata(p_method_info->arguments_metadata[i]));
		}

		set_hint_flags(p_method_info->method_flags);
		argument_count = p_method_info->argument_count;
		vararg = p_method_info->method_flags & NATIVE_EXTENSION_METHOD_FLAG_VARARG;
		_set_returns(p_method_info->has_return_value);
		_set_const(p_method_info->method_flags & NATIVE_EXTENSION_METHOD_FLAG_CONST);
		_set_static(p_method_info->method_flags & NATIVE_EXTENSION_METHOD_FLAG_STATIC);
#ifdef DEBUG_ENABLED
		_generate_argument_types(p_method_info->argument_count);
#endif // DEBUG_ENABLED
		set_argument_count(p_method_info->argument_count);

		Vector<Variant> defargs;
		defargs.resize(p_method_info->default_argument_count);
		for (uint32_t i = 0; i < p_method_info->default_argument_count; i++) {
			defargs.write[i] = *static_cast<Variant *>(p_method_info->default_arguments[i]);
		}

		set_default_arguments(defargs);
	}

	explicit NativeExtensionMethodBind(const NativeExtensionClassMethodInfo *p_method_info) {
		update(p_method_info);
	}
};

void NativeExtension::_register_extension_class6(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, NativeExtensionConstStringNamePtr p_parent_class_name, const NativeExtensionClassCreationInfo6 *p_extension_funcs) {
	NativeExtension *self = reinterpret_cast<NativeExtension *>(p_library);

	StringName class_name = *reinterpret_cast<const StringName *>(p_class_name);
	StringName parent_class_name = *reinterpret_cast<const StringName *>(p_parent_class_name);
	ERR_FAIL_COND_MSG(!String(class_name).is_valid_unicode_identifier(), vformat("Attempt to register extension class '%s', which is not a valid class identifier.", class_name));
	ERR_FAIL_COND_MSG(ClassRegistry::class_exists(class_name), vformat("Attempt to register extension class '%s', which appears to be already registered.", class_name));

	Extension *parent_extension = nullptr;

	if (self->extension_classes.has(parent_class_name)) {
		parent_extension = &self->extension_classes[parent_class_name];
	} else if (ClassRegistry::class_exists(parent_class_name)) {
		if (ClassRegistry::get_api_type(parent_class_name) == ClassRegistry::API_EXTENSION || ClassRegistry::get_api_type(parent_class_name) == ClassRegistry::API_EDITOR_EXTENSION) {
			ERR_PRINT("Unimplemented yet");
			//inheriting from another extension
		} else {
			//inheriting from engine class
		}
	} else {
		ERR_FAIL_MSG(vformat("Attempt to register an extension class '%s' using non-existing parent class '%s'.", String(class_name), String(parent_class_name)));
	}

#ifdef TOOLS_ENABLED
	Extension *extension = nullptr;
	bool is_runtime = (bool)p_extension_funcs->is_runtime;
	if (self->is_reloading && self->extension_classes.has(class_name)) {
		extension = &self->extension_classes[class_name];
		if (!parent_extension && parent_class_name != extension->native_extension.parent_class_name) {
			ERR_FAIL_MSG(vformat("NativeExtension class '%s' cannot change parent type from '%s' to '%s' on hot reload. Restart Orblit for this change to take effect.", class_name, extension->native_extension.parent_class_name, parent_class_name));
		}
		if (extension->native_extension.is_runtime != is_runtime) {
			ERR_PRINT(vformat("NativeExtension class '%s' cannot change to/from runtime class on hot reload. Restart Orblit for this change to take effect.", class_name));
			is_runtime = extension->native_extension.is_runtime;
		}
		extension->is_reloading = false;
	} else {
		self->extension_classes[class_name] = Extension();
		extension = &self->extension_classes[class_name];
	}
#else
	self->extension_classes[class_name] = Extension();
	Extension *extension = &self->extension_classes[class_name];
#endif

	if (parent_extension) {
		extension->native_extension.parent = &parent_extension->native_extension;
		parent_extension->native_extension.children.push_back(&extension->native_extension);
	}

	if (self->reloadable && p_extension_funcs->recreate_instance_func == nullptr) {
		bool can_create_class = (bool)p_extension_funcs->create_instance_func;
		if (can_create_class) {
			ERR_PRINT(vformat("Extension marked as reloadable, but attempted to register class '%s' which doesn't support reloading. Perhaps your language binding don't support it? Reloading disabled for this extension.", class_name));
			self->reloadable = false;
		}
	}

	extension->native_extension.library = self;
	extension->native_extension.parent_class_name = parent_class_name;
	extension->native_extension.class_name = class_name;
	extension->native_extension.editor_class = self->level_initialized == INITIALIZATION_LEVEL_EDITOR;
	extension->native_extension.is_virtual = p_extension_funcs->is_virtual;
	extension->native_extension.is_abstract = p_extension_funcs->is_abstract;
	extension->native_extension.is_exposed = p_extension_funcs->is_exposed;
#ifdef TOOLS_ENABLED
	extension->native_extension.is_runtime = is_runtime;
#endif
	extension->native_extension.set = p_extension_funcs->set_func;
	extension->native_extension.get = p_extension_funcs->get_func;
	extension->native_extension.get_property_list = p_extension_funcs->get_property_list_func;
	extension->native_extension.free_property_list2 = p_extension_funcs->free_property_list_func;
	extension->native_extension.property_can_revert = p_extension_funcs->property_can_revert_func;
	extension->native_extension.property_get_revert = p_extension_funcs->property_get_revert_func;
	extension->native_extension.validate_property = p_extension_funcs->validate_property_func;
	extension->native_extension.notification2 = p_extension_funcs->notification_func;
	extension->native_extension.to_string = p_extension_funcs->to_string_func;
	extension->native_extension.reference = p_extension_funcs->reference_func;
	extension->native_extension.unreference = p_extension_funcs->unreference_func;
	extension->native_extension.class_userdata = p_extension_funcs->class_userdata;
	extension->native_extension.create_instance3 = p_extension_funcs->create_instance_func;
	extension->native_extension.free_instance = p_extension_funcs->free_instance_func;
	extension->native_extension.recreate_instance = p_extension_funcs->recreate_instance_func;
	extension->native_extension.get_virtual2 = p_extension_funcs->get_virtual_func;
	extension->native_extension.get_virtual_call_data2 = p_extension_funcs->get_virtual_call_data_func;
	extension->native_extension.call_virtual_with_data = p_extension_funcs->call_virtual_with_data_func;

	extension->native_extension.reloadable = self->reloadable;
#ifdef TOOLS_ENABLED
	if (extension->native_extension.reloadable) {
		extension->native_extension.tracking_userdata = extension;
		extension->native_extension.track_instance = &NativeExtension::_track_instance;
		extension->native_extension.untrack_instance = &NativeExtension::_untrack_instance;
	} else {
		extension->native_extension.tracking_userdata = nullptr;
		extension->native_extension.track_instance = nullptr;
		extension->native_extension.untrack_instance = nullptr;
	}
#endif

	ClassRegistry::register_extension_class(&extension->native_extension);

	if (p_extension_funcs->icon_path != nullptr) {
		const String icon_path = *reinterpret_cast<const String *>(p_extension_funcs->icon_path);
		if (!icon_path.is_empty()) {
			self->class_icon_paths[class_name] = icon_path;
		}
	}
}

void NativeExtension::_register_extension_class_method(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, const NativeExtensionClassMethodInfo *p_method_info) {
	NativeExtension *self = reinterpret_cast<NativeExtension *>(p_library);

	StringName class_name = *reinterpret_cast<const StringName *>(p_class_name);
	StringName method_name = *reinterpret_cast<const StringName *>(p_method_info->name);
	ERR_FAIL_COND_MSG(!self->extension_classes.has(class_name), vformat("Attempt to register extension method '%s' for unexisting class '%s'.", String(method_name), class_name));

	bool is_from_old_extension = false;

#ifdef TOOLS_ENABLED
	Extension *extension = &self->extension_classes[class_name];
	NativeExtensionMethodBind *method = nullptr;

	// If the extension is still marked as reloading, that means it failed to register again.
	if (extension->is_reloading) {
		return;
	}

	if (self->is_reloading && extension->methods.has(method_name)) {
		method = extension->methods[method_name];

		// Try to update the method bind. If it doesn't work (because it's incompatible) then
		// mark as invalid and create a new one.
		if (!method->is_reloading || !method->try_update(p_method_info)) {
			method->valid = false;
			method = nullptr;
		}
	}

	if (method == nullptr) {
		method = memnew(NativeExtensionMethodBind(p_method_info));
		method->set_instance_class(class_name);
		extension->methods[method_name] = method;
	} else {
		method->is_reloading = false;
		is_from_old_extension = true;
	}
#else
	NativeExtensionMethodBind *method = memnew(NativeExtensionMethodBind(p_method_info));
	method->set_instance_class(class_name);
#endif

	ClassRegistry::bind_method_custom(class_name, method, !is_from_old_extension);
}

void NativeExtension::_register_extension_class_virtual_method(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, const NativeExtensionClassVirtualMethodInfo *p_method_info) {
	StringName class_name = *reinterpret_cast<const StringName *>(p_class_name);
	ClassRegistry::add_extension_class_virtual_method(class_name, p_method_info);
}

void NativeExtension::_register_extension_class_integer_constant(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, NativeExtensionConstStringNamePtr p_enum_name, NativeExtensionConstStringNamePtr p_constant_name, NativeExtensionInt p_constant_value, NativeExtensionBool p_is_bitfield) {
	NativeExtension *self = reinterpret_cast<NativeExtension *>(p_library);

	StringName class_name = *reinterpret_cast<const StringName *>(p_class_name);
	StringName enum_name = *reinterpret_cast<const StringName *>(p_enum_name);
	StringName constant_name = *reinterpret_cast<const StringName *>(p_constant_name);
	ERR_FAIL_COND_MSG(!self->extension_classes.has(class_name), vformat("Attempt to register extension constant '%s' for unexisting class '%s'.", constant_name, class_name));

#ifdef TOOLS_ENABLED
	// If the extension is still marked as reloading, that means it failed to register again.
	Extension *extension = &self->extension_classes[class_name];
	if (extension->is_reloading) {
		return;
	}
#endif

	ClassRegistry::bind_integer_constant(class_name, enum_name, constant_name, p_constant_value, p_is_bitfield);
}

void NativeExtension::_register_extension_class_property(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, const NativeExtensionPropertyInfo *p_info, NativeExtensionConstStringNamePtr p_setter, NativeExtensionConstStringNamePtr p_getter) {
	_register_extension_class_property_indexed(p_library, p_class_name, p_info, p_setter, p_getter, -1);
}

void NativeExtension::_register_extension_class_property_indexed(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, const NativeExtensionPropertyInfo *p_info, NativeExtensionConstStringNamePtr p_setter, NativeExtensionConstStringNamePtr p_getter, NativeExtensionInt p_index) {
	NativeExtension *self = reinterpret_cast<NativeExtension *>(p_library);

	StringName class_name = *reinterpret_cast<const StringName *>(p_class_name);
	StringName setter = *reinterpret_cast<const StringName *>(p_setter);
	StringName getter = *reinterpret_cast<const StringName *>(p_getter);
	String property_name = *reinterpret_cast<const StringName *>(p_info->name);
	ERR_FAIL_COND_MSG(!self->extension_classes.has(class_name), vformat("Attempt to register extension class property '%s' for unexisting class '%s'.", property_name, class_name));

#ifdef TOOLS_ENABLED
	// If the extension is still marked as reloading, that means it failed to register again.
	Extension *extension = &self->extension_classes[class_name];
	if (extension->is_reloading) {
		return;
	}
#endif

	PropertyInfo pinfo(*p_info);

	ClassRegistry::add_property(class_name, pinfo, setter, getter, p_index);
}

void NativeExtension::_register_extension_class_property_group(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, NativeExtensionConstStringPtr p_group_name, NativeExtensionConstStringPtr p_prefix) {
	NativeExtension *self = reinterpret_cast<NativeExtension *>(p_library);

	StringName class_name = *reinterpret_cast<const StringName *>(p_class_name);
	String group_name = *reinterpret_cast<const String *>(p_group_name);
	String prefix = *reinterpret_cast<const String *>(p_prefix);
	ERR_FAIL_COND_MSG(!self->extension_classes.has(class_name), vformat("Attempt to register extension class property group '%s' for unexisting class '%s'.", group_name, class_name));

#ifdef TOOLS_ENABLED
	// If the extension is still marked as reloading, that means it failed to register again.
	Extension *extension = &self->extension_classes[class_name];
	if (extension->is_reloading) {
		return;
	}
#endif

	ClassRegistry::add_property_group(class_name, group_name, prefix);
}

void NativeExtension::_register_extension_class_property_subgroup(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, NativeExtensionConstStringPtr p_subgroup_name, NativeExtensionConstStringPtr p_prefix) {
	NativeExtension *self = reinterpret_cast<NativeExtension *>(p_library);

	StringName class_name = *reinterpret_cast<const StringName *>(p_class_name);
	String subgroup_name = *reinterpret_cast<const String *>(p_subgroup_name);
	String prefix = *reinterpret_cast<const String *>(p_prefix);
	ERR_FAIL_COND_MSG(!self->extension_classes.has(class_name), vformat("Attempt to register extension class property subgroup '%s' for unexisting class '%s'.", subgroup_name, class_name));

#ifdef TOOLS_ENABLED
	// If the extension is still marked as reloading, that means it failed to register again.
	Extension *extension = &self->extension_classes[class_name];
	if (extension->is_reloading) {
		return;
	}
#endif

	ClassRegistry::add_property_subgroup(class_name, subgroup_name, prefix);
}

void NativeExtension::_register_extension_class_signal(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name, NativeExtensionConstStringNamePtr p_signal_name, const NativeExtensionPropertyInfo *p_argument_info, NativeExtensionInt p_argument_count) {
	NativeExtension *self = reinterpret_cast<NativeExtension *>(p_library);

	StringName class_name = *reinterpret_cast<const StringName *>(p_class_name);
	StringName signal_name = *reinterpret_cast<const StringName *>(p_signal_name);
	ERR_FAIL_COND_MSG(!self->extension_classes.has(class_name), vformat("Attempt to register extension class signal '%s' for unexisting class '%s'.", signal_name, class_name));

#ifdef TOOLS_ENABLED
	// If the extension is still marked as reloading, that means it failed to register again.
	Extension *extension = &self->extension_classes[class_name];
	if (extension->is_reloading) {
		return;
	}
#endif

	MethodInfo s;
	s.name = signal_name;
	for (int i = 0; i < p_argument_count; i++) {
		PropertyInfo arg(p_argument_info[i]);
		s.arguments.push_back(arg);
	}
	ClassRegistry::add_signal(class_name, s);
}

void NativeExtension::_unregister_extension_class(NativeExtensionClassLibraryPtr p_library, NativeExtensionConstStringNamePtr p_class_name) {
	NativeExtension *self = reinterpret_cast<NativeExtension *>(p_library);

	StringName class_name = *reinterpret_cast<const StringName *>(p_class_name);
	ERR_FAIL_COND_MSG(!self->extension_classes.has(class_name), vformat("Attempt to unregister unexisting extension class '%s'.", class_name));

	Extension *ext = &self->extension_classes[class_name];
#ifdef TOOLS_ENABLED
	if (ext->is_reloading) {
		self->_clear_extension(ext);
	}
#endif
	ERR_FAIL_COND_MSG(ext->native_extension.children.size(), vformat("Attempt to unregister class '%s' while other extension classes inherit from it.", class_name));

	ClassRegistry::unregister_extension_class(class_name);

	if (ext->native_extension.parent != nullptr) {
		ext->native_extension.parent->children.erase(&ext->native_extension);
	}

#ifdef TOOLS_ENABLED
	if (!ext->is_reloading) {
		self->extension_classes.erase(class_name);
	}

	NativeExtensionEditorHelp::remove_class(class_name);
#else
	self->extension_classes.erase(class_name);
#endif
}

void NativeExtension::_get_library_path(NativeExtensionClassLibraryPtr p_library, NativeExtensionUninitializedStringPtr r_path) {
	NativeExtension *self = reinterpret_cast<NativeExtension *>(p_library);

	Ref<NativeExtensionLibraryLoader> library_loader = self->loader;
	String library_path;
	if (library_loader.is_valid()) {
		library_path = library_loader->library_path;
	}

	memnew_placement(r_path, String(library_path));
}

void NativeExtension::_register_get_classes_used_callback(NativeExtensionClassLibraryPtr p_library, NativeExtensionEditorGetClassesUsedCallback p_callback) {
#ifdef TOOLS_ENABLED
	NativeExtension *self = reinterpret_cast<NativeExtension *>(p_library);
	self->get_classes_used_callback = p_callback;
#endif
}

void NativeExtension::_register_main_loop_callbacks(NativeExtensionClassLibraryPtr p_library, const NativeExtensionMainLoopCallbacks *p_callbacks) {
	NativeExtension *self = reinterpret_cast<NativeExtension *>(p_library);
	self->startup_callback = p_callbacks->startup_func;
	self->shutdown_callback = p_callbacks->shutdown_func;
	self->frame_callback = p_callbacks->frame_func;
}

void NativeExtension::register_interface_function(const StringName &p_function_name, NativeExtensionInterfaceFunctionPtr p_function_pointer) {
	ERR_FAIL_COND_MSG(native_extension_interface_functions.has(p_function_name), vformat("Attempt to register interface function '%s', which appears to be already registered.", p_function_name));
	native_extension_interface_functions.insert(p_function_name, p_function_pointer);
}

NativeExtensionInterfaceFunctionPtr NativeExtension::get_interface_function(const StringName &p_function_name) {
	NativeExtensionInterfaceFunctionPtr *function = native_extension_interface_functions.getptr(p_function_name);
	ERR_FAIL_NULL_V_MSG(function, nullptr, vformat("Attempt to get non-existent interface function: '%s'.", String(p_function_name)));
	return *function;
}

Error NativeExtension::open_library(const String &p_path, const Ref<NativeExtensionLoader> &p_loader) {
	ERR_FAIL_COND_V_MSG(p_loader.is_null(), FAILED, "Can't open NativeExtension without a loader.");
	loader = p_loader;

	Error err = loader->open_library(p_path);
	if (err == ERR_SKIP) {
		return err;
	}

	ERR_FAIL_COND_V_MSG(err == ERR_FILE_NOT_FOUND, err, vformat("NativeExtension dynamic library not found: '%s'.", p_path));
	ERR_FAIL_COND_V_MSG(err != OK, err, vformat("Can't open NativeExtension dynamic library: '%s'.", p_path));

	err = loader->initialize(&native_extension_get_proc_address, this, &initialization);

	if (err != OK) {
		// Errors already logged in initialize().
		loader->close_library();
		return err;
	}

	level_initialized = -1;

	return OK;
}

void NativeExtension::close_library() {
	ERR_FAIL_COND(!is_library_open());
	loader->close_library();

	class_icon_paths.clear();

#ifdef TOOLS_ENABLED
	instance_bindings.clear();
#endif
}

bool NativeExtension::is_library_open() const {
	return loader.is_valid() && loader->is_library_open();
}

NativeExtension::InitializationLevel NativeExtension::get_minimum_library_initialization_level() const {
	ERR_FAIL_COND_V(!is_library_open(), INITIALIZATION_LEVEL_CORE);
	return InitializationLevel(initialization.minimum_initialization_level);
}

void NativeExtension::initialize_library(InitializationLevel p_level) {
	ERR_FAIL_COND(!is_library_open());
	ERR_FAIL_COND_MSG(p_level <= int32_t(level_initialized), vformat("Level '%d' must be higher than the current level '%d'", p_level, level_initialized));

	level_initialized = int32_t(p_level);

	ERR_FAIL_NULL(initialization.initialize);

	initialization.initialize(initialization.userdata, NativeExtensionInitializationLevel(p_level));
}
void NativeExtension::deinitialize_library(InitializationLevel p_level) {
	ERR_FAIL_COND(!is_library_open());
	ERR_FAIL_COND(p_level > int32_t(level_initialized));

	level_initialized = int32_t(p_level) - 1;

	ERR_FAIL_NULL(initialization.deinitialize);

	initialization.deinitialize(initialization.userdata, NativeExtensionInitializationLevel(p_level));
}

void NativeExtension::register_members() {
	ClassRegistry::bind_method(D_METHOD("is_library_open"), &NativeExtension::is_library_open);
	ClassRegistry::bind_method(D_METHOD("get_minimum_library_initialization_level"), &NativeExtension::get_minimum_library_initialization_level);

	BIND_ENUM_CONSTANT(INITIALIZATION_LEVEL_CORE);
	BIND_ENUM_CONSTANT(INITIALIZATION_LEVEL_SYSTEMS);
	BIND_ENUM_CONSTANT(INITIALIZATION_LEVEL_SCENE);
	BIND_ENUM_CONSTANT(INITIALIZATION_LEVEL_EDITOR);
}

NativeExtension::~NativeExtension() {
	if (is_library_open()) {
		close_library();
	}
}

void NativeExtension::initialize_native_extensions() {
	native_extension_setup_interface();

	register_interface_function("class_registry_register_extension_class6", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_register_extension_class6);
	register_interface_function("class_registry_register_extension_class_method", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_register_extension_class_method);
	register_interface_function("class_registry_register_extension_class_virtual_method", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_register_extension_class_virtual_method);
	register_interface_function("class_registry_register_extension_class_integer_constant", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_register_extension_class_integer_constant);
	register_interface_function("class_registry_register_extension_class_property", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_register_extension_class_property);
	register_interface_function("class_registry_register_extension_class_property_indexed", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_register_extension_class_property_indexed);
	register_interface_function("class_registry_register_extension_class_property_group", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_register_extension_class_property_group);
	register_interface_function("class_registry_register_extension_class_property_subgroup", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_register_extension_class_property_subgroup);
	register_interface_function("class_registry_register_extension_class_signal", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_register_extension_class_signal);
	register_interface_function("class_registry_unregister_extension_class", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_unregister_extension_class);
	register_interface_function("get_library_path", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_get_library_path);
	register_interface_function("editor_register_get_classes_used_callback", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_register_get_classes_used_callback);
	register_interface_function("register_main_loop_callbacks", (NativeExtensionInterfaceFunctionPtr)&NativeExtension::_register_main_loop_callbacks);
}

void NativeExtension::finalize_native_extensions() {
	native_extension_interface_functions.clear();
}

#ifdef TOOLS_ENABLED

bool NativeExtension::has_library_changed() const {
	return loader->has_library_changed();
}

void NativeExtension::prepare_reload() {
	is_reloading = true;

	for (KeyValue<StringName, Extension> &E : extension_classes) {
		E.value.is_reloading = true;

		for (KeyValue<StringName, NativeExtensionMethodBind *> &M : E.value.methods) {
			M.value->is_reloading = true;
		}

		for (const ObjectID &obj_id : E.value.instances) {
			Object *obj = ObjectRegistry::get_instance(obj_id);
			if (!obj) {
				continue;
			}

			// Store instance state so it can be restored after reload.
			List<Pair<String, Variant>> state;
			List<PropertyInfo> prop_list;
			obj->get_property_list(&prop_list);
			for (const PropertyInfo &P : prop_list) {
				if (!(P.usage & PROPERTY_USAGE_STORAGE)) {
					continue;
				}

				Variant value = obj->get(P.name);
				Variant default_value = ClassRegistry::class_get_default_property_value(obj->get_class_name(), P.name);

				if (default_value.get_type() != Variant::NIL && bool(Variant::evaluate(Variant::OP_EQUAL, value, default_value))) {
					continue;
				}

				if (P.type == Variant::OBJECT && value.is_zero() && !(P.usage & PROPERTY_USAGE_STORE_IF_NULL)) {
					continue;
				}

				state.push_back(Pair<String, Variant>(P.name, value));
			}
			E.value.instance_state[obj_id] = {
				std::move(state), // List<Pair<String, Variant>> properties;
				obj->is_extension_placeholder(), // bool is_placeholder;
			};
		}
	}
}

void NativeExtension::_clear_extension(Extension *p_extension) {
	// Clear out hierarchy information because it may change.
	p_extension->native_extension.parent = nullptr;
	p_extension->native_extension.children.clear();

	// Clear all objects of any NativeExtension data. It will become its native parent class
	// until the reload can reset the object with the new NativeExtension data.
	for (const ObjectID &obj_id : p_extension->instances) {
		Object *obj = ObjectRegistry::get_instance(obj_id);
		if (!obj) {
			continue;
		}

		obj->clear_internal_extension();
	}
}

void NativeExtension::track_instance_binding(Object *p_object) {
	instance_bindings.push_back(p_object->get_instance_id());
}

void NativeExtension::untrack_instance_binding(Object *p_object) {
	instance_bindings.erase(p_object->get_instance_id());
}

void NativeExtension::clear_instance_bindings() {
	for (ObjectID obj_id : instance_bindings) {
		Object *obj = ObjectRegistry::get_instance(obj_id);
		if (!obj) {
			continue;
		}

		obj->free_instance_binding(this);
	}
	instance_bindings.clear();
}

void NativeExtension::finish_reload() {
	is_reloading = false;

	// Clean up any classes or methods that didn't get re-added.
	Vector<StringName> classes_to_remove;
	for (KeyValue<StringName, Extension> &E : extension_classes) {
		if (E.value.is_reloading) {
			E.value.is_reloading = false;
			classes_to_remove.push_back(E.key);
		}

		Vector<StringName> methods_to_remove;
		for (KeyValue<StringName, NativeExtensionMethodBind *> &M : E.value.methods) {
			if (M.value->is_reloading) {
				M.value->valid = false;

				M.value->is_reloading = false;
				methods_to_remove.push_back(M.key);
			}
		}
		for (const StringName &method_name : methods_to_remove) {
			E.value.methods.erase(method_name);
		}
	}
	for (const StringName &class_name : classes_to_remove) {
		extension_classes.erase(class_name);
	}

	// Reset any the extension on instances made from the classes that remain.
	for (KeyValue<StringName, Extension> &E : extension_classes) {
		// Loop over 'instance_state' rather than 'instance' because new instances
		// may have been created when re-initializing the extension.
		for (const KeyValue<ObjectID, Extension::InstanceState> &S : E.value.instance_state) {
			Object *obj = ObjectRegistry::get_instance(S.key);
			if (!obj) {
				continue;
			}

			if (S.value.is_placeholder) {
				obj->reset_internal_extension(ClassRegistry::get_placeholder_extension(E.value.native_extension.class_name));
			} else {
				obj->reset_internal_extension(&E.value.native_extension);
			}
		}
	}

	// Now that all the classes are back, restore the state.
	for (KeyValue<StringName, Extension> &E : extension_classes) {
		for (const KeyValue<ObjectID, Extension::InstanceState> &S : E.value.instance_state) {
			Object *obj = ObjectRegistry::get_instance(S.key);
			if (!obj) {
				continue;
			}

			for (const Pair<String, Variant> &state : S.value.properties) {
				obj->set(state.first, state.second);
			}
		}
	}

	// Finally, let the objects know that we are done reloading them.
	for (KeyValue<StringName, Extension> &E : extension_classes) {
		for (const KeyValue<ObjectID, Extension::InstanceState> &S : E.value.instance_state) {
			Object *obj = ObjectRegistry::get_instance(S.key);
			if (!obj) {
				continue;
			}

			obj->notification(NOTIFICATION_EXTENSION_RELOADED);
		}

		// Clear the instance state, we're done looping.
		E.value.instance_state.clear();
	}
}

void NativeExtension::_track_instance(void *p_user_data, void *p_instance) {
	Extension *extension = reinterpret_cast<Extension *>(p_user_data);
	Object *obj = reinterpret_cast<Object *>(p_instance);

	extension->instances.insert(obj->get_instance_id());
}

void NativeExtension::_untrack_instance(void *p_user_data, void *p_instance) {
	Extension *extension = reinterpret_cast<Extension *>(p_user_data);
	Object *obj = reinterpret_cast<Object *>(p_instance);

	extension->instances.erase(obj->get_instance_id());
}

PackedStringArray NativeExtension::get_classes_used() const {
	PackedStringArray ret;
	if (get_classes_used_callback) {
		get_classes_used_callback((NativeExtensionTypePtr)&ret);
	}
	return ret;
}

void NativeExtensionEditorPlugins::add_extension_class(const StringName &p_class_name) {
	if (editor_node_add_plugin) {
		callable_mp_static(editor_node_add_plugin).call_deferred(p_class_name);
	} else {
		extension_classes.push_back(p_class_name);
	}
}

void NativeExtensionEditorPlugins::remove_extension_class(const StringName &p_class_name) {
	if (editor_node_remove_plugin) {
		editor_node_remove_plugin(p_class_name);
	} else {
		extension_classes.erase(p_class_name);
	}
}

void NativeExtensionEditorHelp::load_xml_buffer(const uint8_t *p_buffer, int p_size) {
	ERR_FAIL_NULL(editor_help_load_xml_buffer);
	editor_help_load_xml_buffer(p_buffer, p_size);
}

void NativeExtensionEditorHelp::remove_class(const String &p_class) {
	ERR_FAIL_NULL(editor_help_remove_class);
	editor_help_remove_class(p_class);
}
#endif // TOOLS_ENABLED
