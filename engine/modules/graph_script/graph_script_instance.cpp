/**************************************************************************/
/*  graph_script_instance.cpp                                             */

#include "graph_script_instance.h"

#include "graph_call.h"
#include "graph_script_language.h"

#include "core/config/project_settings.h"
#include "scene/stage/entity.h"

namespace {

// Converts the arguments of a call to the types the function declares. Returns false and sets
// the error when the count or a type is wrong.
bool convert_arguments(const GraphSignature &p_signature, const Variant **p_args, int p_argcount, Vector<Variant> &r_arguments, Callable::CallError &r_error) {
	const int expected = p_signature.arguments.size();
	if (p_argcount != expected) {
		r_error.error = p_argcount < expected ? Callable::CallError::CALL_ERROR_TOO_FEW_ARGUMENTS : Callable::CallError::CALL_ERROR_TOO_MANY_ARGUMENTS;
		r_error.expected = expected;
		return false;
	}
	for (int i = 0; i < expected; i++) {
		Variant converted;
		if (!GraphSignatures::convert(p_signature.arguments[i].type, *p_args[i], converted)) {
			r_error.error = Callable::CallError::CALL_ERROR_INVALID_ARGUMENT;
			r_error.argument = i;
			r_error.expected = p_signature.arguments[i].type;
			return false;
		}
		r_arguments.push_back(converted);
	}
	return true;
}

} // namespace

GraphScriptInstance::GraphScriptInstance(const Ref<GraphScript> &p_script, Object *p_owner) :
		script(p_script), owner(p_owner) {
	random.instantiate();
	for (const GraphVariable &variable : script->get_declared_variables()) {
		// An array or dictionary default must not be shared between instances.
		values[variable.name] = variable.default_value.duplicate(true);
	}
	script->add_instance(this);
}

GraphScriptInstance::~GraphScriptInstance() {
	_drop_waiting();
	script->remove_instance(this);
}

void GraphScriptInstance::_drop_waiting() {
	// A drop never touches the list, so it is safe to walk it.
	for (const Ref<GraphCall> &call : waiting) {
		call->drop();
	}
	waiting.clear();
}

void GraphScriptInstance::add_waiting(const Ref<GraphCall> &p_call) {
	waiting.push_back(p_call);
}

void GraphScriptInstance::remove_waiting(const GraphCall *p_call) {
	for (int i = 0; i < waiting.size(); i++) {
		if (waiting[i].ptr() == p_call) {
			waiting.remove_at(i);
			return;
		}
	}
}

void GraphScriptInstance::drop_waiting_calls() {
	if (waiting.is_empty()) {
		return;
	}
	GraphScriptLanguage::get_singleton()->report_error(vformat("The script changed while %d calls were waiting. The calls are dropped.", waiting.size()));
	_drop_waiting();
}

void GraphScriptInstance::refresh() {
	HashMap<StringName, Variant> kept;
	for (const GraphVariable &variable : script->get_declared_variables()) {
		const HashMap<StringName, Variant>::ConstIterator found = values.find(variable.name);
		// The type nil holds any value.
		const bool keeps = found && (variable.type == Variant::NIL || found->value.get_type() == variable.type);
		kept[variable.name] = keeps ? found->value : variable.default_value.duplicate(true);
	}
	values = kept;
}

bool GraphScriptInstance::set(const StringName &p_name, const Variant &p_value) {
	const GraphVariable *variable = script->get_variable(p_name);
	Variant converted;
	if (!variable || !GraphSignatures::convert(variable->type, p_value, converted)) {
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
	r_error.error = Callable::CallError::CALL_OK;
	if (!function->error.is_empty()) {
		GraphScriptLanguage::get_singleton()->report_error(function->error);
		return Variant();
	}
	Vector<Variant> arguments;
	if (!convert_arguments(function->signature, p_args, p_argcount, arguments, r_error)) {
		return Variant();
	}
	if (too_deep || depth >= MAX_CALL_DEPTH) {
		if (!too_deep) {
			GraphScriptLanguage::get_singleton()->report_error(vformat("Function '%s' calls itself deeper than %d levels. The call is dropped.", p_method, MAX_CALL_DEPTH));
		}
		too_deep = true;
		return Variant();
	}
	depth++;
	Ref<GraphCall> call;
	call.instantiate(this, function, GLOBAL_GET_CACHED(int64_t, "graph_script/runtime/step_budget"));
	const Variant result = call->run(arguments);
	depth--;
	too_deep = too_deep && depth > 0;
	// A call that waits goes to its caller. The caller may await it, and the instance keeps it.
	return call->is_waiting() ? Variant(call) : result;
}

// A function that starts at a signal received step listens from the moment the entity starts.
// shortcut: it connects once and never again, so an emitter that appears later is not heard.
// Add a retry when the entity enters the stage again, if someone needs a late emitter.
void GraphScriptInstance::_connect_receiver(Entity *p_owner, const GraphCompiledFunction &p_function) {
	const GraphSignature &signature = p_function.signature;
	Object *emitter = p_owner;
	if (!signature.target.is_empty()) {
		emitter = p_owner->get_entity_or_null(EntityPath(signature.target));
	}
	if (!emitter || !emitter->has_signal(signature.signal)) {
		GraphScriptLanguage::get_singleton()->report_error(vformat("Function '%s' listens to the signal '%s', which its emitter does not have.", p_function.name, signature.signal));
		return;
	}
	const Callable receiver(p_owner, p_function.name);
	if (!emitter->is_connected(signature.signal, receiver)) {
		emitter->connect(signature.signal, receiver);
	}
}

void GraphScriptInstance::notification(int p_notification, bool p_reversed) {
	Entity *entity = Object::cast_to<Entity>(owner);
	if (p_notification != Entity::NOTIFICATION_START || !entity) {
		return;
	}
	for (const GraphCompiledFunction &function : script->get_compiled_functions()) {
		if (!function.signature.signal.is_empty()) {
			_connect_receiver(entity, function);
		}
	}
}

ScriptLanguage *GraphScriptInstance::get_language() {
	return GraphScriptLanguage::get_singleton();
}
