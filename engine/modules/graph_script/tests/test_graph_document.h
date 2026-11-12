/**************************************************************************/
/*  test_graph_document.h                                                 */

#pragma once

#include "test_graph_script_run.h"

#include "core/io/json.h"

namespace TestGraphDocument {

using TestGraphScriptRun::Graph;

constexpr int RUN = 0;
constexpr int THEN = 0;
constexpr int OUT = 0;
constexpr int VALUE = 1;

Dictionary read_document(const Ref<GraphScript> &p_script) {
	const Variant parsed = JSON::parse_string(p_script->get_document());
	REQUIRE(parsed.get_type() == Variant::DICTIONARY);
	return parsed;
}

Dictionary first_function(const Dictionary &p_document) {
	const Array functions = p_document["functions"];
	REQUIRE(functions.size() >= 1);
	return functions[0];
}

Dictionary find_step(const Dictionary &p_function, int p_id) {
	const Array steps = p_function["steps"];
	for (const Variant &step : steps) {
		const Dictionary entry = step;
		if (int(entry["id"]) == p_id) {
			return entry;
		}
	}
	FAIL(vformat("The document has no step %d.", p_id));
	return Dictionary();
}

bool lists_kind(const String &p_json, const String &p_kind) {
	const Array entries = JSON::parse_string(p_json);
	for (const Variant &value : entries) {
		const Dictionary entry = value;
		if (entry["kind"] == Variant(p_kind)) {
			return true;
		}
	}
	return false;
}

TEST_CASE("[Stage][GraphScript] The document lists steps with their ports and the wires between them") {
	Graph graph("main");
	graph.variable("total", Variant::INT);
	graph.add(1, "entry");
	graph.add_named(2, "set", "total");
	graph.add(3, "constant");
	graph.set(3, "value", "Vector2(1, 2)");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, VALUE);

	const Dictionary function = first_function(read_document(graph.script));
	CHECK(function["name"] == Variant("main"));
	CHECK(String(function["error"]).is_empty());
	CHECK(Array(function["steps"]).size() == 3);

	const Dictionary entry = find_step(function, 1);
	CHECK(entry["kind"] == Variant("entry"));
	CHECK(bool(entry["known"]));
	const Array entry_outputs = entry["outputs"];
	REQUIRE(entry_outputs.size() >= 1);
	CHECK(Dictionary(entry_outputs[0])["name"] == Variant("then"));
	CHECK(Dictionary(entry_outputs[0])["sort"] == Variant("flow"));

	const Dictionary set = find_step(function, 2);
	const Array set_inputs = set["inputs"];
	REQUIRE(set_inputs.size() == 2);
	CHECK(Dictionary(set_inputs[0])["sort"] == Variant("flow"));
	CHECK(Dictionary(set_inputs[1])["sort"] == Variant("data"));
	CHECK(Dictionary(set["parameters"])["name"] == Variant("\"total\""));

	// A parameter is Variant text, the same text set_step_parameter takes.
	CHECK(Dictionary(Dictionary(find_step(function, 3))["parameters"])["value"] == Variant("Vector2(1, 2)"));

	const Array wires = function["wires"];
	REQUIRE(wires.size() == 2);
	const Dictionary wire = wires[0];
	CHECK(int(wire["from_step"]) == 1);
	CHECK(int(wire["to_step"]) == 2);
}

TEST_CASE("[Stage][GraphScript] The document carries variables and signals in one shape") {
	Ref<GraphScript> script;
	script.instantiate();
	CHECK(script->add_variable("speed", Variant::FLOAT) == OK);
	CHECK(script->set_variable_default("speed", "2.5") == OK);
	CHECK(script->set_variable_exported("speed", true) == OK);
	CHECK(script->set_variable_info("speed", "Movement", "How fast it goes") == OK);
	// A signal with no arguments is stored as its name, and the document still gives it a list.
	CHECK(script->add_signal("hit") == OK);
	CHECK(script->add_signal("scored") == OK);
	CHECK(script->set_signal_arguments("scored", "[{\"name\": \"amount\", \"type\": 2}]") == OK);

	const Dictionary document = read_document(script);
	const Array variables = document["variables"];
	REQUIRE(variables.size() == 1);
	const Dictionary speed = variables[0];
	CHECK(speed["name"] == Variant("speed"));
	CHECK(int(speed["type"]) == int(Variant::FLOAT));
	CHECK(speed["default"] == Variant("2.5"));
	CHECK(bool(speed["exported"]));
	CHECK(speed["category"] == Variant("Movement"));
	CHECK(speed["description"] == Variant("How fast it goes"));

	const Array signals = document["signals"];
	REQUIRE(signals.size() == 2);
	CHECK(Array(Dictionary(signals[0])["arguments"]).is_empty());
	const Array arguments = Dictionary(signals[1])["arguments"];
	REQUIRE(arguments.size() == 1);
	CHECK(Dictionary(arguments[0])["name"] == Variant("amount"));
	CHECK(int(Dictionary(arguments[0])["type"]) == int(Variant::INT));
}

TEST_CASE("[Stage][GraphScript] The document names the function that does not compile and lists warnings") {
	Graph graph("main");
	graph.add(1, "entry");
	graph.add(2, "set");
	graph.wire(1, THEN, 2, RUN);

	const Dictionary document = read_document(graph.script);
	// The Set step names no variable, so the function does not compile.
	CHECK_FALSE(String(first_function(document)["error"]).is_empty());
	CHECK(document["error"] == Variant(graph.script->get_error()));
	CHECK(document["warnings"].get_type() == Variant::ARRAY);
}

TEST_CASE("[Stage][GraphScript] A step of an unknown kind is in the document with no ports") {
	Graph graph("main");
	graph.add(1, "entry");
	Array functions = graph.script->get_functions();
	Dictionary function = functions[0];
	Array steps = function["steps"];
	Dictionary step = steps[0];
	step["kind"] = "future_kind";
	graph.script->set_functions(functions);

	const Dictionary shown = find_step(first_function(read_document(graph.script)), 1);
	CHECK(shown["kind"] == Variant("future_kind"));
	CHECK_FALSE(bool(shown["known"]));
	CHECK(Array(shown["inputs"]).is_empty());
	CHECK(Array(shown["outputs"]).is_empty());
}

TEST_CASE("[Stage][GraphScript] Find steps offers what fits a dragged flow wire") {
	Ref<GraphScript> script;
	script.instantiate();

	const String from_flow = script->find_steps("", "output", Variant::NIL, "flow");
	CHECK(lists_kind(from_flow, "branch"));
	CHECK_FALSE(lists_kind(from_flow, "constant"));

	const String from_nothing = script->find_steps("", "", Variant::NIL, "flow");
	CHECK(lists_kind(from_nothing, "branch"));
	CHECK(lists_kind(from_nothing, "constant"));

	const Array entries = JSON::parse_string(from_flow);
	REQUIRE(entries.size() >= 1);
	const Dictionary first = entries[0];
	CHECK(first.has("group"));
	CHECK(first.has("label"));
	CHECK(first["parameters"].get_type() == Variant::DICTIONARY);
}

TEST_CASE("[Stage][GraphScript] Find steps refuses a side or sort it does not know") {
	Ref<GraphScript> script;
	script.instantiate();

	ERR_PRINT_OFF;
	CHECK(script->find_steps("", "sideways", Variant::NIL, "flow").is_empty());
	CHECK(script->find_steps("", "output", Variant::NIL, "sparkle").is_empty());
	ERR_PRINT_ON;
}

} // namespace TestGraphDocument
