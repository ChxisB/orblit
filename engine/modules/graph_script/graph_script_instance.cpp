/**************************************************************************/
/*  graph_script_instance.cpp                                             */

#include "graph_script_instance.h"

#include "graph_call.h"
#include "graph_script_language.h"

#include "core/config/project_settings.h"

namespace {

// A float may go into an int variable, a string may not. A variable of type nil holds anything.
bool convert_to(Variant::Type p_type, const Variant &p_value, Variant &r_converted) {
	if (p_type == Variant::NIL || p_value.get_type() == p_type) {
		r_converted = p_value;
		return true;
	}
	if (!Variant::can_convert_strict(p_value.get_type(), p_type)) {
		return false;
	}
	const Variant *args[1] = { &p_value };
	Callable::CallError error;
	Variant::construct(p_type, r_converted, args, 1, error);
	return error.error == Callable::CallError::CALL_OK;
}

} // namespace

GraphScriptInstance::GraphScriptInstance(const Ref<GraphScript> &p_script, Object *p_owner) :
		script(p_script), owner(p_owner) {
	random.instantiate();
	List<PropertyInfo> properties;
	script->get_script_property_list(&properties);
	for (const PropertyInfo &property : properties) {
		Variant default_value;
		script->get_property_default_value(property.name, default_value);
		// An array or dictionary default must not be shared between instances.
		values[property.name] = default_value.duplicate(true);
	}
}

bool GraphScriptInstance::set(const StringName &p_name, const Variant &p_value) {
	const GraphVariable *variable = script->get_variable(p_name);
	Variant converted;
	if (!variable || !convert_to(variable->type, p_value, converted)) {
		return false;
	}
	values[p_name] = converted;
	return true;
}

bool GraphScriptInstance::get(const StringName &p_name, Variant &r_ret) const {
	const GraphVariable *variable = script->get_variable(p_name);
	if (!variable) {
		return false;
	}
	const HashMap<StringName, Variant>::ConstIterator found = values.find(p_name);
	r_ret = found ? found->value : variable->default_value;
	return true;
}

void GraphScriptInstance::get_property_list(List<PropertyInfo> *r_properties) const {
	script->get_script_property_list(r_properties);
}

Variant::Type GraphScriptInstance::get_property_type(const StringName &p_name, bool *r_is_valid) const {
	const GraphVariable *variable = script->get_variable(p_name);
	if (r_is_valid) {
		*r_is_valid = variable != nullptr;
	}
	return variable ? variable->type : Variant::NIL;
}

bool GraphScriptInstance::property_can_revert(const StringName &p_name) const {
	return script->get_variable(p_name) != nullptr;
}

bool GraphScriptInstance::property_get_revert(const StringName &p_name, Variant &r_ret) const {
	return script->get_property_default_value(p_name, r_ret);
}

void GraphScriptInstance::get_method_list(List<MethodInfo> *r_list) const {
	script->get_script_method_list(r_list);
}

bool GraphScriptInstance::has_method(const StringName &p_method) const {
	return script->has_method(p_method);
}

Variant GraphScriptInstance::callp(const StringName &p_method, const Variant **p_args, int p_argcount, Callable::CallError &r_error) {
	const GraphCompiledFunction *function = script->get_compiled(p_method);
	if (!function) {
		r_error.error = Callable::CallError::CALL_ERROR_INVALID_METHOD;
		return Variant();
	}
	// A function takes no arguments yet, so a call that passes one is refused.
	if (p_argcount > 0) {
		r_error.error = Callable::CallError::CALL_ERROR_TOO_MANY_ARGUMENTS;
		r_error.expected = 0;
		return Variant();
	}
	r_error.error = Callable::CallError::CALL_OK;
	if (!function->error.is_empty()) {
		GraphScriptLanguage::get_singleton()->report_error(function->error);
		return Variant();
	}
	GraphCall call(*this, *function, GLOBAL_GET_CACHED(int64_t, "graph_script/runtime/step_budget"));
	return call.run();
}

ScriptLanguage *GraphScriptInstance::get_language() {
	return GraphScriptLanguage::get_singleton();
}
