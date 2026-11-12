/**************************************************************************/
/*  graph_document.cpp                                                    */

#include "graph_document.h"

#include "graph_script.h"
#include "graph_warnings.h"

#include "core/io/json.h"
#include "core/variant/variant_parser.h"

namespace GraphDocument {

namespace {

String text_of(const Variant &p_value) {
	String text;
	VariantWriter::write_to_string(p_value, text);
	return text;
}

Dictionary texts_of(const Dictionary &p_values) {
	Dictionary texts;
	const Array keys = p_values.keys();
	for (const Variant &key : keys) {
		texts[key] = text_of(p_values[key]);
	}
	return texts;
}

String sort_name(GraphPortSort p_sort) {
	return p_sort == GraphPortSort::FLOW ? "flow" : "data";
}

Array ports_of(const GraphStepKind &p_kind, const Dictionary &p_parameters, GraphPinSide p_side) {
	Array ports;
	const int count = GraphStepKinds::get_port_count(p_kind, p_parameters, p_side);
	for (int i = 0; i < count; i++) {
		const GraphPort port = GraphStepKinds::get_port(p_kind, p_parameters, p_side, i);
		Dictionary entry;
		entry["name"] = port.name;
		entry["sort"] = sort_name(port.sort);
		entry["type"] = int(port.type);
		entry["parameter"] = port.parameter;
		entry["optional"] = port.optional;
		ports.push_back(entry);
	}
	return ports;
}

Dictionary step_to_dictionary(const GraphStep &p_step) {
	const GraphStepKind *kind = GraphStepKinds::find(p_step.kind);
	Dictionary entry;
	entry["id"] = p_step.id;
	entry["kind"] = String(p_step.kind);
	entry["known"] = kind != nullptr;
	entry["category"] = kind ? String(kind->category) : String();
	entry["x"] = p_step.position.x;
	entry["y"] = p_step.position.y;
	entry["parameters"] = texts_of(p_step.parameters);
	// The problem is what the parameters name wrongly, such as a class the engine does not have.
	entry["problem"] = kind ? GraphStepKinds::get_ports_problem(*kind, p_step.parameters) : String();
	entry["inputs"] = kind ? ports_of(*kind, p_step.parameters, GraphPinSide::INPUT) : Array();
	entry["outputs"] = kind ? ports_of(*kind, p_step.parameters, GraphPinSide::OUTPUT) : Array();
	return entry;
}

Dictionary wire_to_dictionary(const GraphWire &p_wire) {
	Dictionary entry;
	entry["from_step"] = p_wire.from_step;
	entry["from_port"] = p_wire.from_port;
	entry["to_step"] = p_wire.to_step;
	entry["to_port"] = p_wire.to_port;
	return entry;
}

Dictionary function_to_dictionary(const GraphScript &p_script, const GraphFunction &p_function) {
	Array steps;
	for (int i = 0; i < p_function.get_step_count(); i++) {
		steps.push_back(step_to_dictionary(p_function.get_step_at(i)));
	}
	Array wires;
	for (int i = 0; i < p_function.get_wire_count(); i++) {
		wires.push_back(wire_to_dictionary(p_function.get_wire_at(i)));
	}
	const GraphCompiledFunction *compiled = p_script.get_compiled(p_function.get_name());
	Dictionary entry;
	entry["name"] = p_function.get_name();
	entry["error"] = compiled ? compiled->error : String();
	entry["steps"] = steps;
	entry["wires"] = wires;
	return entry;
}

Dictionary variable_to_dictionary(const GraphVariable &p_variable) {
	Dictionary entry;
	entry["name"] = String(p_variable.name);
	entry["type"] = int(p_variable.type);
	entry["default"] = text_of(p_variable.default_value);
	entry["exported"] = p_variable.exported;
	entry["hint"] = int(p_variable.hint);
	entry["hint_string"] = p_variable.hint_string;
	entry["category"] = p_variable.category;
	entry["description"] = p_variable.description;
	return entry;
}

// A signal with no arguments is stored as a name. The document always gives the list.
Dictionary signal_to_dictionary(const GraphSignal &p_signal) {
	Dictionary entry;
	entry["name"] = String(p_signal.name);
	entry["arguments"] = GraphSignatures::typed_list_to_array(p_signal.arguments);
	return entry;
}

Dictionary warning_to_dictionary(const GraphWarning &p_warning) {
	Dictionary entry;
	entry["kind"] = p_warning.kind;
	entry["function"] = p_warning.function;
	entry["step"] = p_warning.step;
	entry["name"] = p_warning.name;
	entry["text"] = p_warning.text;
	return entry;
}

} // namespace

String write(const GraphScript &p_script) {
	Array variables;
	for (const GraphVariable &variable : p_script.get_declared_variables()) {
		variables.push_back(variable_to_dictionary(variable));
	}
	Array signals;
	for (const GraphSignal &declared : p_script.get_declared_signals()) {
		signals.push_back(signal_to_dictionary(declared));
	}
	Array functions;
	for (const GraphFunction &function : p_script.get_declared_functions()) {
		functions.push_back(function_to_dictionary(p_script, function));
	}
	Array warnings;
	for (const GraphWarning &warning : GraphWarnings::find(p_script)) {
		warnings.push_back(warning_to_dictionary(warning));
	}

	Dictionary document;
	document["base_type"] = String(p_script.get_base_type());
	document["error"] = p_script.get_error();
	document["variables"] = variables;
	document["signals"] = signals;
	document["functions"] = functions;
	document["warnings"] = warnings;
	return JSON::stringify(document);
}

String write_entries(const Vector<GraphStepEntry> &p_entries) {
	Array entries;
	for (const GraphStepEntry &entry : p_entries) {
		Dictionary shown;
		shown["kind"] = String(entry.kind);
		shown["group"] = entry.group;
		shown["label"] = entry.label;
		shown["parameters"] = texts_of(entry.parameters);
		entries.push_back(shown);
	}
	return JSON::stringify(entries);
}

} // namespace GraphDocument
