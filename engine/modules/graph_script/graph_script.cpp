/**************************************************************************/
/*  graph_script.cpp                                                      */

#include "graph_script.h"

#include "graph_compiler.h"
#include "graph_script_instance.h"
#include "graph_script_language.h"

#include "core/object/class_registry.h"
#include "core/variant/variant_parser.h"

namespace {

bool is_valid_name(const String &p_name) {
	return p_name.is_valid_unicode_identifier();
}

bool is_valid_type(int p_type) {
	return p_type >= Variant::NIL && p_type < Variant::VARIANT_MAX;
}

// Returns false when the text is not a Variant. Text of "null" parses to nil and is fine.
bool parse_variant(const String &p_text, Variant &r_value) {
	VariantParser::StreamString stream;
	stream.s = p_text;
	String error_text;
	int error_line = 0;
	return VariantParser::parse(&stream, r_value, error_text, error_line) == OK;
}

Variant default_for_type(Variant::Type p_type) {
	Variant value;
	Callable::CallError call_error;
	Variant::construct(p_type, value, nullptr, 0, call_error);
	return value;
}

Error parse_variable(const Variant &p_value, GraphVariable &r_variable) {
	if (p_value.get_type() != Variant::DICTIONARY) {
		return ERR_INVALID_DATA;
	}
	const Dictionary entry = p_value;
	if (entry.get("name", Variant()).get_type() != Variant::STRING || entry.get("type", Variant()).get_type() != Variant::INT || entry.get("exported", Variant()).get_type() != Variant::BOOL) {
		return ERR_INVALID_DATA;
	}
	const String name = entry["name"];
	const int type = entry["type"];
	if (!is_valid_name(name) || !is_valid_type(type) || !entry.has("default")) {
		return ERR_INVALID_DATA;
	}
	r_variable.name = name;
	r_variable.type = Variant::Type(type);
	r_variable.default_value = entry["default"];
	r_variable.exported = entry["exported"];
	return OK;
}

Dictionary variable_to_dictionary(const GraphVariable &p_variable) {
	Dictionary entry;
	entry["name"] = String(p_variable.name);
	entry["type"] = int(p_variable.type);
	entry["default"] = p_variable.default_value;
	entry["exported"] = p_variable.exported;
	return entry;
}

} // namespace

int GraphScript::_find_function_index(const String &p_name) const {
	for (int i = 0; i < functions.size(); i++) {
		if (functions[i].get_name() == p_name) {
			return i;
		}
	}
	return -1;
}

int GraphScript::_find_variable_index(const StringName &p_name) const {
	for (int i = 0; i < variables.size(); i++) {
		if (variables[i].name == p_name) {
			return i;
		}
	}
	return -1;
}

int GraphScript::_find_signal_index(const StringName &p_name) const {
	return signals.find(p_name);
}

void GraphScript::_compile() {
	Vector<StringName> names;
	for (const GraphVariable &variable : variables) {
		names.push_back(variable.name);
	}
	compiled.clear();
	error = String();
	for (const GraphFunction &function : functions) {
		compiled.push_back(GraphCompiler::compile(function, names));
		if (error.is_empty()) {
			error = compiled[compiled.size() - 1].error;
		}
	}
}

Error GraphScript::_commit(Error p_error) {
	if (p_error == OK) {
		_compile();
		emit_changed();
	}
	return p_error;
}

const GraphCompiledFunction *GraphScript::get_compiled(const StringName &p_function) const {
	for (const GraphCompiledFunction &function : compiled) {
		if (function.name == p_function) {
			return &function;
		}
	}
	return nullptr;
}

const GraphFunction *GraphScript::get_function(const String &p_name) const {
	const int index = _find_function_index(p_name);
	return index < 0 ? nullptr : &functions[index];
}

const GraphVariable *GraphScript::get_variable(const StringName &p_name) const {
	const int index = _find_variable_index(p_name);
	return index < 0 ? nullptr : &variables[index];
}

int GraphScript::get_valid_step_id(const String &p_function) const {
	const GraphFunction *function = get_function(p_function);
	return function ? function->get_valid_step_id() : -1;
}

Error GraphScript::add_function(const String &p_name) {
	if (!is_valid_name(p_name)) {
		return ERR_INVALID_PARAMETER;
	}
	if (_find_function_index(p_name) >= 0) {
		return ERR_ALREADY_EXISTS;
	}
	functions.push_back(GraphFunction(p_name));
	return _commit(OK);
}

Error GraphScript::remove_function(const String &p_name) {
	const int index = _find_function_index(p_name);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	functions.remove_at(index);
	return _commit(OK);
}

Error GraphScript::add_step(const String &p_function, int p_id, const String &p_kind) {
	return _edit_function(p_function, [&](GraphFunction &p_target) { return p_target.add_step(p_id, StringName(p_kind)); });
}

Error GraphScript::remove_step(const String &p_function, int p_id) {
	return _edit_function(p_function, [&](GraphFunction &p_target) { return p_target.remove_step(p_id); });
}

Error GraphScript::move_step(const String &p_function, int p_id, float p_x, float p_y) {
	return _edit_function(p_function, [&](GraphFunction &p_target) { return p_target.move_step(p_id, Vector2(p_x, p_y)); });
}

Error GraphScript::set_step_parameter(const String &p_function, int p_id, const String &p_parameter, const String &p_value_text) {
	Variant value;
	if (!parse_variant(p_value_text, value)) {
		return ERR_PARSE_ERROR;
	}
	return _edit_function(p_function, [&](GraphFunction &p_target) { return p_target.set_step_parameter(p_id, StringName(p_parameter), value); });
}

Error GraphScript::set_step_pins(const String &p_function, int p_id, int p_count) {
	return _edit_function(p_function, [&](GraphFunction &p_target) { return p_target.set_pin_count(p_id, p_count); });
}

Error GraphScript::connect_steps(const String &p_function, int p_from_step, int p_from_port, int p_to_step, int p_to_port) {
	const GraphWire wire = { p_from_step, p_from_port, p_to_step, p_to_port };
	return _edit_function(p_function, [&](GraphFunction &p_target) { return p_target.connect_steps(wire); });
}

Error GraphScript::disconnect_steps(const String &p_function, int p_from_step, int p_from_port, int p_to_step, int p_to_port) {
	const GraphWire wire = { p_from_step, p_from_port, p_to_step, p_to_port };
	return _edit_function(p_function, [&](GraphFunction &p_target) { return p_target.disconnect_steps(wire); });
}

Error GraphScript::add_variable(const String &p_name, int p_type) {
	if (!is_valid_name(p_name) || !is_valid_type(p_type)) {
		return ERR_INVALID_PARAMETER;
	}
	if (_find_variable_index(p_name) >= 0) {
		return ERR_ALREADY_EXISTS;
	}
	GraphVariable variable;
	variable.name = p_name;
	variable.type = Variant::Type(p_type);
	variable.default_value = default_for_type(variable.type);
	variables.push_back(variable);
	return _commit(OK);
}

Error GraphScript::remove_variable(const String &p_name) {
	const int index = _find_variable_index(p_name);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	variables.remove_at(index);
	return _commit(OK);
}

Error GraphScript::set_variable_default(const String &p_name, const String &p_value_text) {
	const int index = _find_variable_index(p_name);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	Variant value;
	if (!parse_variant(p_value_text, value)) {
		return ERR_PARSE_ERROR;
	}
	// Nil is the type of a variable that holds any value.
	if (variables[index].type != Variant::NIL && value.get_type() != variables[index].type) {
		return ERR_INVALID_PARAMETER;
	}
	variables.write[index].default_value = value;
	return _commit(OK);
}

Error GraphScript::set_variable_exported(const String &p_name, bool p_exported) {
	const int index = _find_variable_index(p_name);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	variables.write[index].exported = p_exported;
	return _commit(OK);
}

Error GraphScript::add_signal(const String &p_name) {
	if (!is_valid_name(p_name)) {
		return ERR_INVALID_PARAMETER;
	}
	if (_find_signal_index(p_name) >= 0) {
		return ERR_ALREADY_EXISTS;
	}
	signals.push_back(p_name);
	return _commit(OK);
}

Error GraphScript::remove_signal(const String &p_name) {
	const int index = _find_signal_index(p_name);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	signals.remove_at(index);
	return _commit(OK);
}

void GraphScript::set_functions(const Array &p_functions) {
	Vector<GraphFunction> loaded;
	for (const Variant &value : p_functions) {
		GraphFunction function;
		ERR_FAIL_COND_MSG(value.get_type() != Variant::DICTIONARY || GraphFunction::from_dictionary(value, function) != OK, "Graph script functions are malformed.");
		ERR_FAIL_COND_MSG(!is_valid_name(function.get_name()), "Graph script function name is not an identifier.");
		for (const GraphFunction &other : loaded) {
			ERR_FAIL_COND_MSG(other.get_name() == function.get_name(), "Graph script has two functions with one name.");
		}
		loaded.push_back(function);
	}
	functions = loaded;
	_commit(OK);
}

Array GraphScript::get_functions() const {
	Array result;
	for (const GraphFunction &function : functions) {
		result.push_back(function.to_dictionary());
	}
	return result;
}

void GraphScript::set_variables(const Array &p_variables) {
	Vector<GraphVariable> loaded;
	for (const Variant &value : p_variables) {
		GraphVariable variable;
		ERR_FAIL_COND_MSG(parse_variable(value, variable) != OK, "Graph script variables are malformed.");
		for (const GraphVariable &other : loaded) {
			ERR_FAIL_COND_MSG(other.name == variable.name, "Graph script has two variables with one name.");
		}
		loaded.push_back(variable);
	}
	variables = loaded;
	_commit(OK);
}

Array GraphScript::get_variables() const {
	Array result;
	for (const GraphVariable &variable : variables) {
		result.push_back(variable_to_dictionary(variable));
	}
	return result;
}

void GraphScript::set_signals(const Array &p_signals) {
	Vector<StringName> loaded;
	for (const Variant &value : p_signals) {
		ERR_FAIL_COND_MSG(value.get_type() != Variant::STRING || !is_valid_name(value) || loaded.has(StringName(String(value))), "Graph script signals are malformed.");
		loaded.push_back(StringName(String(value)));
	}
	signals = loaded;
	_commit(OK);
}

Array GraphScript::get_signals() const {
	Array result;
	for (const StringName &signal_name : signals) {
		result.push_back(String(signal_name));
	}
	return result;
}

void GraphScript::reload_from_file() {
	// The language reloads a script by calling this, so it must not call back into the language.
	Resource::reload_from_file();
}

bool GraphScript::can_instantiate() const {
	// A function that does not compile reports at its call, so the others still run.
	return true;
}

Ref<Script> GraphScript::get_base_script() const {
	return Ref<Script>();
}

StringName GraphScript::get_global_name() const {
	return StringName();
}

bool GraphScript::inherits_script(const Ref<Script> &p_script) const {
	return p_script.ptr() == this;
}

StringName GraphScript::get_instance_base_type() const {
	return StringName();
}

ScriptInstance *GraphScript::instance_create(Object *p_this) {
	return memnew(GraphScriptInstance(Ref<GraphScript>(this), p_this));
}

bool GraphScript::has_source_code() const {
	return false;
}

String GraphScript::get_source_code() const {
	return String();
}

void GraphScript::set_source_code(const String &p_code) {}

Error GraphScript::reload(bool p_keep_state) {
	_compile();
	return error.is_empty() ? OK : ERR_COMPILATION_FAILED;
}

#ifdef TOOLS_ENABLED
StringName GraphScript::get_doc_class_name() const {
	return StringName();
}

Vector<DocData::ClassDoc> GraphScript::get_documentation() const {
	return Vector<DocData::ClassDoc>();
}

String GraphScript::get_class_icon_path() const {
	return String();
}
#endif // TOOLS_ENABLED

bool GraphScript::has_method(const StringName &p_method) const {
	return _find_function_index(p_method) >= 0;
}

MethodInfo GraphScript::get_method_info(const StringName &p_method) const {
	return has_method(p_method) ? MethodInfo(p_method) : MethodInfo();
}

bool GraphScript::is_tool() const {
	return false;
}

bool GraphScript::is_script_valid() const {
	return error.is_empty();
}

bool GraphScript::is_abstract() const {
	return false;
}

ScriptLanguage *GraphScript::get_language() const {
	return GraphScriptLanguage::get_singleton();
}

bool GraphScript::has_script_signal(const StringName &p_signal) const {
	return _find_signal_index(p_signal) >= 0;
}

void GraphScript::get_script_signal_list(List<MethodInfo> *r_signals) const {
	for (const StringName &signal_name : signals) {
		r_signals->push_back(MethodInfo(signal_name));
	}
}

bool GraphScript::get_property_default_value(const StringName &p_property, Variant &r_value) const {
	const GraphVariable *variable = get_variable(p_property);
	if (!variable) {
		return false;
	}
	r_value = variable->default_value;
	return true;
}

void GraphScript::get_script_method_list(List<MethodInfo> *r_list) const {
	for (const GraphFunction &function : functions) {
		r_list->push_back(MethodInfo(function.get_name()));
	}
}

void GraphScript::get_script_property_list(List<PropertyInfo> *r_list) const {
	for (const GraphVariable &variable : variables) {
		uint32_t usage = PROPERTY_USAGE_SCRIPT_VARIABLE;
		if (variable.exported) {
			usage |= PROPERTY_USAGE_DEFAULT;
		}
		if (variable.type == Variant::NIL) {
			usage |= PROPERTY_USAGE_NIL_IS_VARIANT;
		}
		r_list->push_back(PropertyInfo(variable.type, variable.name, PROPERTY_HINT_NONE, String(), usage));
	}
}

const Variant GraphScript::get_rpc_config() const {
	return Variant();
}

void GraphScript::register_members() {
	ClassRegistry::bind_method(D_METHOD("add_function", "name"), &GraphScript::add_function);
	ClassRegistry::bind_method(D_METHOD("remove_function", "name"), &GraphScript::remove_function);
	ClassRegistry::bind_method(D_METHOD("get_valid_step_id", "function"), &GraphScript::get_valid_step_id);
	ClassRegistry::bind_method(D_METHOD("get_error"), &GraphScript::get_error);
	ClassRegistry::bind_method(D_METHOD("add_step", "function", "id", "kind"), &GraphScript::add_step);
	ClassRegistry::bind_method(D_METHOD("remove_step", "function", "id"), &GraphScript::remove_step);
	ClassRegistry::bind_method(D_METHOD("move_step", "function", "id", "x", "y"), &GraphScript::move_step);
	ClassRegistry::bind_method(D_METHOD("set_step_parameter", "function", "id", "parameter", "value_text"), &GraphScript::set_step_parameter);
	ClassRegistry::bind_method(D_METHOD("set_step_pins", "function", "id", "count"), &GraphScript::set_step_pins);
	ClassRegistry::bind_method(D_METHOD("connect_steps", "function", "from_step", "from_port", "to_step", "to_port"), &GraphScript::connect_steps);
	ClassRegistry::bind_method(D_METHOD("disconnect_steps", "function", "from_step", "from_port", "to_step", "to_port"), &GraphScript::disconnect_steps);
	ClassRegistry::bind_method(D_METHOD("add_variable", "name", "type"), &GraphScript::add_variable);
	ClassRegistry::bind_method(D_METHOD("remove_variable", "name"), &GraphScript::remove_variable);
	ClassRegistry::bind_method(D_METHOD("set_variable_default", "name", "value_text"), &GraphScript::set_variable_default);
	ClassRegistry::bind_method(D_METHOD("set_variable_exported", "name", "exported"), &GraphScript::set_variable_exported);
	ClassRegistry::bind_method(D_METHOD("add_signal", "name"), &GraphScript::add_signal);
	ClassRegistry::bind_method(D_METHOD("remove_signal", "name"), &GraphScript::remove_signal);

	ClassRegistry::bind_method(D_METHOD("set_functions", "functions"), &GraphScript::set_functions);
	ClassRegistry::bind_method(D_METHOD("get_functions"), &GraphScript::get_functions);
	ClassRegistry::bind_method(D_METHOD("set_variables", "variables"), &GraphScript::set_variables);
	ClassRegistry::bind_method(D_METHOD("get_variables"), &GraphScript::get_variables);
	ClassRegistry::bind_method(D_METHOD("set_signals", "signals"), &GraphScript::set_signals);
	ClassRegistry::bind_method(D_METHOD("get_signals"), &GraphScript::get_signals);

	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "functions", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR), "set_functions", "get_functions");
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "variables", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR), "set_variables", "get_variables");
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "signals", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR), "set_signals", "get_signals");
}
