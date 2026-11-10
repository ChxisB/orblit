/**************************************************************************/
/*  script_instance.cpp                                                   */

#include "script_instance.h"

#include "core/object/script_language.h"

int ScriptInstance::get_method_argument_count(const StringName &p_method, bool *r_is_valid) const {
	// Default implementation simply traverses hierarchy.
	Ref<Script> script = get_script();
	while (script.is_valid()) {
		bool valid = false;
		int ret = script->get_script_method_argument_count(p_method, &valid);
		if (valid) {
			if (r_is_valid) {
				*r_is_valid = true;
			}
			return ret;
		}

		script = script->get_base_script();
	}

	if (r_is_valid) {
		*r_is_valid = false;
	}
	return 0;
}

Variant ScriptInstance::call_const(const StringName &p_method, const Variant **p_args, int p_argcount, Callable::CallError &r_error) {
	return callp(p_method, p_args, p_argcount, r_error);
}

void ScriptInstance::get_property_state(List<Pair<StringName, Variant>> &r_state) {
	List<PropertyInfo> pinfo;
	get_property_list(&pinfo);
	for (const PropertyInfo &E : pinfo) {
		if (E.usage & PROPERTY_USAGE_STORAGE) {
			Pair<StringName, Variant> p;
			p.first = E.name;
			if (get(p.first, p.second)) {
				r_state.push_back(p);
			}
		}
	}
}

void ScriptInstance::property_set_fallback(const StringName &, const Variant &, bool *r_valid) {
	if (r_valid) {
		*r_valid = false;
	}
}

Variant ScriptInstance::property_get_fallback(const StringName &, bool *r_valid) {
	if (r_valid) {
		*r_valid = false;
	}
	return Variant();
}

const Variant ScriptInstance::get_rpc_config() const {
	return get_script()->get_rpc_config();
}
