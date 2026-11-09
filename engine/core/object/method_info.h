/**************************************************************************/
/*  method_info.h                                                         */

#pragma once

#include "core/object/property_info.h"
#include "core/variant/variant.h"

enum MethodFlags {
	METHOD_FLAG_NORMAL = 1,
	METHOD_FLAG_EDITOR = 2,
	METHOD_FLAG_CONST = 4,
	METHOD_FLAG_VIRTUAL = 8,
	METHOD_FLAG_VARARG = 16,
	METHOD_FLAG_STATIC = 32,
	METHOD_FLAG_OBJECT_CORE = 64,
	METHOD_FLAG_VIRTUAL_REQUIRED = 128,
	METHOD_FLAGS_DEFAULT = METHOD_FLAG_NORMAL,
};

struct MethodInfo {
	String name;
	PropertyInfo return_val;
	uint32_t flags = METHOD_FLAGS_DEFAULT;
	int id = 0;
	Vector<PropertyInfo> arguments;
	Vector<Variant> default_arguments;
	int return_val_metadata = 0;
	Vector<int> arguments_metadata;

	int get_argument_meta(int p_arg) const {
		ERR_FAIL_COND_V(p_arg < -1 || p_arg > arguments.size(), 0);
		if (p_arg == -1) {
			return return_val_metadata;
		}
		return arguments_metadata.size() > p_arg ? arguments_metadata[p_arg] : 0;
	}

	inline bool operator==(const MethodInfo &p_method) const { return id == p_method.id && name == p_method.name; }
	inline bool operator<(const MethodInfo &p_method) const { return id == p_method.id ? (name < p_method.name) : (id < p_method.id); }

	operator Dictionary() const;

	static MethodInfo from_dict(const Dictionary &p_dict);

	uint32_t get_method_hash() const;

	MethodInfo() {}

	explicit MethodInfo(const NativeExtensionMethodInfo &p_info) :
			name(*reinterpret_cast<StringName *>(p_info.name)),
			return_val(PropertyInfo(p_info.return_value)),
			flags(p_info.flags),
			id(p_info.id) {
		for (uint32_t i = 0; i < p_info.argument_count; i++) {
			arguments.push_back(PropertyInfo(p_info.arguments[i]));
		}
		const Variant *def_values = (const Variant *)p_info.default_arguments;
		for (uint32_t j = 0; j < p_info.default_argument_count; j++) {
			default_arguments.push_back(def_values[j]);
		}
	}

	MethodInfo(const String &p_name) { name = p_name; }

	template <typename... VarArgs>
	MethodInfo(const String &p_name, VarArgs... p_params) {
		name = p_name;
		arguments = Vector<PropertyInfo>{ p_params... };
	}

	MethodInfo(Variant::Type p_ret) { return_val.type = p_ret; }
	MethodInfo(Variant::Type p_ret, const String &p_name) {
		return_val.type = p_ret;
		name = p_name;
	}

	template <typename... VarArgs>
	MethodInfo(Variant::Type p_ret, const String &p_name, VarArgs... p_params) {
		name = p_name;
		return_val.type = p_ret;
		arguments = Vector<PropertyInfo>{ p_params... };
	}

	MethodInfo(const PropertyInfo &p_ret, const String &p_name) {
		return_val = p_ret;
		name = p_name;
	}

	template <typename... VarArgs>
	MethodInfo(const PropertyInfo &p_ret, const String &p_name, VarArgs... p_params) {
		return_val = p_ret;
		name = p_name;
		arguments = Vector<PropertyInfo>{ p_params... };
	}

	// Need to be declared because ~MethodInfo is specified.
	MethodInfo(const MethodInfo &) = default;
	MethodInfo(MethodInfo &&) = default;
	MethodInfo &operator=(const MethodInfo &) = default;
	MethodInfo &operator=(MethodInfo &&) = default;

	// This is _NO_INLINE_ to save on binary size.
	_NO_INLINE_ ~MethodInfo() = default;
};
