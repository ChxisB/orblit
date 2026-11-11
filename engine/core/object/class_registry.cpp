/**************************************************************************/
/*  class_registry.cpp                                                    */

#include "class_registry.h"

#include "core/config/engine.h"
#include "core/containers/sort_array.h"
#include "core/object/ref_counted.h"
#include "core/version.h"

#define ERR_FAIL_NO_CLASS(m_type, m_class) ERR_FAIL_NULL_MSG(m_type, vformat("Cannot get class \"%s\".", m_class))

#ifdef DEBUG_ENABLED

MethodDefinition D_METHODP(const char *p_name, const char *const **p_args, uint32_t p_argcount) {
	MethodDefinition md;
	md.name = StringName(p_name);
	md.args.resize(p_argcount);
	for (uint32_t i = 0; i < p_argcount; i++) {
		md.args.write[i] = StringName(*p_args[i]);
	}
	return md;
}

#endif // DEBUG_ENABLED

ClassRegistry::APIType ClassRegistry::current_api = API_CORE;
HashMap<ClassRegistry::APIType, uint32_t> ClassRegistry::api_hashes_cache;

void ClassRegistry::set_current_api(APIType p_api) {
	DEV_ASSERT(!api_hashes_cache.has(p_api)); // This API type may not be suitable for caching of hash if it can change later.
	current_api = p_api;
}

ClassRegistry::APIType ClassRegistry::get_current_api() {
	return current_api;
}

HashMap<StringName, ClassRegistry::ClassInfo> ClassRegistry::classes;
HashMap<StringName, StringName> ClassRegistry::resource_base_extensions;

#ifdef TOOLS_ENABLED
HashMap<StringName, ObjectNativeExtension> ClassRegistry::placeholder_extensions;

class PlaceholderExtensionInstance {
	StringName class_name;
	HashMap<StringName, Variant> properties;

	// Checks if a property is from a runtime class, and not a non-runtime base class.
	bool is_runtime_property(const StringName &p_property_name) {
		StringName current_class_name = class_name;

		while (ClassRegistry::is_class_runtime(current_class_name)) {
			if (ClassRegistry::has_property(current_class_name, p_property_name, true)) {
				return true;
			}

			current_class_name = ClassRegistry::get_parent_class(current_class_name);
		}

		return false;
	}

public:
	PlaceholderExtensionInstance(const StringName &p_class_name) {
		class_name = p_class_name;
	}

	void set(const StringName &p_name, const Variant &p_value, bool &r_valid) {
		r_valid = is_runtime_property(p_name);
		if (r_valid) {
			properties[p_name] = p_value;
		}
	}

	Variant get(const StringName &p_name, bool &r_valid) {
		const Variant *value = properties.getptr(p_name);
		Variant ret;

		if (value) {
			ret = *value;
			r_valid = true;
		} else {
			r_valid = is_runtime_property(p_name);
			if (r_valid) {
				ret = ClassRegistry::class_get_default_property_value(class_name, p_name);
			}
		}

		return ret;
	}

	static NativeExtensionBool placeholder_instance_set(NativeExtensionClassInstancePtr p_instance, NativeExtensionConstStringNamePtr p_name, NativeExtensionConstVariantPtr p_value) {
		PlaceholderExtensionInstance *self = (PlaceholderExtensionInstance *)p_instance;
		const StringName &name = *(StringName *)p_name;
		const Variant &value = *(const Variant *)p_value;

		bool valid = false;
		self->set(name, value, valid);

		return valid;
	}

	static NativeExtensionBool placeholder_instance_get(NativeExtensionClassInstancePtr p_instance, NativeExtensionConstStringNamePtr p_name, NativeExtensionVariantPtr r_ret) {
		PlaceholderExtensionInstance *self = (PlaceholderExtensionInstance *)p_instance;
		const StringName &name = *(StringName *)p_name;
		Variant *value = (Variant *)r_ret;

		bool valid = false;
		*value = self->get(name, valid);

		return valid;
	}

	static const NativeExtensionPropertyInfo *placeholder_instance_get_property_list(NativeExtensionClassInstancePtr p_instance, uint32_t *r_count) {
		*r_count = 0;
		return nullptr;
	}

	static void placeholder_instance_free_property_list(NativeExtensionClassInstancePtr p_instance, const NativeExtensionPropertyInfo *p_list, uint32_t p_count) {
	}

	static NativeExtensionBool placeholder_instance_property_can_revert(NativeExtensionClassInstancePtr p_instance, NativeExtensionConstStringNamePtr p_name) {
		return false;
	}

	static NativeExtensionBool placeholder_instance_property_get_revert(NativeExtensionClassInstancePtr p_instance, NativeExtensionConstStringNamePtr p_name, NativeExtensionVariantPtr r_ret) {
		return false;
	}

	static NativeExtensionBool placeholder_instance_validate_property(NativeExtensionClassInstancePtr p_instance, NativeExtensionPropertyInfo *p_property) {
		return false;
	}

	static void placeholder_instance_notification(NativeExtensionClassInstancePtr p_instance, int32_t p_what, NativeExtensionBool p_reversed) {
	}

	static void placeholder_instance_to_string(NativeExtensionClassInstancePtr p_instance, NativeExtensionBool *r_is_valid, NativeExtensionStringPtr p_out) {
		*r_is_valid = true;
	}

	static void placeholder_instance_reference(NativeExtensionClassInstancePtr p_instance) {
	}

	static void placeholder_instance_unreference(NativeExtensionClassInstancePtr p_instance) {
	}

	static uint64_t placeholder_instance_get_rid(NativeExtensionClassInstancePtr p_instance) {
		return 0;
	}

	static NativeExtensionObjectPtr placeholder_class_create_instance(void *p_class_userdata, NativeExtensionBool p_notify_postinitialize) {
		ClassRegistry::ClassInfo *ti = (ClassRegistry::ClassInfo *)p_class_userdata;

		// Find the closest native parent, that isn't a runtime class.
		ClassRegistry::ClassInfo *native_parent = ti->inherits_ptr;
		while (native_parent->native_extension || native_parent->is_runtime) {
			native_parent = native_parent->inherits_ptr;
		}
		ERR_FAIL_NULL_V(native_parent->creation_func, nullptr);

		// Construct a placeholder.
		Object *obj = native_parent->creation_func(static_cast<bool>(p_notify_postinitialize));

		// Classes descending from RefCounted are expected to be returned with a refcount of 1.
		if (RefCounted *ref_counted = Object::cast_to<RefCounted>(obj)) {
			ref_counted->init_ref();
		}

		// ClassRegistry::set_object_extension_instance() won't be called for placeholders.
		// We need need to make sure that all the things it would have done (even if
		// done in a different way to support placeholders) will also be done here.

		obj->_extension = ClassRegistry::get_placeholder_extension(ti->class_descriptor->get_name());
		obj->_extension_instance = memnew(PlaceholderExtensionInstance(ti->class_descriptor->get_name()));

		obj->_reset_class_descriptor();

#ifdef TOOLS_ENABLED
		if (obj->_extension->track_instance) {
			obj->_extension->track_instance(obj->_extension->tracking_userdata, obj);
		}
#endif

		return obj;
	}

	static NativeExtensionObjectPtr placeholder_class_recreate_instance(void *p_class_userdata, NativeExtensionObjectPtr p_object) {
		ClassRegistry::ClassInfo *ti = (ClassRegistry::ClassInfo *)p_class_userdata;
		return memnew(PlaceholderExtensionInstance(ti->class_descriptor->get_name()));
	}

	static void placeholder_class_free_instance(void *p_class_userdata, NativeExtensionClassInstancePtr p_instance) {
		PlaceholderExtensionInstance *instance = (PlaceholderExtensionInstance *)p_instance;
		memdelete(instance);
	}

	static NativeExtensionClassCallVirtual placeholder_class_get_virtual(void *p_class_userdata, NativeExtensionConstStringNamePtr p_name, uint32_t p_hash) {
		return nullptr;
	}
};
#endif

bool ClassRegistry::_is_parent_class(const StringName &p_class, const StringName &p_inherits) {
	const ClassInfo *c = classes.getptr(p_class);
	return c ? c->class_descriptor->get_name_hierarchy().has(p_inherits) : false;
}

bool ClassRegistry::is_parent_class(const StringName &p_class, const StringName &p_inherits) {
	Locker::Lock lock(Locker::STATE_READ);

	return _is_parent_class(p_class, p_inherits);
}

// This function only sorts items added by this function.
// If `p_classes` is not empty before calling and a global sort is needed, caller must handle that separately.
void ClassRegistry::get_class_list(LocalVector<StringName> &p_classes) {
	Locker::Lock lock(Locker::STATE_READ);

	if (classes.is_empty()) {
		return;
	}

	p_classes.reserve(p_classes.size() + classes.size());
	for (const KeyValue<StringName, ClassInfo> &cls : classes) {
		p_classes.push_back(cls.key);
	}

	SortArray<StringName, StringName::AlphCompare> sorter;
	sorter.sort(&p_classes[p_classes.size() - classes.size()], classes.size());
}

#ifdef TOOLS_ENABLED
// This function only sorts items added by this function.
// If `p_classes` is not empty before calling and a global sort is needed, caller must handle that separately.
void ClassRegistry::get_extensions_class_list(LocalVector<StringName> &p_classes) {
	Locker::Lock lock(Locker::STATE_READ);

	uint32_t original_size = p_classes.size();

	for (const KeyValue<StringName, ClassInfo> &E : classes) {
		if (E.value.api != API_EXTENSION && E.value.api != API_EDITOR_EXTENSION) {
			continue;
		}
		p_classes.push_back(E.key);
	}

	// Nothing appended.
	if (p_classes.size() == original_size) {
		return;
	}

	SortArray<StringName, StringName::AlphCompare> sorter;
	sorter.sort(&p_classes[original_size], p_classes.size() - original_size);
}

void ClassRegistry::get_extension_class_list(const Ref<NativeExtension> &p_extension, List<StringName> *p_classes) {
	Locker::Lock lock(Locker::STATE_READ);

	for (const KeyValue<StringName, ClassInfo> &E : classes) {
		if (E.value.api != API_EXTENSION && E.value.api != API_EDITOR_EXTENSION) {
			continue;
		}
		if (!E.value.native_extension || E.value.native_extension->library != p_extension.ptr()) {
			continue;
		}
		p_classes->push_back(E.key);
	}

	p_classes->sort_custom<StringName::AlphCompare>();
}
#endif

void ClassRegistry::get_inheriters_from_class(const StringName &p_class, LocalVector<StringName> &p_classes) {
	Locker::Lock lock(Locker::STATE_READ);

	for (const KeyValue<StringName, ClassInfo> &E : classes) {
		if (E.key != p_class && _is_parent_class(E.key, p_class)) {
			p_classes.push_back(E.key);
		}
	}
}

void ClassRegistry::get_direct_inheriters_from_class(const StringName &p_class, List<StringName> *p_classes) {
	Locker::Lock lock(Locker::STATE_READ);

	for (const KeyValue<StringName, ClassInfo> &E : classes) {
		if (E.value.class_descriptor->get_super_type_name() == p_class) {
			p_classes->push_back(E.key);
		}
	}
}

StringName ClassRegistry::get_parent_class_nocheck(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *ti = classes.getptr(p_class);
	return ti ? ti->class_descriptor->get_super_type_name() : StringName();
}

bool ClassRegistry::get_inheritance_chain_nocheck(const StringName &p_class, Vector<StringName> &r_result) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *ti = classes.getptr(p_class);
	if (!ti) {
		return false;
	}

	r_result.append_array(ti->class_descriptor->get_name_hierarchy());

	return true;
}

StringName ClassRegistry::_get_parent_class(const StringName &p_class) {
	ClassInfo *ti = classes.getptr(p_class);
	ERR_FAIL_NULL_V_MSG(ti, StringName(), vformat("Cannot get class '%s'.", String(p_class)));
	return ti->class_descriptor->get_super_type_name();
}

StringName ClassRegistry::get_parent_class(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);

	return _get_parent_class(p_class);
}

ClassRegistry::APIType ClassRegistry::get_api_type(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *ti = classes.getptr(p_class);

	ERR_FAIL_NULL_V_MSG(ti, API_NONE, vformat("Cannot get class '%s'.", String(p_class)));
	return ti->api;
}

uint32_t ClassRegistry::get_api_hash(APIType p_api) {
#ifdef DEBUG_ENABLED
	Locker::Lock lock(Locker::STATE_WRITE);

	if (api_hashes_cache.has(p_api)) {
		return api_hashes_cache[p_api];
	}

	uint64_t hash = hash_murmur3_one_64(HashMapHasherDefault::hash(ORBLIT_VERSION_FULL_CONFIG));

	List<StringName> class_list;
	for (const KeyValue<StringName, ClassInfo> &E : classes) {
		class_list.push_back(E.key);
	}
	// Must be alphabetically sorted for hash to compute.
	class_list.sort_custom<StringName::AlphCompare>();

	for (const StringName &E : class_list) {
		ClassInfo *t = classes.getptr(E);
		ERR_FAIL_NULL_V_MSG(t, 0, vformat("Cannot get class '%s'.", String(E)));
		if (t->api != p_api || !t->exposed) {
			continue;
		}
		hash = hash_murmur3_one_64(t->class_descriptor->get_name().hash(), hash);
		hash = hash_murmur3_one_64(t->class_descriptor->get_super_type_name().hash(), hash);

		LocalVector<StringName> methods;
		LocalVector<StringName> constants;
		LocalVector<StringName> signals;
		LocalVector<StringName> setgets;

		for (const KeyValue<StringName, ClassDescriptor::Member> &kv : t->class_descriptor->members(true)) {
			if (kv.value.type == ClassDescriptor::Member::Type::METHOD) {
				String name = kv.key.string();

				ERR_CONTINUE(name.is_empty());

				if (name[0] == '_') {
					continue; // Ignore non-virtual methods that start with an underscore
				}

				methods.push_back(kv.key);
			} else if (kv.value.type == ClassDescriptor::Member::Type::INTEGER_CONSTANT) {
				constants.push_back(kv.key);
			} else if (kv.value.type == ClassDescriptor::Member::Type::SIGNAL) {
				signals.push_back(kv.key);
			} else if (kv.value.type == ClassDescriptor::Member::Type::PROPERTY) {
				setgets.push_back(kv.key);
			}
		}

		{ //methods
			methods.sort_custom<StringName::AlphCompare>();

			for (const StringName &F : methods) {
				const MethodBind *mb = t->class_descriptor->members(true)[F].payload.method;
				hash = hash_murmur3_one_64(mb->get_name().hash(), hash);
				hash = hash_murmur3_one_64(mb->get_argument_count(), hash);
				hash = hash_murmur3_one_64(mb->get_argument_type(-1), hash); //return

				for (int i = 0; i < mb->get_argument_count(); i++) {
					const PropertyInfo info = mb->get_argument_info(i);
					hash = hash_murmur3_one_64(info.type, hash);
					hash = hash_murmur3_one_64(info.name.hash(), hash);
					hash = hash_murmur3_one_64(info.hint, hash);
					hash = hash_murmur3_one_64(info.hint_string.hash(), hash);
				}

				hash = hash_murmur3_one_64(mb->get_default_argument_count(), hash);

				for (int i = 0; i < mb->get_argument_count(); i++) {
					if (mb->has_default_argument(i)) {
						Variant da = mb->get_default_argument(i);
						hash = hash_murmur3_one_64(da.hash(), hash);
					}
				}

				hash = hash_murmur3_one_64(mb->get_hint_flags(), hash);
			}
		}

		{ //constants
			constants.sort_custom<StringName::AlphCompare>();

			for (const StringName &F : constants) {
				hash = hash_murmur3_one_64(F.hash(), hash);
				hash = hash_murmur3_one_64(uint64_t(t->class_descriptor->members(true)[F].payload.integer_constant.value), hash);
			}
		}

		{ //signals
			signals.sort_custom<StringName::AlphCompare>();

			for (const StringName &F : signals) {
				const MethodInfo &mi = *t->class_descriptor->members(true)[F].payload.signal;
				hash = hash_murmur3_one_64(F.hash(), hash);
				for (const PropertyInfo &pi : mi.arguments) {
					hash = hash_murmur3_one_64(pi.type, hash);
				}
			}
		}

		{
			setgets.sort_custom<StringName::AlphCompare>();

			for (const StringName &F : setgets) {
				const ClassDescriptor::Member &member = t->class_descriptor->members(true)[F];
				const ClassDescriptor::Member::Property &psg = member.payload.property;

				hash = hash_murmur3_one_64(F.hash(), hash);
				hash = hash_murmur3_one_64((psg.setter ? psg.setter->get_name() : StringName()).hash(), hash);
				hash = hash_murmur3_one_64((psg.getter ? psg.getter->get_name() : StringName()).hash(), hash);
			}
		}

		//property list
		for (const PropertyInfo *F : t->class_descriptor->get_ordered_self_properties()) {
			hash = hash_murmur3_one_64(F->name.hash(), hash);
			hash = hash_murmur3_one_64(F->type, hash);
			hash = hash_murmur3_one_64(F->hint, hash);
			hash = hash_murmur3_one_64(F->hint_string.hash(), hash);
			hash = hash_murmur3_one_64(F->usage, hash);
		}
	}

	hash = hash_fmix32(hash);

	// Extension API changes at runtime; let's just not cache them by now.
	if (p_api != API_EXTENSION && p_api != API_EDITOR_EXTENSION) {
		api_hashes_cache[p_api] = hash;
	}

	return hash;
#else
	return 0;
#endif // DEBUG_ENABLED
}

bool ClassRegistry::class_exists(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);
	return classes.has(p_class);
}

Object *ClassRegistry::_instantiate_from_native_extension(ObjectNativeExtension *p_object_native_extension, bool p_notify_postinitialize, bool p_with_refcount) {
	if (p_with_refcount) {
		if (p_object_native_extension->create_instance3 != nullptr) {
			// Caller expects refcount=1, creation func returns with refcount=1. It's a match.
			return (Object *)p_object_native_extension->create_instance3(p_object_native_extension->class_userdata, p_notify_postinitialize);
		}
	} else {
		if (p_object_native_extension->create_instance3 != nullptr) {
			Object *object = (Object *)p_object_native_extension->create_instance3(p_object_native_extension->class_userdata, p_notify_postinitialize);
			if (object != nullptr && object->is_ref_counted()) {
				// Caller expects no refcount, but refcount was already incremented by creation func.
				// Must fall back to RefCounted's refcount_init trick.
				((RefCounted *)object)->deinit_ref();
			}
			return object;
		}
	}

	ERR_FAIL_V_MSG(nullptr, vformat("Internal error; ObjectNativeExtension of %s has neither create_instance2 nor create_instance3.", p_object_native_extension->class_name));
}

Object *ClassRegistry::_instantiate_internal(const StringName &p_class, bool p_require_real_class, bool p_notify_postinitialize, bool p_exposed_only, bool p_with_refcount) {
	ClassInfo *ti;
	{
		Locker::Lock lock(Locker::STATE_READ);
		ti = classes.getptr(p_class);
		ERR_FAIL_NULL_V_MSG(ti, nullptr, vformat("Cannot get class '%s'.", String(p_class)));
		ERR_FAIL_COND_V_MSG(ti->disabled, nullptr, vformat("Class '%s' is disabled.", String(p_class)));
		if (p_exposed_only) {
			ERR_FAIL_COND_V_MSG(!ti->exposed, nullptr, vformat("Class '%s' isn't exposed.", String(p_class)));
		}
		ERR_FAIL_NULL_V_MSG(ti->creation_func, nullptr, vformat("Class '%s' or its base class cannot be instantiated.", String(p_class)));
	}

#ifdef TOOLS_ENABLED
	if ((ti->api == API_EDITOR || ti->api == API_EDITOR_EXTENSION) && !Engine::get_singleton()->is_editor_hint()) {
		ERR_PRINT(vformat("Class '%s' can only be instantiated by editor.", String(p_class)));
		return nullptr;
	}
#endif

#ifdef TOOLS_ENABLED
	// Try to create placeholder.
	if (!p_require_real_class && ti->is_runtime && Engine::get_singleton()->is_editor_hint()) {
		bool can_create_placeholder = false;
		if (ti->native_extension) {
			if (ti->native_extension->create_instance3) {
				can_create_placeholder = true;
			}
		} else if (!ti->inherits_ptr || !ti->inherits_ptr->creation_func) {
			ERR_PRINT(vformat("Cannot make a placeholder instance of runtime class %s because its parent cannot be constructed.", ti->class_descriptor->get_name()));
		} else {
			can_create_placeholder = true;
		}

		if (can_create_placeholder) {
			ObjectNativeExtension *extension = get_placeholder_extension(ti->class_descriptor->get_name());
			return _instantiate_from_native_extension(extension, p_notify_postinitialize, p_with_refcount);
		}
	}
#endif // TOOLS_ENABLED

	if (ti->native_extension && ti->native_extension->create_instance3) {
		ObjectNativeExtension *extension = ti->native_extension;
		return _instantiate_from_native_extension(extension, p_notify_postinitialize, p_with_refcount);
	} else {
		Object *object = ti->creation_func(p_notify_postinitialize);
		if (p_with_refcount && object != nullptr && object->is_ref_counted()) {
			// creation_func creates the object with an (effective) refcount of 1,
			// so establish the expected refcount=1 now.
			((RefCounted *)object)->init_ref();
		}
		return object;
	}
}

bool ClassRegistry::_can_instantiate(ClassInfo *p_class_info, bool p_exposed_only) {
	if (!p_class_info) {
		return false;
	}

	if (p_exposed_only && !p_class_info->exposed) {
		return false;
	}

	if (p_class_info->disabled || !p_class_info->creation_func) {
		return false;
	}

	if (!p_class_info->native_extension) {
		return true;
	}

	if (p_class_info->native_extension->create_instance3) {
		return true;
	}

	return false;
}

Object *ClassRegistry::instantiate(const StringName &p_class) {
	return _instantiate_internal(p_class);
}

Object *ClassRegistry::instantiate_no_placeholders(const StringName &p_class) {
	return _instantiate_internal(p_class, true);
}

Object *ClassRegistry::instantiate_without_postinitialization(const StringName &p_class) {
	return _instantiate_internal(p_class, true, false);
}

Object *ClassRegistry::instantiate_without_postinitialization_with_refcount(const StringName &p_class) {
	return _instantiate_internal(p_class, true, false, true, true);
}

#ifdef TOOLS_ENABLED
ObjectNativeExtension *ClassRegistry::get_placeholder_extension(const StringName &p_class) {
	ObjectNativeExtension *placeholder_extension = placeholder_extensions.getptr(p_class);
	if (placeholder_extension) {
		return placeholder_extension;
	}

	ClassInfo *ti;
	{
		Locker::Lock lock(Locker::STATE_READ);
		ti = classes.getptr(p_class);
		ERR_FAIL_NULL_V_MSG(ti, nullptr, vformat("Cannot get class '%s'.", String(p_class)));
		ERR_FAIL_COND_V_MSG(ti->disabled, nullptr, vformat("Class '%s' is disabled.", String(p_class)));
	}

	// Make a "fake" extension to act as a placeholder.
	placeholder_extensions[p_class] = ObjectNativeExtension();
	placeholder_extension = placeholder_extensions.getptr(p_class);

	placeholder_extension->is_runtime = true;
	placeholder_extension->is_placeholder = true;

	if (ti->native_extension) {
		placeholder_extension->library = ti->native_extension->library;
		placeholder_extension->parent = ti->native_extension->parent;
		placeholder_extension->children = ti->native_extension->children;
		placeholder_extension->parent_class_name = ti->native_extension->parent_class_name;
		placeholder_extension->class_name = ti->native_extension->class_name;
		placeholder_extension->editor_class = ti->native_extension->editor_class;
		placeholder_extension->reloadable = ti->native_extension->reloadable;
		placeholder_extension->is_virtual = ti->native_extension->is_virtual;
		placeholder_extension->is_abstract = ti->native_extension->is_abstract;
		placeholder_extension->is_exposed = ti->native_extension->is_exposed;

		placeholder_extension->tracking_userdata = ti->native_extension->tracking_userdata;
		placeholder_extension->track_instance = ti->native_extension->track_instance;
		placeholder_extension->untrack_instance = ti->native_extension->untrack_instance;
	} else {
		placeholder_extension->library = nullptr;
		placeholder_extension->parent = nullptr;
		placeholder_extension->parent_class_name = ti->class_descriptor->get_super_type_name();
		placeholder_extension->class_name = ti->class_descriptor->get_name();
		placeholder_extension->editor_class = ti->api == API_EDITOR;
		placeholder_extension->reloadable = false;
		placeholder_extension->is_virtual = ti->is_virtual;
		placeholder_extension->is_abstract = false;
		placeholder_extension->is_exposed = ti->exposed;
	}

	placeholder_extension->set = &PlaceholderExtensionInstance::placeholder_instance_set;
	placeholder_extension->get = &PlaceholderExtensionInstance::placeholder_instance_get;
	placeholder_extension->get_property_list = &PlaceholderExtensionInstance::placeholder_instance_get_property_list;
	placeholder_extension->free_property_list2 = &PlaceholderExtensionInstance::placeholder_instance_free_property_list;
	placeholder_extension->property_can_revert = &PlaceholderExtensionInstance::placeholder_instance_property_can_revert;
	placeholder_extension->property_get_revert = &PlaceholderExtensionInstance::placeholder_instance_property_get_revert;
	placeholder_extension->validate_property = &PlaceholderExtensionInstance::placeholder_instance_validate_property;
	placeholder_extension->notification2 = &PlaceholderExtensionInstance::placeholder_instance_notification;
	placeholder_extension->to_string = &PlaceholderExtensionInstance::placeholder_instance_to_string;
	placeholder_extension->reference = &PlaceholderExtensionInstance::placeholder_instance_reference;
	placeholder_extension->unreference = &PlaceholderExtensionInstance::placeholder_instance_unreference;
	placeholder_extension->get_rid = &PlaceholderExtensionInstance::placeholder_instance_get_rid;

	placeholder_extension->class_userdata = ti;
	placeholder_extension->create_instance3 = &PlaceholderExtensionInstance::placeholder_class_create_instance;
	placeholder_extension->free_instance = &PlaceholderExtensionInstance::placeholder_class_free_instance;
	placeholder_extension->get_virtual2 = &PlaceholderExtensionInstance::placeholder_class_get_virtual;
	placeholder_extension->get_virtual_call_data2 = nullptr;
	placeholder_extension->call_virtual_with_data = nullptr;
	placeholder_extension->recreate_instance = &PlaceholderExtensionInstance::placeholder_class_recreate_instance;

	placeholder_extension->class_descriptor = ti->class_descriptor;

	return placeholder_extension;
}
#endif

const ClassDescriptor *ClassRegistry::get_class_descriptor(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);
	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NULL_V(type, nullptr);
	return type->class_descriptor;
}

void ClassRegistry::set_object_extension_instance(Object *p_object, const StringName &p_class, NativeExtensionClassInstancePtr p_instance) {
	ERR_FAIL_NULL(p_object);
	ClassInfo *ti;
	{
		Locker::Lock lock(Locker::STATE_READ);
		ti = classes.getptr(p_class);
		ERR_FAIL_NULL_MSG(ti, vformat("Cannot get class '%s'.", String(p_class)));
		ERR_FAIL_COND_MSG(ti->disabled, vformat("Class '%s' is disabled.", String(p_class)));
		ERR_FAIL_NULL_MSG(ti->native_extension, vformat("Class '%s' has no native extension.", String(p_class)));
	}

	p_object->_extension = ti->native_extension;
	p_object->_extension_instance = p_instance;

	p_object->_reset_class_descriptor();

#ifdef TOOLS_ENABLED
	if (p_object->_extension->track_instance) {
		p_object->_extension->track_instance(p_object->_extension->tracking_userdata, p_object);
	}
#endif
}

bool ClassRegistry::can_instantiate(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *ti = classes.getptr(p_class);
	ERR_FAIL_NULL_V_MSG(ti, false, vformat("Cannot get class '%s'.", String(p_class)));
#ifdef TOOLS_ENABLED
	if ((ti->api == API_EDITOR || ti->api == API_EDITOR_EXTENSION) && !Engine::get_singleton()->is_editor_hint()) {
		return false;
	}
#endif
	return _can_instantiate(ti);
}

bool ClassRegistry::is_abstract(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *ti = classes.getptr(p_class);
	ERR_FAIL_NULL_V_MSG(ti, false, vformat("Cannot get class '%s'.", String(p_class)));

	if (ti->creation_func != nullptr) {
		return false;
	}
	if (!ti->native_extension) {
		return true;
	}
	return ti->native_extension->create_instance3 == nullptr;
}

bool ClassRegistry::is_virtual(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *ti = classes.getptr(p_class);
	ERR_FAIL_NULL_V_MSG(ti, false, vformat("Cannot get class '%s'.", String(p_class)));

#ifdef TOOLS_ENABLED
	if ((ti->api == API_EDITOR || ti->api == API_EDITOR_EXTENSION) && !Engine::get_singleton()->is_editor_hint()) {
		return false;
	}
#endif
	return (_can_instantiate(ti) && ti->is_virtual);
}

bool ClassRegistry::is_native_extension(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);
	ClassInfo *ti = classes.getptr(p_class);
	if (ti) {
		return ti->native_extension;
	}
	return false;
}

void ClassRegistry::_add_class(ClassDescriptor &p_class, const ClassDescriptor *p_inherits) {
	Locker::Lock lock(Locker::STATE_WRITE);

	const StringName &name = p_class.get_name();

	ERR_FAIL_COND_MSG(classes.has(name), vformat("Class '%s' already exists.", name));

	classes[name] = ClassInfo();
	ClassInfo &ti = classes[name];
	ti.class_descriptor = &p_class;
	ti.api = current_api;

	if (p_inherits) {
		ERR_FAIL_COND(!classes.has(ti.class_descriptor->get_super_type_name())); //it MUST be registered.
		ti.inherits_ptr = &classes[ti.class_descriptor->get_super_type_name()];
	} else {
		ti.inherits_ptr = nullptr;
	}
}

static MethodInfo info_from_bind(const MethodBind *p_method) {
	MethodInfo minfo;
	minfo.name = p_method->get_name();
	minfo.id = p_method->get_method_id();

	for (int i = 0; i < p_method->get_argument_count(); i++) {
		minfo.arguments.push_back(p_method->get_argument_info(i));
	}

	minfo.return_val = p_method->get_return_info();
	minfo.flags = p_method->get_hint_flags();

	for (int i = 0; i < p_method->get_argument_count(); i++) {
		if (p_method->has_default_argument(i)) {
			minfo.default_arguments.push_back(p_method->get_default_argument(i));
		}
	}

	return minfo;
}

void ClassRegistry::get_method_list(const StringName &p_class, List<MethodInfo> *p_methods, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NULL(type);

	do {
		if (type->disabled) {
			type = type->inherits_ptr;
			continue;
		}

		for (const KeyValue<StringName, ClassDescriptor::Member> &kv : type->class_descriptor->members(true)) {
			if (kv.value.type == ClassDescriptor::Member::Type::METHOD) {
				p_methods->push_back(info_from_bind(kv.value.payload.method));
			}
		}
		type = type->inherits_ptr;
	} while (type && !p_no_inheritance);

#ifdef DEBUG_ENABLED
	ClassRegistry::get_virtual_methods(p_class, p_methods, p_no_inheritance);
#endif
}

void ClassRegistry::get_method_list_with_hashes(const StringName &p_class, List<Pair<MethodInfo, uint32_t>> *p_methods, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NULL(type);

	while (type) {
		if (type->disabled) {
			if (p_no_inheritance) {
				break;
			}

			type = type->inherits_ptr;
			continue;
		}
		for (const KeyValue<StringName, ClassDescriptor::Member> &kv : type->class_descriptor->members(true)) {
			if (kv.value.type == ClassDescriptor::Member::Type::METHOD) {
				p_methods->push_back(Pair(info_from_bind(kv.value.payload.method), kv.value.payload.method->get_hash()));
			}
		}
#ifdef DEBUG_ENABLED
		for (const MethodInfo &E : type->virtual_methods) {
			Pair<MethodInfo, uint32_t> pair(E, E.get_method_hash());
			p_methods->push_back(pair);
		}
#endif
		if (p_no_inheritance) {
			break;
		}

		type = type->inherits_ptr;
	}
}

bool ClassRegistry::get_method_info(const StringName &p_class, const StringName &p_method, MethodInfo *r_info, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);

	while (type) {
		if (type->disabled) {
			if (p_no_inheritance) {
				break;
			}

			type = type->inherits_ptr;
			continue;
		}

#ifdef DEBUG_ENABLED
		const ClassDescriptor::Member *member = type->class_descriptor->members(true).getptr(p_method);
		if (member && member->type == ClassDescriptor::Member::Type::METHOD) {
			if (r_info != nullptr) {
				*r_info = info_from_bind(member->payload.method);
			}
			return true;
		} else if (type->virtual_methods_map.has(p_method)) {
			if (r_info) {
				*r_info = type->virtual_methods_map[p_method];
			}
			return true;
		}
#else
		const ClassDescriptor::Member *member = type->class_descriptor->members(true).getptr(p_method);
		if (member && member->type == ClassDescriptor::Member::Type::METHOD) {
			if (r_info) {
				*r_info = info_from_bind(member->payload.method);
			}
			return true;
		}
#endif // DEBUG_ENABLED

		if (p_no_inheritance) {
			break;
		}

		type = type->inherits_ptr;
	}

	return false;
}

const MethodBind *ClassRegistry::get_method(const StringName &p_class, const StringName &p_name) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	if (!type) {
		return nullptr;
	}

	const ClassDescriptor::Member *member = type->class_descriptor->members().getptr(p_name);
	return member && member->type == ClassDescriptor::Member::Type::METHOD ? member->payload.method : nullptr;
}

const MethodBind *ClassRegistry::get_method_by_hash(const StringName &p_class, const StringName &p_name, uint64_t p_hash, bool *r_method_exists) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);

	while (type) {
		const ClassDescriptor::Member *member = type->class_descriptor->members(true).getptr(p_name);
		if (member && member->type == ClassDescriptor::Member::Type::METHOD) {
			if (r_method_exists) {
				*r_method_exists = true;
			}
			if (member->payload.method->get_hash() == p_hash) {
				return member->payload.method;
			}
		}
		type = type->inherits_ptr;
	}
	return nullptr;
}

void ClassRegistry::bind_integer_constant(const StringName &p_class, const StringName &p_enum, const StringName &p_name, int64_t p_constant, bool p_is_bitfield) {
	Locker::Lock lock(Locker::STATE_WRITE);

	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NO_CLASS(type, p_class);

	type->class_descriptor->bind_integer_constant(p_enum, p_name, p_constant, p_is_bitfield);
}

void ClassRegistry::get_integer_constant_list(const StringName &p_class, List<String> *p_constants, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NO_CLASS(type, p_class);

	for (const KeyValue<StringName, ClassDescriptor::Member> &kv : type->class_descriptor->members(p_no_inheritance)) {
		if (kv.value.type == ClassDescriptor::Member::Type::INTEGER_CONSTANT) {
			p_constants->push_back(kv.key);
		}
	}
}

int64_t ClassRegistry::get_integer_constant(const StringName &p_class, const StringName &p_name, bool *p_success) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	if (type) {
		const ClassDescriptor::Member *member = type->class_descriptor->members().getptr(p_name);
		if (member && member->type == ClassDescriptor::Member::Type::INTEGER_CONSTANT) {
			if (p_success) {
				*p_success = true;
			}
			return member->payload.integer_constant.value;
		}
	}

	if (p_success) {
		*p_success = false;
	}
	return 0;
}

bool ClassRegistry::has_integer_constant(const StringName &p_class, const StringName &p_name, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NULL_V(type, false);

	const ClassDescriptor::Member *member = type->class_descriptor->members(p_no_inheritance).getptr(p_name);
	return member && member->type == ClassDescriptor::Member::Type::INTEGER_CONSTANT;
}

StringName ClassRegistry::get_integer_constant_enum(const StringName &p_class, const StringName &p_name, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NULL_V(type, StringName());

	const ClassDescriptor::EnumInfo *enum_info = type->class_descriptor->get_integer_constant_enum(p_name, p_no_inheritance);
	return enum_info ? enum_info->name : StringName();
}

void ClassRegistry::get_enum_list(const StringName &p_class, List<StringName> *p_enums, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	if (!type) {
		// TODO This should probably error, but didn't previously.
		// Making this error leads to a few prints during unit tests.
		return;
	}

	for (const KeyValue<StringName, ClassDescriptor::Member> &kv : type->class_descriptor->members(p_no_inheritance)) {
		if (kv.value.type == ClassDescriptor::Member::Type::ENUM) {
			p_enums->push_back(kv.key);
		}
	}
}

void ClassRegistry::get_enum_constants(const StringName &p_class, const StringName &p_enum, List<StringName> *p_constants, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NO_CLASS(type, p_class);

	const ClassDescriptor::Member *member = type->class_descriptor->members(p_no_inheritance).getptr(p_enum);
	ERR_FAIL_NULL(member);
	ERR_FAIL_COND(member->type != ClassDescriptor::Member::Type::ENUM);

	for (const KeyValue<StringName, int64_t> &kv : member->payload.enum_info->values) {
		p_constants->push_back(kv.key);
	}
}

void ClassRegistry::set_method_error_return_values(const StringName &p_class, const StringName &p_method, const Vector<Error> &p_values) {
#ifdef DEBUG_ENABLED
	Locker::Lock lock(Locker::STATE_WRITE);
	ClassInfo *type = classes.getptr(p_class);

	ERR_FAIL_NO_CLASS(type, p_class);

	type->method_error_values[p_method] = p_values;
#endif // DEBUG_ENABLED
}

Vector<Error> ClassRegistry::get_method_error_return_values(const StringName &p_class, const StringName &p_method) {
#ifdef DEBUG_ENABLED
	Locker::Lock lock(Locker::STATE_READ);
	ClassInfo *type = classes.getptr(p_class);

	ERR_FAIL_NULL_V(type, Vector<Error>());

	if (!type->method_error_values.has(p_method)) {
		return Vector<Error>();
	}
	return type->method_error_values[p_method];
#else
	return Vector<Error>();
#endif // DEBUG_ENABLED
}

bool ClassRegistry::has_enum(const StringName &p_class, const StringName &p_name, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NULL_V(type, false);

	const ClassDescriptor::Member *member = type->class_descriptor->members(p_no_inheritance).getptr(p_name);
	return member && member->type == ClassDescriptor::Member::Type::ENUM;
}

bool ClassRegistry::is_enum_bitfield(const StringName &p_class, const StringName &p_name, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NULL_V(type, false);

	const ClassDescriptor::Member *member = type->class_descriptor->members(p_no_inheritance).getptr(p_name);
	// FIXME This fails unexpectedly often. Keeping legacy behavior to silently fail for now.
	if (!member || member->type != ClassDescriptor::Member::Type::ENUM) {
		return false;
	}

	return member->payload.enum_info->is_bitfield;
}

void ClassRegistry::add_signal(const StringName &p_class, const MethodInfo &p_signal) {
	Locker::Lock lock(Locker::STATE_WRITE);

	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NO_CLASS(type, p_class);

	type->class_descriptor->add_signal(p_signal);
}

void ClassRegistry::get_signal_list(const StringName &p_class, List<MethodInfo> *p_signals, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NO_CLASS(type, p_class);

	for (const KeyValue<StringName, ClassDescriptor::Member> &kv : type->class_descriptor->members(p_no_inheritance)) {
		if (kv.value.type == ClassDescriptor::Member::Type::SIGNAL) {
			p_signals->push_back(*kv.value.payload.signal);
		}
	}
}

bool ClassRegistry::has_signal(const StringName &p_class, const StringName &p_signal, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);
	ClassInfo *type = classes.getptr(p_class);
	if (!type) {
		return false;
	}
	const ClassDescriptor::Member *member = type->class_descriptor->members(p_no_inheritance).getptr(p_signal);
	return member && member->type == ClassDescriptor::Member::Type::SIGNAL;
}

bool ClassRegistry::get_signal(const StringName &p_class, const StringName &p_signal, MethodInfo *r_signal) {
	Locker::Lock lock(Locker::STATE_READ);
	ClassInfo *type = classes.getptr(p_class);
	if (!type) {
		return false;
	}
	const ClassDescriptor::Member *member = type->class_descriptor->members().getptr(p_signal);
	if (!member || member->type != ClassDescriptor::Member::Type::SIGNAL) {
		return false;
	}
	*r_signal = *member->payload.signal;
	return true;
}

void ClassRegistry::add_property_group(const StringName &p_class, const String &p_name, const String &p_prefix, int p_indent_depth) {
	Locker::Lock lock(Locker::STATE_READ); // Doesn't modify ClassRegistry stuff.
	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NO_CLASS(type, p_class);

	String prefix = p_prefix;
	if (p_indent_depth > 0) {
		prefix = vformat("%s,%d", p_prefix, p_indent_depth);
	}

	type->class_descriptor->add_to_ordered_properties(PropertyInfo(Variant::NIL, p_name, PROPERTY_HINT_NONE, prefix, PROPERTY_USAGE_GROUP));
}

void ClassRegistry::add_property_subgroup(const StringName &p_class, const String &p_name, const String &p_prefix, int p_indent_depth) {
	Locker::Lock lock(Locker::STATE_READ); // Doesn't modify ClassRegistry stuff.
	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NO_CLASS(type, p_class);

	String prefix = p_prefix;
	if (p_indent_depth > 0) {
		prefix = vformat("%s,%d", p_prefix, p_indent_depth);
	}

	type->class_descriptor->add_to_ordered_properties(PropertyInfo(Variant::NIL, p_name, PROPERTY_HINT_NONE, prefix, PROPERTY_USAGE_SUBGROUP));
}

void ClassRegistry::add_property_array_count(const StringName &p_class, const String &p_label, const StringName &p_count_property, const StringName &p_count_setter, const StringName &p_count_getter, const String &p_array_element_prefix, uint32_t p_count_usage) {
	add_property(p_class, PropertyInfo(Variant::INT, p_count_property, PROPERTY_HINT_NONE, "", p_count_usage | PROPERTY_USAGE_ARRAY, vformat("%s,%s", p_label, p_array_element_prefix)), p_count_setter, p_count_getter);
}

void ClassRegistry::add_property_array(const StringName &p_class, const StringName &p_path, const String &p_array_element_prefix) {
	Locker::Lock lock(Locker::STATE_READ); // Doesn't modify ClassRegistry stuff.
	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NO_CLASS(type, p_class);

	type->class_descriptor->add_to_ordered_properties(PropertyInfo(Variant::NIL, p_path, PROPERTY_HINT_NONE, "", PROPERTY_USAGE_EDITOR | PROPERTY_USAGE_ARRAY, p_array_element_prefix));
}

// NOTE: For implementation simplicity reasons, this method doesn't allow setters to have optional arguments at the end.
void ClassRegistry::add_property(const StringName &p_class, const PropertyInfo &p_pinfo, const StringName &p_setter, const StringName &p_getter, int p_index) {
	Locker::Lock lock(Locker::STATE_READ); // Doesn't modify ClassRegistry stuff.

	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NO_CLASS(type, p_class);
	type->class_descriptor->add_property(p_pinfo, p_setter, p_getter, p_index);
}

void ClassRegistry::set_property_default_value(const StringName &p_class, const StringName &p_name, const Variant &p_default) {
	if (!default_values.has(p_class)) {
		default_values[p_class] = HashMap<StringName, Variant>();
	}
	default_values[p_class][p_name] = p_default;
}

void ClassRegistry::add_linked_property(const StringName &p_class, const String &p_property, const String &p_linked_property) {
#ifdef TOOLS_ENABLED
	Locker::Lock lock(Locker::STATE_READ); // Doesn't modify ClassRegistry stuff.
	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NO_CLASS(type, p_class);

	const ClassDescriptor::Member *member = type->class_descriptor->members(true).getptr(p_property);
	ERR_FAIL_NULL(member);
	ERR_FAIL_COND(member->type != ClassDescriptor::Member::Type::PROPERTY);

	const ClassDescriptor::Member *linked_property = type->class_descriptor->members(true).getptr(p_linked_property);
	ERR_FAIL_NULL(linked_property);
	ERR_FAIL_COND(linked_property->type != ClassDescriptor::Member::Type::PROPERTY);

	if (!type->linked_properties.has(p_property)) {
		type->linked_properties.insert(p_property, List<StringName>());
	}
	type->linked_properties[p_property].push_back(p_linked_property);

#endif
}

void ClassRegistry::get_property_list(const StringName &p_class, List<PropertyInfo> *p_list, bool p_no_inheritance, const Object *p_validator) {
	Locker::Lock lock(Locker::STATE_READ); // Doesn't modify ClassRegistry stuff.

	ClassInfo *type = classes.getptr(p_class);
	ClassInfo *check = type;
	while (check) {
		for (const PropertyInfo *pi : check->class_descriptor->get_ordered_self_properties()) {
			p_list->push_back(*pi);
			if (p_validator) {
				p_validator->validate_property(p_list->back()->get());
			}
		}

		if (p_no_inheritance) {
			return;
		}
		check = check->inherits_ptr;
	}
}

void ClassRegistry::get_linked_properties_info(const StringName &p_class, const StringName &p_property, List<StringName> *r_properties, bool p_no_inheritance) {
#ifdef TOOLS_ENABLED
	ClassInfo *check = classes.getptr(p_class);
	while (check) {
		if (!check->linked_properties.has(p_property)) {
			return;
		}
		for (const StringName &E : check->linked_properties[p_property]) {
			r_properties->push_back(E);
		}

		if (p_no_inheritance) {
			break;
		}
		check = check->inherits_ptr;
	}
#endif
}

bool ClassRegistry::get_property_info(const StringName &p_class, const StringName &p_property, PropertyInfo *r_info, bool p_no_inheritance, const Object *p_validator) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *class_info = classes.getptr(p_class);
	if (class_info) {
		const ClassDescriptor::Member *member = class_info->class_descriptor->members(p_no_inheritance).getptr(p_property);
		if (member && member->type == ClassDescriptor::Member::Type::PROPERTY) {
			if (r_info) {
				*r_info = *member->payload.property.property_info;
			}
			if (p_validator) {
				p_validator->validate_property(*r_info);
			}
			return true;
		}
	}

	return false;
}

bool ClassRegistry::set_property(Object *p_object, const StringName &p_property, const Variant &p_value, bool *r_valid) {
	ERR_FAIL_NULL_V(p_object, false);

	return p_object->set_native(p_property, p_value, r_valid);
}

bool ClassRegistry::get_property(Object *p_object, const StringName &p_property, Variant &r_value) {
	ERR_FAIL_NULL_V(p_object, false);

	return p_object->get_native(p_property, r_value);
}

int ClassRegistry::get_property_index(const StringName &p_class, const StringName &p_property, bool *r_is_valid) {
	ClassInfo *class_info = classes.getptr(p_class);
	if (class_info) {
		const ClassDescriptor::Member *member = class_info->class_descriptor->members().getptr(p_property);
		if (member && member->type == ClassDescriptor::Member::Type::PROPERTY) {
			const ClassDescriptor::Member::Property &psg = member->payload.property;
			if (r_is_valid) {
				*r_is_valid = true;
			}

			return psg.index;
		}
	}

	if (r_is_valid) {
		*r_is_valid = false;
	}

	return -1;
}

Variant::Type ClassRegistry::get_property_type(const StringName &p_class, const StringName &p_property, bool *r_is_valid) {
	ClassInfo *class_info = classes.getptr(p_class);
	if (class_info) {
		const ClassDescriptor::Member *member = class_info->class_descriptor->members().getptr(p_property);
		if (member && member->type == ClassDescriptor::Member::Type::PROPERTY) {
			const ClassDescriptor::Member::Property &psg = member->payload.property;
			if (r_is_valid) {
				*r_is_valid = true;
			}

			return psg.property_info->type;
		}
	}

	if (r_is_valid) {
		*r_is_valid = false;
	}

	return Variant::NIL;
}

StringName ClassRegistry::get_property_setter(const StringName &p_class, const StringName &p_property) {
	ClassInfo *class_info = classes.getptr(p_class);
	if (class_info) {
		const ClassDescriptor::Member *member = class_info->class_descriptor->members().getptr(p_property);
		if (member && member->type == ClassDescriptor::Member::Type::PROPERTY) {
			return member->payload.property.setter ? member->payload.property.setter->get_name() : StringName();
		}
	}

	return StringName();
}

StringName ClassRegistry::get_property_getter(const StringName &p_class, const StringName &p_property) {
	ClassInfo *class_info = classes.getptr(p_class);
	if (class_info) {
		const ClassDescriptor::Member *member = class_info->class_descriptor->members().getptr(p_property);
		if (member && member->type == ClassDescriptor::Member::Type::PROPERTY) {
			return member->payload.property.getter ? member->payload.property.getter->get_name() : StringName();
		}
	}

	return StringName();
}

bool ClassRegistry::has_property(const StringName &p_class, const StringName &p_property, bool p_no_inheritance) {
	ClassInfo *class_info = classes.getptr(p_class);
	if (class_info) {
		const ClassDescriptor::Member *member = class_info->class_descriptor->members(p_no_inheritance).getptr(p_property);
		return member ? member->type == ClassDescriptor::Member::Type::PROPERTY : false;
	}

	return false;
}

void ClassRegistry::set_method_flags(const StringName &p_class, const StringName &p_method, int p_flags) {
	Locker::Lock lock(Locker::STATE_WRITE);
	ClassInfo *type = classes.getptr(p_class);
	ERR_FAIL_NULL(type);
	type->class_descriptor->set_method_flags(p_method, p_flags);
}

bool ClassRegistry::has_method(const StringName &p_class, const StringName &p_method, bool p_no_inheritance) {
	ClassInfo *type = classes.getptr(p_class);
	if (!type) {
		return false;
	}
	const ClassDescriptor::Member *member = type->class_descriptor->members(p_no_inheritance).getptr(p_method);
	return member && member->type == ClassDescriptor::Member::Type::METHOD;
}

int ClassRegistry::get_method_argument_count(const StringName &p_class, const StringName &p_method, bool *r_is_valid, bool p_no_inheritance) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *type = classes.getptr(p_class);
	if (type) {
		const ClassDescriptor::Member *member = type->class_descriptor->members(p_no_inheritance).getptr(p_method);
		if (member && member->type == ClassDescriptor::Member::Type::METHOD) {
			if (r_is_valid) {
				*r_is_valid = true;
			}
			return member->payload.method->get_argument_count();
		}
	}

	if (r_is_valid) {
		*r_is_valid = false;
	}

	return 0;
}

void ClassRegistry::bind_method_custom(const StringName &p_class, MethodBind *p_method, bool p_take_ownership) {
	Locker::Lock lock(Locker::STATE_WRITE);

	StringName method_name = p_method->get_name();

	ClassInfo *type = classes.getptr(p_class);
	if (!type) {
		memdelete(p_method);
		ERR_FAIL_MSG(vformat("Couldn't bind custom method '%s' for instance '%s'.", method_name, p_class));
	}

	type->class_descriptor->bind_method(p_method, p_take_ownership);
}

MethodBind *ClassRegistry::_bind_vararg_method(MethodBind *p_bind, const StringName &p_name, const Vector<Variant> &p_default_args) {
	MethodBind *bind = p_bind;
	bind->set_name(p_name);
	bind->set_default_arguments(p_default_args);

	String instance_type = bind->get_instance_class();

	ClassInfo *type = classes.getptr(instance_type);
	if (!type) {
		memdelete(bind);
		ERR_FAIL_NULL_V(type, nullptr);
	}

	return type->class_descriptor->bind_method(bind) ? bind : nullptr;
}

#ifdef DEBUG_ENABLED
MethodBind *ClassRegistry::bind_methodfi(uint32_t p_flags, MethodBind *p_bind, const MethodDefinition &p_method_name, const Variant **p_defs, int p_defcount) {
	StringName mdname = p_method_name.name;
#else
MethodBind *ClassRegistry::bind_methodfi(uint32_t p_flags, MethodBind *p_bind, const char *p_method_name, const Variant **p_defs, int p_defcount) {
	StringName mdname = StringName(p_method_name);
#endif // DEBUG_ENABLED

	Locker::Lock lock(Locker::STATE_WRITE);
	ERR_FAIL_NULL_V(p_bind, nullptr);
	p_bind->set_name(mdname);

	String instance_type = p_bind->get_instance_class();

#ifdef DEBUG_ENABLED

	ERR_FAIL_COND_V_MSG(has_method(instance_type, mdname), nullptr, vformat("Class '%s' already has a method '%s'.", String(instance_type), String(mdname)));
#endif // DEBUG_ENABLED

	ClassInfo *type = classes.getptr(instance_type);
	if (!type) {
		memdelete(p_bind);
		ERR_FAIL_V_MSG(nullptr, vformat("Couldn't bind method '%s' for instance '%s'.", mdname, instance_type));
	}

#ifdef DEBUG_ENABLED

	if (p_method_name.args.size() > p_bind->get_argument_count()) {
		memdelete(p_bind);
		ERR_FAIL_V_MSG(nullptr, vformat("Method definition provides more arguments than the method actually has '%s::%s'.", instance_type, mdname));
	}

	if (p_defcount > p_bind->get_argument_count()) {
		memdelete(p_bind);
		ERR_FAIL_V_MSG(nullptr, vformat("Method definition for '%s::%s' provides more default arguments than the method has arguments.", instance_type, mdname));
	}

	p_bind->set_argument_names(p_method_name.args);
#endif // DEBUG_ENABLED

	if (!type->class_descriptor->bind_method(p_bind)) {
		return nullptr;
	}

	Vector<Variant> defvals;

	defvals.resize(p_defcount);
	for (int i = 0; i < p_defcount; i++) {
		defvals.write[i] = *p_defs[i];
	}

	p_bind->set_default_arguments(defvals);
	p_bind->set_hint_flags(p_flags);
	return p_bind;
}

void ClassRegistry::add_virtual_method(const StringName &p_class, const MethodInfo &p_method, bool p_virtual, const Vector<String> &p_arg_names, bool p_object_core) {
	ERR_FAIL_COND_MSG(!classes.has(p_class), vformat("Request for nonexistent class '%s'.", p_class));

	Locker::Lock lock(Locker::STATE_WRITE);

#ifdef DEBUG_ENABLED
	MethodInfo mi = p_method;
	if (p_virtual) {
		mi.flags |= METHOD_FLAG_VIRTUAL;
	}
	if (p_object_core) {
		mi.flags |= METHOD_FLAG_OBJECT_CORE;
	}

	if (!p_object_core) {
		if (p_arg_names.size() != mi.arguments.size()) {
			WARN_PRINT(vformat("Mismatch argument name count for virtual method: '%s::%s'.", String(p_class), p_method.name));
		} else {
			for (int64_t i = 0; i < p_arg_names.size(); ++i) {
				mi.arguments.write[i].name = p_arg_names[i];
			}
		}
	}

	if (classes[p_class].virtual_methods_map.has(p_method.name)) {
		// overloading not supported
		ERR_FAIL_MSG(vformat("Virtual method already bound '%s::%s'.", String(p_class), p_method.name));
	}
	classes[p_class].virtual_methods.push_back(mi);
	classes[p_class].virtual_methods_map[p_method.name] = mi;

#endif // DEBUG_ENABLED
}

void ClassRegistry::get_virtual_methods(const StringName &p_class, List<MethodInfo> *p_methods, bool p_no_inheritance) {
	ERR_FAIL_COND_MSG(!classes.has(p_class), vformat("Request for nonexistent class '%s'.", p_class));

#ifdef DEBUG_ENABLED

	ClassInfo *type = classes.getptr(p_class);
	ClassInfo *check = type;
	while (check) {
		for (const MethodInfo &E : check->virtual_methods) {
			p_methods->push_back(E);
		}

		if (p_no_inheritance) {
			return;
		}
		check = check->inherits_ptr;
	}

#endif // DEBUG_ENABLED
}

void ClassRegistry::add_extension_class_virtual_method(const StringName &p_class, const NativeExtensionClassVirtualMethodInfo *p_method_info) {
	ERR_FAIL_COND_MSG(!classes.has(p_class), vformat("Request for nonexistent class '%s'.", p_class));

#ifdef DEBUG_ENABLED
	PackedStringArray arg_names;

	MethodInfo mi;
	mi.name = *reinterpret_cast<StringName *>(p_method_info->name);
	mi.return_val = PropertyInfo(p_method_info->return_value);
	mi.return_val_metadata = p_method_info->return_value_metadata;
	mi.flags = p_method_info->method_flags;
	for (int i = 0; i < (int)p_method_info->argument_count; i++) {
		PropertyInfo arg(p_method_info->arguments[i]);
		mi.arguments.push_back(arg);
		mi.arguments_metadata.push_back(p_method_info->arguments_metadata[i]);
		arg_names.push_back(arg.name);
	}

	add_virtual_method(p_class, mi, true, arg_names);
#endif // DEBUG_ENABLED
}

void ClassRegistry::set_class_enabled(const StringName &p_class, bool p_enable) {
	Locker::Lock lock(Locker::STATE_WRITE);

	ERR_FAIL_COND_MSG(!classes.has(p_class), vformat("Request for nonexistent class '%s'.", p_class));
	classes[p_class].disabled = !p_enable;
}

bool ClassRegistry::is_class_enabled(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *ti = classes.getptr(p_class);
	ERR_FAIL_NULL_V_MSG(ti, false, vformat("Cannot get class '%s'.", String(p_class)));
	return !ti->disabled;
}

bool ClassRegistry::is_class_exposed(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *ti = classes.getptr(p_class);
	ERR_FAIL_NULL_V_MSG(ti, false, vformat("Cannot get class '%s'.", String(p_class)));
	return ti->exposed;
}

bool ClassRegistry::is_class_reloadable(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *ti = classes.getptr(p_class);
	ERR_FAIL_NULL_V_MSG(ti, false, vformat("Cannot get class '%s'.", String(p_class)));
	return ti->reloadable;
}

bool ClassRegistry::is_class_runtime(const StringName &p_class) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *ti = classes.getptr(p_class);
	ERR_FAIL_NULL_V_MSG(ti, false, vformat("Cannot get class '%s'.", String(p_class)));
	return ti->is_runtime;
}

#ifdef TOOLS_ENABLED
void ClassRegistry::add_class_dependency(const StringName &p_class, const StringName &p_dependency) {
	Locker::Lock lock(Locker::STATE_WRITE);

	ERR_FAIL_COND_MSG(!classes.has(p_class), vformat("Request for nonexistent class '%s'.", p_class));
	if (classes[p_class].dependency_list.find(p_dependency)) {
		ERR_FAIL();
	}

	classes[p_class].dependency_list.push_back(p_dependency);
}

void ClassRegistry::get_class_dependencies(const StringName &p_class, List<StringName> *r_rependencies) {
	Locker::Lock lock(Locker::STATE_READ);

	ClassInfo *ti = classes.getptr(p_class);
	ERR_FAIL_NULL_MSG(ti, vformat("Cannot get class '%s'.", String(p_class)));

	for (const StringName &dep : ti->dependency_list) {
		r_rependencies->push_back(dep);
	}
}
#endif // TOOLS_ENABLED

void ClassRegistry::add_resource_base_extension(const StringName &p_extension, const StringName &p_class) {
	if (resource_base_extensions.has(p_extension)) {
		return;
	}

	resource_base_extensions[p_extension] = p_class;
}

void ClassRegistry::get_resource_base_extensions(List<String> *p_extensions) {
	for (const KeyValue<StringName, StringName> &E : resource_base_extensions) {
		p_extensions->push_back(E.key);
	}
}

bool ClassRegistry::is_resource_extension(const StringName &p_extension) {
	return resource_base_extensions.has(p_extension);
}

void ClassRegistry::get_extensions_for_type(const StringName &p_class, List<String> *p_extensions) {
	for (const KeyValue<StringName, StringName> &E : resource_base_extensions) {
		if (is_parent_class(p_class, E.value) || is_parent_class(E.value, p_class)) {
			p_extensions->push_back(E.key);
		}
	}
}

HashMap<StringName, HashMap<StringName, Variant>> ClassRegistry::default_values;
HashSet<StringName> ClassRegistry::default_values_cached;

// Building the object and reading its properties runs arbitrary code. That code
// can wait on a thread that wants the registry lock, so the caller holds no lock.
static HashMap<StringName, Variant> _read_default_values(const StringName &p_class) {
	HashMap<StringName, Variant> values;
	Object *c = nullptr;
	bool cleanup_c = false;

	if (Engine::get_singleton()->has_singleton(p_class)) {
		c = Engine::get_singleton()->get_singleton_object(p_class);
	} else if (ClassRegistry::can_instantiate(p_class) && !ClassRegistry::is_virtual(p_class)) { // Keep this condition in sync with doc_tools.cpp get_documentation_default_value.
		c = ClassRegistry::instantiate_no_placeholders(p_class);
		cleanup_c = true;
	}
	if (!c) {
		return values;
	}

	List<PropertyInfo> plist;
	c->get_property_list(&plist);
	for (const PropertyInfo &E : plist) {
		if ((E.usage & (PROPERTY_USAGE_STORAGE | PROPERTY_USAGE_EDITOR)) && !values.has(E.name)) {
			values[E.name] = c->get(E.name);
		}
	}

	if (cleanup_c) {
		memdelete(c);
	}
	return values;
}

Variant ClassRegistry::class_get_default_property_value(const StringName &p_class, const StringName &p_property, bool *r_valid) {
	bool cached;
	{
		Locker::Lock lock(Locker::STATE_READ);
		cached = default_values_cached.has(p_class);
	}

	if (!cached) {
		const HashMap<StringName, Variant> values = _read_default_values(p_class);
		Locker::Lock lock(Locker::STATE_WRITE);
		// Another thread may have filled the cache while this one read the values.
		if (!default_values_cached.has(p_class)) {
			HashMap<StringName, Variant> &stored = default_values[p_class];
			for (const KeyValue<StringName, Variant> &E : values) {
				if (!stored.has(E.key)) {
					stored[E.key] = E.value;
				}
			}
			default_values_cached.insert(p_class);
		}
	}

	Locker::Lock lock(Locker::STATE_READ);
	if (!default_values.has(p_class)) {
		if (r_valid != nullptr) {
			*r_valid = false;
		}
		return Variant();
	}

	if (!default_values[p_class].has(p_property)) {
		if (r_valid != nullptr) {
			*r_valid = false;
		}
		return Variant();
	}

	if (r_valid != nullptr) {
		*r_valid = true;
	}

	Variant var = default_values[p_class][p_property];

#ifdef DEBUG_ENABLED
	// Some properties may have an instantiated Object as default value,
	// (like Path2D's `curve` used to have), but that's not a good practice.
	// Instead, those properties should use PROPERTY_USAGE_EDITOR_INSTANTIATE_OBJECT
	// to be auto-instantiated when created in the editor with the following method:
	// EditorNode::get_editor_data().instantiate_object_properties(obj);
	if (var.get_type() == Variant::OBJECT) {
		Object *obj = var.get_validated_object();
		if (obj) {
			WARN_PRINT(vformat("Instantiated %s used as default value for %s's \"%s\" property.", obj->get_class(), p_class, p_property));
		}
	}
#endif // DEBUG_ENABLED

	return var;
}

void ClassRegistry::register_extension_class(ObjectNativeExtension *p_extension) {
	GLOBAL_LOCK_FUNCTION;

	ERR_FAIL_COND_MSG(classes.has(p_extension->class_name), vformat("Class already registered: '%s'.", String(p_extension->class_name)));
	ERR_FAIL_COND_MSG(!classes.has(p_extension->parent_class_name), vformat("Parent class name for extension class not found: '%s'.", String(p_extension->parent_class_name)));

	ClassInfo *parent = classes.getptr(p_extension->parent_class_name);

#ifdef TOOLS_ENABLED
	// @todo This is a limitation of the current implementation, but it should be possible to remove.
	ERR_FAIL_COND_MSG(p_extension->is_runtime && parent->native_extension && !parent->is_runtime, vformat("Extension runtime class '%s' cannot descend from '%s' which isn't also a runtime class.", String(p_extension->class_name), parent->class_descriptor->get_name()));
#endif

	ClassInfo c;
	c.api = p_extension->editor_class ? API_EDITOR_EXTENSION : API_EXTENSION;
	c.native_extension = p_extension;
	c.is_virtual = p_extension->is_virtual;
	if (!p_extension->is_abstract) {
		// Find the closest ancestor which is either non-abstract or native (or both).
		ClassInfo *concrete_ancestor = parent;
		while (concrete_ancestor->creation_func == nullptr &&
				concrete_ancestor->inherits_ptr != nullptr &&
				concrete_ancestor->native_extension != nullptr) {
			concrete_ancestor = concrete_ancestor->inherits_ptr;
		}
		ERR_FAIL_NULL_MSG(concrete_ancestor->creation_func, vformat("Extension class '%s' cannot extend native abstract class '%s'.", String(p_extension->class_name), String(concrete_ancestor->class_descriptor->get_name())));
		c.creation_func = concrete_ancestor->creation_func;
	}
	c.class_ptr = parent->class_ptr;
	c.inherits_ptr = parent;
	c.exposed = p_extension->is_exposed;
	if (c.exposed) {
		// The parent classes should be exposed if it has an exposed child class.
		while (parent && !parent->exposed) {
			parent->exposed = true;
			parent = classes.getptr(parent->class_descriptor->get_super_type_name());
		}
	}
	c.reloadable = p_extension->reloadable;
#ifdef TOOLS_ENABLED
	c.is_runtime = p_extension->is_runtime;
#endif

	c.class_descriptor = memnew(ClassDescriptor(c.inherits_ptr->class_descriptor, p_extension->class_name));
	c.class_descriptor->initialize();
	p_extension->class_descriptor = c.class_descriptor;

	classes[p_extension->class_name] = c;
}

static LocalVector<ClassDescriptor *> class_descriptor_leaked_autorelease_pool;

void ClassRegistry::unregister_extension_class(const StringName &p_class) {
	ClassInfo *c = classes.getptr(p_class);
	ERR_FAIL_NULL_MSG(c, vformat("Class '%s' does not exist.", String(p_class)));
	// Leak the ClassDescriptor until exit so potential consumers don't have dangling pointers.
	// Its default arguments go now, while the extension can still free the objects in them.
	c->class_descriptor->release_default_arguments();
	class_descriptor_leaked_autorelease_pool.push_back(c->class_descriptor);
	classes.erase(p_class);
	default_values_cached.erase(p_class);
	default_values.erase(p_class);
#ifdef TOOLS_ENABLED
	placeholder_extensions.erase(p_class);
#endif
}

HashMap<StringName, ClassRegistry::NativeStruct> ClassRegistry::native_structs;
void ClassRegistry::register_native_struct(const StringName &p_name, const String &p_code, uint64_t p_current_size) {
	NativeStruct ns;
	ns.ccode = p_code;
	ns.struct_size = p_current_size;
	native_structs[p_name] = ns;
}

void ClassRegistry::get_native_struct_list(List<StringName> *r_names) {
	for (const KeyValue<StringName, NativeStruct> &E : native_structs) {
		r_names->push_back(E.key);
	}
}

String ClassRegistry::get_native_struct_code(const StringName &p_name) {
	ERR_FAIL_COND_V(!native_structs.has(p_name), String());
	return native_structs[p_name].ccode;
}

uint64_t ClassRegistry::get_native_struct_size(const StringName &p_name) {
	ERR_FAIL_COND_V(!native_structs.has(p_name), 0);
	return native_structs[p_name].struct_size;
}

Object *ClassRegistry::_instantiate_allow_unexposed(const StringName &p_class) {
	return _instantiate_internal(p_class, false, true, false);
}

void ClassRegistry::cleanup_defaults() {
	default_values.clear();
	default_values_cached.clear();
}

LocalVector<ClassDescriptor **> ClassRegistry::class_descriptor_autorelease_pool;
void ClassRegistry::cleanup() {
	//OBJTYPE_LOCK; hah not here

	for (KeyValue<StringName, ClassInfo> &E : classes) {
		if (E.value.native_extension) {
			WARN_PRINT(vformat("Extension class '%s' is still registered at exit; its NativeExtension did not unregister it.", E.key));
			class_descriptor_leaked_autorelease_pool.push_back(E.value.class_descriptor); // We created it; we need to clear it.
		}
	}

	classes.clear();
	resource_base_extensions.clear();
	native_structs.clear();

	for (ClassDescriptor **type : class_descriptor_autorelease_pool) {
		if (!*type) {
			WARN_PRINT("ClassDescriptor in autorelease pool was cleaned up before being auto-released. Ignoring.");
			continue;
		}
		memdelete(*type);
		*type = nullptr;
	}
	class_descriptor_autorelease_pool.clear();

	for (ClassDescriptor *type : class_descriptor_leaked_autorelease_pool) {
		memdelete(type);
	}
	class_descriptor_leaked_autorelease_pool.clear();
}

// Array to use in optional parameters on methods and the DEFVAL_ARRAY macro.
Array ClassRegistry::default_array_arg = Array::create_read_only();

bool ClassRegistry::is_default_array_arg(const Array &p_array) {
	return p_array.is_same_instance(default_array_arg);
}

//

ClassRegistry::Locker::Lock::Lock(Locker::State p_state) {
	DEV_ASSERT(p_state != STATE_UNLOCKED);
	if (p_state == STATE_READ) {
		if (Locker::thread_state == STATE_UNLOCKED) {
			state = STATE_READ;
			Locker::thread_state = STATE_READ;
			Locker::lock.read_lock();
		}
	} else if (p_state == STATE_WRITE) {
		if (Locker::thread_state == STATE_UNLOCKED) {
			state = STATE_WRITE;
			Locker::thread_state = STATE_WRITE;
			Locker::lock.write_lock();
		} else if (Locker::thread_state == STATE_READ) {
			CRASH_NOW_MSG("Lock can't be upgraded from read to write.");
		}
	}
}

ClassRegistry::Locker::Lock::~Lock() {
	if (state == STATE_READ) {
		Locker::lock.read_unlock();
		Locker::thread_state = STATE_UNLOCKED;
	} else if (state == STATE_WRITE) {
		Locker::lock.write_unlock();
		Locker::thread_state = STATE_UNLOCKED;
	}
}

#undef ERR_FAIL_NO_CLASS
