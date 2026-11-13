/**************************************************************************/
/*  graph_script.cpp                                                      */

#include "graph_script.h"

#include "graph_compiler.h"
#include "graph_document.h"
#include "graph_script_instance.h"
#include "graph_script_language.h"
#include "graph_step_catalogue.h"
#include "graph_warnings.h"

#include "core/io/json.h"
#include "core/object/class_registry.h"
#include "core/variant/variant_parser.h"

namespace {

bool is_valid_name(const String &p_name) {
	return p_name.is_valid_unicode_identifier();
}

bool is_valid_type(int p_type) {
	return p_type >= Variant::NIL && p_type < Variant::VARIANT_MAX;
}

bool is_valid_hint(int p_hint) {
	return p_hint >= PROPERTY_HINT_NONE && p_hint < PROPERTY_HINT_MAX;
}

bool parse_side(const String &p_text, GraphPinSide &r_side) {
	static const struct {
		const char *text;
		GraphPinSide side;
	} SIDES[] = { { "", GraphPinSide::NONE }, { "input", GraphPinSide::INPUT }, { "output", GraphPinSide::OUTPUT } };
	for (const auto &entry : SIDES) {
		if (p_text == entry.text) {
			r_side = entry.side;
			return true;
		}
	}
	return false;
}

bool parse_sort(const String &p_text, GraphPortSort &r_sort) {
	if (p_text != "flow" && p_text != "data") {
		return false;
	}
	r_sort = p_text == "flow" ? GraphPortSort::FLOW : GraphPortSort::DATA;
	return true;
}

// The text fields of a variable that a file stores only when they are not empty.
struct TextKey {
	const char *key;
	String GraphVariable::*field;
};

constexpr TextKey TEXT_KEYS[] = {
	{ "hint_string", &GraphVariable::hint_string },
	{ "category", &GraphVariable::category },
	{ "description", &GraphVariable::description },
};

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

// The input of the Call, Connect and Await steps that takes the object they act on.
constexpr int TARGET_PORT = 1;

// The steps that name a declared function, variable or signal, and so change with a rename.
constexpr GraphReference FUNCTION_REFERENCES[] = {
	{ "call_function", "function", TARGET_PORT },
};
constexpr GraphReference VARIABLE_REFERENCES[] = {
	{ "get", "name", -1 },
	{ "set", "name", -1 },
};
constexpr GraphReference SIGNAL_REFERENCES[] = {
	{ "emit", "signal", -1 },
	{ "signal_received", "signal", -1 },
	{ "connect", "signal", TARGET_PORT },
	{ "await_signal", "signal", TARGET_PORT },
};

template <size_t COUNT>
void rename_references(Vector<GraphFunction> &r_functions, const GraphReference (&p_references)[COUNT], const StringName &p_old, const StringName &p_new) {
	for (int i = 0; i < r_functions.size(); i++) {
		for (const GraphReference &reference : p_references) {
			r_functions.write[i].rename_references(reference, p_old, p_new);
		}
	}
}

// A file from before exports had hints has none of these keys, and each then keeps its default.
Error parse_variable_info(const Dictionary &p_entry, GraphVariable &r_variable) {
	const Variant hint = p_entry.get("hint", int(PROPERTY_HINT_NONE));
	if (hint.get_type() != Variant::INT || !is_valid_hint(hint)) {
		return ERR_INVALID_DATA;
	}
	r_variable.hint = PropertyHint(int(hint));
	for (const TextKey &text : TEXT_KEYS) {
		const Variant value = p_entry.get(text.key, String());
		if (value.get_type() != Variant::STRING) {
			return ERR_INVALID_DATA;
		}
		r_variable.*text.field = value;
	}
	return OK;
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
	return parse_variable_info(entry, r_variable);
}

Dictionary variable_to_dictionary(const GraphVariable &p_variable) {
	Dictionary entry;
	entry["name"] = String(p_variable.name);
	entry["type"] = int(p_variable.type);
	entry["default"] = p_variable.default_value;
	entry["exported"] = p_variable.exported;
	if (p_variable.hint != PROPERTY_HINT_NONE) {
		entry["hint"] = int(p_variable.hint);
	}
	for (const TextKey &text : TEXT_KEYS) {
		if (!(p_variable.*text.field).is_empty()) {
			entry[text.key] = p_variable.*text.field;
		}
	}
	return entry;
}

// A signal with no arguments is stored as its name, and one with arguments as a dictionary.
Error parse_signal(const Variant &p_value, GraphSignal &r_signal) {
	Variant name = p_value;
	if (p_value.get_type() == Variant::DICTIONARY) {
		const Dictionary entry = p_value;
		name = entry.get("name", Variant());
		if (!GraphSignatures::parse_typed_list(entry.get("arguments", Variant()), r_signal.arguments).is_empty()) {
			return ERR_INVALID_DATA;
		}
	}
	if (name.get_type() != Variant::STRING || !is_valid_name(name)) {
		return ERR_INVALID_DATA;
	}
	r_signal.name = String(name);
	return OK;
}

// The loaders return the reason a list is refused, or an empty text.
String load_functions(const Array &p_functions, Vector<GraphFunction> &r_loaded) {
	for (const Variant &value : p_functions) {
		GraphFunction function;
		if (value.get_type() != Variant::DICTIONARY || GraphFunction::from_dictionary(value, function) != OK) {
			return "Graph script functions are malformed.";
		}
		if (!is_valid_name(function.get_name())) {
			return "Graph script function name is not an identifier.";
		}
		for (const GraphFunction &other : r_loaded) {
			if (other.get_name() == function.get_name()) {
				return "Graph script has two functions with one name.";
			}
		}
		r_loaded.push_back(function);
	}
	return String();
}

String load_variables(const Array &p_variables, Vector<GraphVariable> &r_loaded) {
	for (const Variant &value : p_variables) {
		GraphVariable variable;
		if (parse_variable(value, variable) != OK) {
			return "Graph script variables are malformed.";
		}
		for (const GraphVariable &other : r_loaded) {
			if (other.name == variable.name) {
				return "Graph script has two variables with one name.";
			}
		}
		r_loaded.push_back(variable);
	}
	return String();
}

String load_signals(const Array &p_signals, Vector<GraphSignal> &r_loaded) {
	for (const Variant &value : p_signals) {
		GraphSignal loaded_signal;
		if (parse_signal(value, loaded_signal) != OK) {
			return "Graph script signals are malformed.";
		}
		for (const GraphSignal &other : r_loaded) {
			if (other.name == loaded_signal.name) {
				return "Graph script signals are malformed.";
			}
		}
		r_loaded.push_back(loaded_signal);
	}
	return String();
}

// A signal with no arguments is stored as its name, and one with arguments as a dictionary.
Variant signal_to_variant(const GraphSignal &p_signal) {
	if (p_signal.arguments.is_empty()) {
		return String(p_signal.name);
	}
	Dictionary entry;
	entry["name"] = String(p_signal.name);
	entry["arguments"] = GraphSignatures::typed_list_to_array(p_signal.arguments);
	return entry;
}

String write_variant(const Variant &p_value) {
	String text;
	VariantWriter::write_to_string(p_value, text);
	return text;
}

bool parse_dictionary(const String &p_text, Dictionary &r_dictionary) {
	Variant value;
	if (!parse_variant(p_text, value) || value.get_type() != Variant::DICTIONARY) {
		return false;
	}
	r_dictionary = value;
	return true;
}

bool read_list(const Dictionary &p_dictionary, const char *p_key, Array &r_list) {
	const Variant value = p_dictionary.get(p_key, Variant());
	if (value.get_type() != Variant::ARRAY) {
		return false;
	}
	r_list = value;
	return true;
}

// Everything a script holds apart from its compiled form.
struct Snapshot {
	StringName base_type;
	Vector<GraphFunction> functions;
	Vector<GraphVariable> variables;
	Vector<GraphSignal> signals;
};

bool read_snapshot(const String &p_text, Snapshot &r_snapshot) {
	Dictionary entry;
	Array function_list;
	Array variable_list;
	Array signal_list;
	if (!parse_dictionary(p_text, entry) || !read_list(entry, "functions", function_list) || !read_list(entry, "variables", variable_list) || !read_list(entry, "signals", signal_list)) {
		return false;
	}
	const Variant base = entry.get("base_type", Variant());
	if (base.get_type() != Variant::STRING || (!String(base).is_empty() && !ClassRegistry::class_exists(StringName(String(base))))) {
		return false;
	}
	r_snapshot.base_type = StringName(String(base));
	return load_functions(function_list, r_snapshot.functions).is_empty() && load_variables(variable_list, r_snapshot.variables).is_empty() && load_signals(signal_list, r_snapshot.signals).is_empty();
}

// The steps of a function that were copied and what they name, as one text.
struct Payload {
	GraphFunction steps;
	Vector<GraphVariable> variables;
	Vector<GraphSignal> signals;
	Vector<String> functions;
};

bool read_payload(const String &p_text, Payload &r_payload) {
	Dictionary entry;
	Array variable_list;
	Array signal_list;
	Array function_list;
	if (!parse_dictionary(p_text, entry) || !read_list(entry, "variables", variable_list) || !read_list(entry, "signals", signal_list) || !read_list(entry, "functions", function_list)) {
		return false;
	}
	const Variant steps = entry.get("steps", Variant());
	if (steps.get_type() != Variant::DICTIONARY || GraphFunction::from_dictionary(steps, r_payload.steps) != OK) {
		return false;
	}
	for (const Variant &name : function_list) {
		if (name.get_type() != Variant::STRING || !is_valid_name(name)) {
			return false;
		}
		r_payload.functions.push_back(name);
	}
	return load_variables(variable_list, r_payload.variables).is_empty() && load_signals(signal_list, r_payload.signals).is_empty();
}

template <typename T>
int find_by_name(const Vector<T> &p_list, const StringName &p_name) {
	for (int i = 0; i < p_list.size(); i++) {
		if (p_list[i].name == p_name) {
			return i;
		}
	}
	return -1;
}

// The first name of the form name_2, name_3 and so on that the list does not have.
template <typename T>
StringName unique_name(const Vector<T> &p_list, const StringName &p_name) {
	for (int number = 2;; number++) {
		const StringName candidate = StringName(String(p_name) + "_" + itos(number));
		if (find_by_name(p_list, candidate) < 0) {
			return candidate;
		}
	}
}

bool has_function(const Vector<GraphFunction> &p_functions, const String &p_name) {
	for (const GraphFunction &function : p_functions) {
		if (function.get_name() == p_name) {
			return true;
		}
	}
	return false;
}

bool same_declaration(const GraphVariable &p_a, const GraphVariable &p_b) {
	return p_a.type == p_b.type;
}

bool same_declaration(const GraphSignal &p_a, const GraphSignal &p_b) {
	if (p_a.arguments.size() != p_b.arguments.size()) {
		return false;
	}
	for (int i = 0; i < p_a.arguments.size(); i++) {
		if (p_a.arguments[i].name != p_b.arguments[i].name || p_a.arguments[i].type != p_b.arguments[i].type) {
			return false;
		}
	}
	return true;
}

template <typename T>
void add_conflicts(const Vector<T> &p_declared, const Vector<T> &p_incoming, const String &p_kind, Array &r_conflicts) {
	for (const T &item : p_incoming) {
		const int at = find_by_name(p_declared, item.name);
		if (at >= 0 && !same_declaration(p_declared[at], item)) {
			Dictionary conflict;
			conflict["kind"] = p_kind;
			conflict["name"] = String(item.name);
			r_conflicts.push_back(conflict);
		}
	}
}

// Declares what a payload names that the list lacks. One of another declaration is left alone, or
// declared again under a new name that the payload's steps are pointed at.
template <typename T, typename Rename>
void declare_incoming(Vector<T> &r_declared, Vector<T> &r_incoming, bool p_keep_both, Rename p_rename) {
	for (int i = 0; i < r_incoming.size(); i++) {
		T &item = r_incoming.write[i];
		const int at = find_by_name(r_declared, item.name);
		if (at < 0) {
			r_declared.push_back(item);
		} else if (p_keep_both && !same_declaration(r_declared[at], item)) {
			const StringName name = unique_name(r_declared, item.name);
			p_rename(item.name, name);
			item.name = name;
			r_declared.push_back(item);
		}
	}
}

template <size_t COUNT>
Vector<StringName> referenced(const GraphFunction &p_function, const GraphReference (&p_references)[COUNT]) {
	Vector<StringName> names;
	for (const GraphReference &reference : p_references) {
		for (const StringName &name : p_function.get_references(reference)) {
			if (!names.has(name)) {
				names.push_back(name);
			}
		}
	}
	return names;
}

String paste_result(Error p_error, const Array &p_conflicts = Array(), const Array &p_steps = Array()) {
	Dictionary result;
	result["error"] = int(p_error);
	result["conflicts"] = p_conflicts;
	result["steps"] = p_steps;
	return JSON::stringify(result);
}

// The arguments and the return type come from the start step, so a function that did not
// compile far enough to find one lists none.
MethodInfo method_info_of(const GraphCompiledFunction &p_function) {
	MethodInfo info(p_function.name);
	for (const GraphTyped &argument : p_function.signature.arguments) {
		info.arguments.push_back(PropertyInfo(argument.type, argument.name));
	}
	info.return_val = PropertyInfo(p_function.signature.returns, "");
	if (p_function.signature.returns == Variant::NIL) {
		info.return_val.usage |= PROPERTY_USAGE_NIL_IS_VARIANT;
	}
	return info;
}

#ifdef TOOLS_ENABLED
DocData::PropertyDoc property_doc(const GraphVariable &p_variable) {
	DocData::PropertyDoc doc;
	doc.name = p_variable.name;
	doc.type = Variant::get_type_name(p_variable.type);
	doc.description = p_variable.description;
	doc.default_value = String(p_variable.default_value);
	return doc;
}

DocData::MethodDoc method_doc(const GraphCompiledFunction &p_function) {
	DocData::MethodDoc doc;
	doc.name = p_function.name;
	doc.return_type = Variant::get_type_name(p_function.signature.returns);
	doc.description = p_function.signature.description;
	for (const GraphTyped &argument : p_function.signature.arguments) {
		DocData::ArgumentDoc argument_doc;
		argument_doc.name = argument.name;
		argument_doc.type = Variant::get_type_name(argument.type);
		doc.arguments.push_back(argument_doc);
	}
	return doc;
}
#endif // TOOLS_ENABLED

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
	for (int i = 0; i < signals.size(); i++) {
		if (signals[i].name == p_name) {
			return i;
		}
	}
	return -1;
}

void GraphScript::_compile() {
	// A waiting call points into the compiled functions, which are about to be replaced.
	for (GraphScriptInstance *instance : instances) {
		instance->drop_waiting_calls();
	}
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

// An instance keeps the value of every variable that still has its name and type.
void GraphScript::_update_instances() {
	for (GraphScriptInstance *instance : instances) {
		instance->refresh();
	}
#ifdef TOOLS_ENABLED
	for (PlaceHolderScriptInstance *placeholder : placeholders) {
		_update_placeholder(placeholder);
	}
#endif
}

Error GraphScript::_commit(Error p_error) {
	if (p_error == OK) {
		_compile();
		_update_instances();
		emit_changed();
	}
	return p_error;
}

#ifdef TOOLS_ENABLED
void GraphScript::_update_placeholder(PlaceHolderScriptInstance *p_placeholder) const {
	List<PropertyInfo> properties;
	get_script_property_list(&properties);
	HashMap<StringName, Variant> defaults;
	for (const GraphVariable &variable : variables) {
		defaults[variable.name] = variable.default_value;
	}
	p_placeholder->update(properties, defaults);
}
#endif // TOOLS_ENABLED

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

Error GraphScript::set_frame(const String &p_function, const String &p_frame_text) {
	Variant value;
	if (!parse_variant(p_frame_text, value)) {
		return ERR_PARSE_ERROR;
	}
	GraphCommentFrame frame;
	const Error err = GraphCommentFrame::from_variant(value, frame);
	if (err != OK) {
		return err;
	}
	return _edit_function(p_function, [&](GraphFunction &p_target) { return p_target.set_frame(frame); });
}

Error GraphScript::remove_frame(const String &p_function, int p_id) {
	return _edit_function(p_function, [&](GraphFunction &p_target) { return p_target.remove_frame(p_id); });
}

Error GraphScript::rename_function(const String &p_name, const String &p_new_name) {
	const int index = _find_function_index(p_name);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	if (!is_valid_name(p_new_name)) {
		return ERR_INVALID_PARAMETER;
	}
	if (_find_function_index(p_new_name) >= 0) {
		return ERR_ALREADY_EXISTS;
	}
	rename_references(functions, FUNCTION_REFERENCES, p_name, p_new_name);
	functions.write[index].set_name(p_new_name);
	return _commit(OK);
}

Error GraphScript::rename_variable(const String &p_name, const String &p_new_name) {
	const int index = _find_variable_index(p_name);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	if (!is_valid_name(p_new_name)) {
		return ERR_INVALID_PARAMETER;
	}
	if (_find_variable_index(p_new_name) >= 0) {
		return ERR_ALREADY_EXISTS;
	}
	rename_references(functions, VARIABLE_REFERENCES, p_name, p_new_name);
	variables.write[index].name = p_new_name;
	return _commit(OK);
}

Error GraphScript::rename_signal(const String &p_name, const String &p_new_name) {
	const int index = _find_signal_index(p_name);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	if (!is_valid_name(p_new_name)) {
		return ERR_INVALID_PARAMETER;
	}
	if (_find_signal_index(p_new_name) >= 0) {
		return ERR_ALREADY_EXISTS;
	}
	rename_references(functions, SIGNAL_REFERENCES, p_name, p_new_name);
	signals.write[index].name = p_new_name;
	return _commit(OK);
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

Error GraphScript::set_variable_type(const String &p_name, int p_type) {
	const int index = _find_variable_index(p_name);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	if (!is_valid_type(p_type)) {
		return ERR_INVALID_PARAMETER;
	}
	GraphVariable &variable = variables.write[index];
	if (variable.type != Variant::Type(p_type)) {
		variable.type = Variant::Type(p_type);
		variable.default_value = default_for_type(variable.type);
	}
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

Error GraphScript::set_variable_hint(const String &p_name, int p_hint, const String &p_hint_string) {
	const int index = _find_variable_index(p_name);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	if (!is_valid_hint(p_hint)) {
		return ERR_INVALID_PARAMETER;
	}
	variables.write[index].hint = PropertyHint(p_hint);
	variables.write[index].hint_string = p_hint_string;
	return _commit(OK);
}

Error GraphScript::set_variable_info(const String &p_name, const String &p_category, const String &p_description) {
	const int index = _find_variable_index(p_name);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	variables.write[index].category = p_category;
	variables.write[index].description = p_description;
	return _commit(OK);
}

Error GraphScript::add_signal(const String &p_name) {
	if (!is_valid_name(p_name)) {
		return ERR_INVALID_PARAMETER;
	}
	if (_find_signal_index(p_name) >= 0) {
		return ERR_ALREADY_EXISTS;
	}
	signals.push_back({ p_name, Vector<GraphTyped>() });
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

Error GraphScript::set_signal_arguments(const String &p_name, const String &p_arguments_text) {
	const int index = _find_signal_index(p_name);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	Variant list;
	if (!parse_variant(p_arguments_text, list)) {
		return ERR_PARSE_ERROR;
	}
	Vector<GraphTyped> arguments;
	if (!GraphSignatures::parse_typed_list(list, arguments).is_empty()) {
		return ERR_INVALID_DATA;
	}
	signals.write[index].arguments = arguments;
	return _commit(OK);
}

void GraphScript::set_functions(const Array &p_functions) {
	Vector<GraphFunction> loaded;
	const String problem = load_functions(p_functions, loaded);
	ERR_FAIL_COND_MSG(!problem.is_empty(), problem);
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
	const String problem = load_variables(p_variables, loaded);
	ERR_FAIL_COND_MSG(!problem.is_empty(), problem);
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
	Vector<GraphSignal> loaded;
	const String problem = load_signals(p_signals, loaded);
	ERR_FAIL_COND_MSG(!problem.is_empty(), problem);
	signals = loaded;
	_commit(OK);
}

Array GraphScript::get_signals() const {
	Array result;
	for (const GraphSignal &declared : signals) {
		result.push_back(signal_to_variant(declared));
	}
	return result;
}

void GraphScript::set_base_type(const StringName &p_base_type) {
	ERR_FAIL_COND_MSG(p_base_type != StringName() && !ClassRegistry::class_exists(p_base_type), vformat("Graph script base type '%s' is not a class.", p_base_type));
	base_type = p_base_type;
}

Array GraphScript::get_warnings() const {
	Array result;
	for (const GraphWarning &warning : GraphWarnings::find(*this)) {
		Dictionary entry;
		entry["kind"] = warning.kind;
		entry["function"] = warning.function;
		entry["step"] = warning.step;
		entry["name"] = warning.name;
		entry["text"] = warning.text;
		result.push_back(entry);
	}
	return result;
}

String GraphScript::get_snapshot() const {
	Dictionary snapshot;
	snapshot["base_type"] = String(base_type);
	snapshot["functions"] = get_functions();
	snapshot["variables"] = get_variables();
	snapshot["signals"] = get_signals();
	return write_variant(snapshot);
}

Error GraphScript::restore_snapshot(const String &p_snapshot_text) {
	Snapshot snapshot;
	if (!read_snapshot(p_snapshot_text, snapshot)) {
		return ERR_INVALID_DATA;
	}
	functions = snapshot.functions;
	variables = snapshot.variables;
	signals = snapshot.signals;
	base_type = snapshot.base_type;
	return _commit(OK);
}

String GraphScript::get_payload(const String &p_function, const String &p_ids_text) const {
	const GraphFunction *function = get_function(p_function);
	Variant ids;
	if (!function || !parse_variant(p_ids_text, ids) || ids.get_type() != Variant::ARRAY) {
		return String();
	}
	Vector<int> wanted;
	const Array id_list = ids;
	for (const Variant &id : id_list) {
		if (id.get_type() == Variant::INT) {
			wanted.push_back(id);
		}
	}
	const GraphFunction copy = function->extract(wanted);
	if (copy.get_step_count() == 0) {
		return String();
	}
	Array used_variables;
	for (const StringName &name : referenced(copy, VARIABLE_REFERENCES)) {
		if (const GraphVariable *variable = get_variable(name)) {
			used_variables.push_back(variable_to_dictionary(*variable));
		}
	}
	Array used_signals;
	for (const StringName &name : referenced(copy, SIGNAL_REFERENCES)) {
		const int at = _find_signal_index(name);
		if (at >= 0) {
			used_signals.push_back(signal_to_variant(signals[at]));
		}
	}
	Array used_functions;
	for (const StringName &name : referenced(copy, FUNCTION_REFERENCES)) {
		used_functions.push_back(String(name));
	}
	Dictionary payload;
	payload["steps"] = copy.to_dictionary();
	payload["variables"] = used_variables;
	payload["signals"] = used_signals;
	payload["functions"] = used_functions;
	return write_variant(payload);
}

String GraphScript::paste_payload(const String &p_function, const String &p_payload_text, float p_x, float p_y, const String &p_conflict) {
	Payload payload;
	if (!read_payload(p_payload_text, payload)) {
		return paste_result(ERR_INVALID_DATA);
	}
	const int index = _find_function_index(p_function);
	if (index < 0) {
		return paste_result(ERR_DOES_NOT_EXIST);
	}
	// A return reports a value to the caller of the function, and a function with no entry has none.
	if (payload.steps.has_step_of_kind("return") && !functions[index].has_step_of_kind("entry")) {
		return paste_result(ERR_INVALID_PARAMETER);
	}
	Array conflicts;
	add_conflicts(variables, payload.variables, "variable", conflicts);
	add_conflicts(signals, payload.signals, "signal", conflicts);
	if (p_conflict != "reuse" && p_conflict != "keep_both" && !p_conflict.is_empty()) {
		return paste_result(ERR_INVALID_PARAMETER);
	}
	if (!conflicts.is_empty() && p_conflict.is_empty()) {
		return paste_result(ERR_ALREADY_EXISTS, conflicts);
	}

	// Everything is built on copies and kept only when it is all done, so a paste is one change.
	Vector<GraphVariable> next_variables = variables;
	Vector<GraphSignal> next_signals = signals;
	Vector<GraphFunction> next_functions = functions;
	const bool keep_both = p_conflict == "keep_both";
	declare_incoming(next_variables, payload.variables, keep_both, [&](const StringName &p_old, const StringName &p_new) {
		for (const GraphReference &reference : VARIABLE_REFERENCES) {
			payload.steps.rename_references(reference, p_old, p_new);
		}
	});
	declare_incoming(next_signals, payload.signals, keep_both, [&](const StringName &p_old, const StringName &p_new) {
		for (const GraphReference &reference : SIGNAL_REFERENCES) {
			payload.steps.rename_references(reference, p_old, p_new);
		}
	});
	for (const String &name : payload.functions) {
		if (!has_function(next_functions, name)) {
			next_functions.push_back(GraphFunction(name));
		}
	}
	Vector<int> new_ids;
	next_functions.write[index].paste(payload.steps, Vector2(p_x, p_y), new_ids);
	variables = next_variables;
	signals = next_signals;
	functions = next_functions;
	Array pasted;
	for (const int id : new_ids) {
		pasted.push_back(id);
	}
	_commit(OK);
	return paste_result(OK, Array(), pasted);
}

String GraphScript::get_document() const {
	return GraphDocument::write(*this);
}

String GraphScript::find_steps(const String &p_text, const String &p_dragged_side, int p_type, const String &p_sort) const {
	GraphWireFilter filter;
	ERR_FAIL_COND_V_MSG(!parse_side(p_dragged_side, filter.dragged_from), String(), vformat("A dragged side is \"\", \"input\" or \"output\", not \"%s\".", p_dragged_side));
	ERR_FAIL_COND_V_MSG(!parse_sort(p_sort, filter.sort), String(), vformat("A port sort is \"flow\" or \"data\", not \"%s\".", p_sort));
	ERR_FAIL_COND_V_MSG(!is_valid_type(p_type), String(), vformat("%d is not a type.", p_type));
	filter.type = Variant::Type(p_type);
	return GraphDocument::write_entries(GraphStepCatalogue::find(p_text, filter));
}

void GraphScript::reload_from_file() {
	// The language reloads a script by calling this, so it must not call back into the language.
	Resource::reload_from_file();
}

bool GraphScript::can_instantiate() const {
	// The editor turns scripting off, and then a script that is not a tool is only a placeholder.
	// A function that does not compile reports at its call, so the others still run.
	return ScriptSystem::is_scripting_enabled() || is_tool();
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
	return base_type;
}

ScriptInstance *GraphScript::instance_create(Object *p_this) {
	ERR_FAIL_COND_V_MSG(base_type != StringName() && !p_this->is_class(String(base_type)), nullptr, vformat("A graph script for '%s' cannot go on an object of the class '%s'.", base_type, p_this->get_class()));
	return memnew(GraphScriptInstance(Ref<GraphScript>(this), p_this));
}

#ifdef TOOLS_ENABLED
PlaceHolderScriptInstance *GraphScript::placeholder_instance_create(Object *p_this) {
	PlaceHolderScriptInstance *placeholder = memnew(PlaceHolderScriptInstance(GraphScriptLanguage::get_singleton(), Ref<Script>(this), p_this));
	placeholders.insert(placeholder);
	_update_placeholder(placeholder);
	return placeholder;
}

void GraphScript::update_exports() {
	for (PlaceHolderScriptInstance *placeholder : placeholders) {
		_update_placeholder(placeholder);
	}
}

void GraphScript::_placeholder_erased(PlaceHolderScriptInstance *p_placeholder) {
	placeholders.erase(p_placeholder);
}
#endif // TOOLS_ENABLED

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
// A script with no file has no name to be documented under.
StringName GraphScript::get_doc_class_name() const {
	return get_path().is_empty() || is_built_in() ? StringName() : StringName(get_path().get_file().get_basename());
}

Vector<DocData::ClassDoc> GraphScript::get_documentation() const {
	Vector<DocData::ClassDoc> docs;
	const StringName name = get_doc_class_name();
	if (name == StringName()) {
		return docs;
	}
	DocData::ClassDoc doc;
	doc.name = name;
	doc.inherits = base_type == StringName() ? String("Object") : String(base_type);
	doc.is_script_doc = true;
	doc.script_path = get_path();
	for (const GraphVariable &variable : variables) {
		if (variable.exported) {
			doc.properties.push_back(property_doc(variable));
		}
	}
	for (const GraphCompiledFunction &function : compiled) {
		doc.methods.push_back(method_doc(function));
	}
	docs.push_back(doc);
	return docs;
}

String GraphScript::get_class_icon_path() const {
	return String();
}
#endif // TOOLS_ENABLED

bool GraphScript::has_method(const StringName &p_method) const {
	return _find_function_index(p_method) >= 0;
}

MethodInfo GraphScript::get_method_info(const StringName &p_method) const {
	const GraphCompiledFunction *function = get_compiled(p_method);
	return function ? method_info_of(*function) : MethodInfo();
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
	for (const GraphSignal &declared : signals) {
		MethodInfo info(declared.name);
		for (const GraphTyped &argument : declared.arguments) {
			info.arguments.push_back(PropertyInfo(argument.type, argument.name));
		}
		r_signals->push_back(info);
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
	for (const GraphCompiledFunction &function : compiled) {
		r_list->push_back(method_info_of(function));
	}
}

// A group entry comes before a run of exported variables with one category. An empty one ends the run.
void GraphScript::get_script_property_list(List<PropertyInfo> *r_list) const {
	String group;
	for (const GraphVariable &variable : variables) {
		uint32_t usage = PROPERTY_USAGE_SCRIPT_VARIABLE;
		if (variable.exported) {
			usage |= PROPERTY_USAGE_DEFAULT;
			if (variable.category != group) {
				group = variable.category;
				r_list->push_back(PropertyInfo(Variant::NIL, group, PROPERTY_HINT_NONE, String(), PROPERTY_USAGE_GROUP));
			}
		}
		if (variable.type == Variant::NIL) {
			usage |= PROPERTY_USAGE_NIL_IS_VARIANT;
		}
		r_list->push_back(PropertyInfo(variable.type, variable.name, variable.hint, variable.hint_string, usage));
	}
}

const Variant GraphScript::get_rpc_config() const {
	Dictionary config;
	for (const GraphCompiledFunction &function : compiled) {
		if (!function.signature.rpc.is_empty()) {
			config[function.name] = function.signature.rpc;
		}
	}
	return config.is_empty() ? Variant() : Variant(config);
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
	ClassRegistry::bind_method(D_METHOD("set_frame", "function", "frame_text"), &GraphScript::set_frame);
	ClassRegistry::bind_method(D_METHOD("remove_frame", "function", "id"), &GraphScript::remove_frame);
	ClassRegistry::bind_method(D_METHOD("rename_function", "name", "new_name"), &GraphScript::rename_function);
	ClassRegistry::bind_method(D_METHOD("rename_variable", "name", "new_name"), &GraphScript::rename_variable);
	ClassRegistry::bind_method(D_METHOD("rename_signal", "name", "new_name"), &GraphScript::rename_signal);
	ClassRegistry::bind_method(D_METHOD("add_variable", "name", "type"), &GraphScript::add_variable);
	ClassRegistry::bind_method(D_METHOD("remove_variable", "name"), &GraphScript::remove_variable);
	ClassRegistry::bind_method(D_METHOD("set_variable_type", "name", "type"), &GraphScript::set_variable_type);
	ClassRegistry::bind_method(D_METHOD("set_variable_default", "name", "value_text"), &GraphScript::set_variable_default);
	ClassRegistry::bind_method(D_METHOD("set_variable_exported", "name", "exported"), &GraphScript::set_variable_exported);
	ClassRegistry::bind_method(D_METHOD("set_variable_hint", "name", "hint", "hint_string"), &GraphScript::set_variable_hint);
	ClassRegistry::bind_method(D_METHOD("set_variable_info", "name", "category", "description"), &GraphScript::set_variable_info);
	ClassRegistry::bind_method(D_METHOD("add_signal", "name"), &GraphScript::add_signal);
	ClassRegistry::bind_method(D_METHOD("remove_signal", "name"), &GraphScript::remove_signal);
	ClassRegistry::bind_method(D_METHOD("set_signal_arguments", "name", "arguments_text"), &GraphScript::set_signal_arguments);

	ClassRegistry::bind_method(D_METHOD("set_functions", "functions"), &GraphScript::set_functions);
	ClassRegistry::bind_method(D_METHOD("get_functions"), &GraphScript::get_functions);
	ClassRegistry::bind_method(D_METHOD("set_variables", "variables"), &GraphScript::set_variables);
	ClassRegistry::bind_method(D_METHOD("get_variables"), &GraphScript::get_variables);
	ClassRegistry::bind_method(D_METHOD("set_signals", "signals"), &GraphScript::set_signals);
	ClassRegistry::bind_method(D_METHOD("get_signals"), &GraphScript::get_signals);
	ClassRegistry::bind_method(D_METHOD("set_base_type", "base_type"), &GraphScript::set_base_type);
	ClassRegistry::bind_method(D_METHOD("get_base_type"), &GraphScript::get_base_type);
	ClassRegistry::bind_method(D_METHOD("get_warnings"), &GraphScript::get_warnings);
	ClassRegistry::bind_method(D_METHOD("get_snapshot"), &GraphScript::get_snapshot);
	ClassRegistry::bind_method(D_METHOD("restore_snapshot", "snapshot_text"), &GraphScript::restore_snapshot);
	ClassRegistry::bind_method(D_METHOD("get_payload", "function", "ids_text"), &GraphScript::get_payload);
	ClassRegistry::bind_method(D_METHOD("paste_payload", "function", "payload_text", "x", "y", "conflict"), &GraphScript::paste_payload);
	ClassRegistry::bind_method(D_METHOD("get_document"), &GraphScript::get_document);
	ClassRegistry::bind_method(D_METHOD("find_steps", "text", "dragged_side", "type", "sort"), &GraphScript::find_steps);

	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "functions", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR), "set_functions", "get_functions");
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "variables", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR), "set_variables", "get_variables");
	ADD_PROPERTY(PropertyInfo(Variant::ARRAY, "signals", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR), "set_signals", "get_signals");
	ADD_PROPERTY(PropertyInfo(Variant::STRING_NAME, "base_type", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NO_EDITOR), "set_base_type", "get_base_type");
}
