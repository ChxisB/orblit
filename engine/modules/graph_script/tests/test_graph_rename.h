/**************************************************************************/
/*  test_graph_rename.h                                                   */

#pragma once

#include "test_graph_document.h"

namespace TestGraphRename {

using TestGraphDocument::find_step;
using TestGraphDocument::read_document;
using TestGraphScriptRun::attach;
using TestGraphScriptRun::Graph;

constexpr int RUN = 0;
constexpr int THEN = 0;
constexpr int OUT_PORT = 0;
constexpr int VALUE = 1;
constexpr int TARGET = 1;

Dictionary function_named(const Ref<GraphScript> &p_script, const String &p_name) {
	const Array functions = read_document(p_script)["functions"];
	for (const Variant &value : functions) {
		const Dictionary function = value;
		if (function["name"] == Variant(p_name)) {
			return function;
		}
	}
	FAIL(vformat("The document has no function %s.", p_name));
	return Dictionary();
}

// The text a step parameter is stored as, such as "\"total\"".
Variant parameter_of(const Ref<GraphScript> &p_script, const String &p_function, int p_step, const String &p_parameter) {
	const Dictionary parameters = find_step(function_named(p_script, p_function), p_step)["parameters"];
	return parameters[p_parameter];
}

String quoted(const String &p_text) {
	return "\"" + p_text + "\"";
}

TEST_CASE("[Stage][GraphScript] Renaming a variable renames the steps that read and write it") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.variable("other", Variant::INT);
	graph.add(1, "entry");
	graph.add_named(2, "set", "total");
	graph.add(3, "constant");
	graph.set(3, "value", "7");
	graph.add_named(4, "get", "total");
	graph.add_named(5, "get", "other");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT_PORT, 2, VALUE);

	CHECK(graph.script->rename_variable("total", "sum") == OK);

	CHECK(graph.script->get_variable("sum") != nullptr);
	CHECK(graph.script->get_variable("total") == nullptr);
	CHECK(parameter_of(graph.script, "go", 2, "name") == Variant(quoted("sum")));
	CHECK(parameter_of(graph.script, "go", 4, "name") == Variant(quoted("sum")));
	CHECK(parameter_of(graph.script, "go", 5, "name") == Variant(quoted("other")));

	Entity *entity = attach(graph.script);
	entity->call("go");
	CHECK(entity->get("sum") == Variant(7));
	memdelete(entity);
}

TEST_CASE("[GraphScript] A renamed variable keeps its type, default and export settings") {
	Ref<GraphScript> script;
	script.instantiate();
	CHECK(script->add_variable("speed", Variant::FLOAT) == OK);
	CHECK(script->set_variable_default("speed", "2.5") == OK);
	CHECK(script->set_variable_exported("speed", true) == OK);
	CHECK(script->set_variable_info("speed", "Movement", "How fast it goes") == OK);

	CHECK(script->rename_variable("speed", "pace") == OK);

	const GraphVariable *pace = script->get_variable("pace");
	REQUIRE(pace != nullptr);
	CHECK(pace->type == Variant::FLOAT);
	CHECK(pace->default_value == Variant(2.5));
	CHECK(pace->exported);
	CHECK(pace->category == "Movement");
	CHECK(pace->description == "How fast it goes");
}

TEST_CASE("[Stage][GraphScript] Renaming a function renames the calls that run on the owner") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	Graph helper(graph.script, "helper");
	helper.add(1, "entry");
	helper.add_named(2, "set", "total");
	helper.set(2, "value", "9");
	helper.wire(1, THEN, 2, RUN);
	graph.add(1, "entry");
	graph.add(2, "call_function");
	graph.set(2, "function", quoted("helper"));
	graph.add(3, "self");
	graph.add(4, "call_function");
	graph.set(4, "function", quoted("helper"));
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT_PORT, 4, TARGET);

	CHECK(graph.script->rename_function("helper", "assist") == OK);

	CHECK(graph.script->get_function("assist") != nullptr);
	CHECK(graph.script->get_function("helper") == nullptr);
	CHECK(parameter_of(graph.script, "go", 2, "function") == Variant(quoted("assist")));
	// A call with a wired target may be a call on another script, which keeps its own names.
	CHECK(parameter_of(graph.script, "go", 4, "function") == Variant(quoted("helper")));

	Entity *entity = attach(graph.script);
	entity->call("go");
	CHECK(entity->get("total") == Variant(9));
	memdelete(entity);
}

TEST_CASE("[GraphScript] Renaming a signal renames the steps that name it on the owner") {
	Graph graph("a");
	Graph listener(graph.script, "b");
	CHECK(graph.script->add_signal("scored") == OK);
	CHECK(graph.script->set_signal_arguments("scored", "[{\"name\": \"amount\", \"type\": 2}]") == OK);
	graph.add(1, "emit");
	graph.set(1, "signal", quoted("scored"));
	graph.add(2, "connect");
	graph.set(2, "signal", quoted("scored"));
	graph.set(2, "method", quoted("react"));
	graph.add(3, "await_signal");
	graph.set(3, "signal", quoted("scored"));
	graph.add(4, "self");
	graph.wire(4, OUT_PORT, 3, TARGET);
	graph.add(5, "emit");
	graph.set(5, "signal", quoted("other"));
	listener.add(1, "signal_received");
	listener.set(1, "signal", quoted("scored"));
	listener.add(2, "signal_received");
	listener.set(2, "signal", quoted("scored"));
	listener.set(2, "target", quoted("Enemy"));

	CHECK(graph.script->rename_signal("scored", "points") == OK);

	CHECK(graph.script->has_script_signal("points"));
	CHECK_FALSE(graph.script->has_script_signal("scored"));
	const Array signals = read_document(graph.script)["signals"];
	REQUIRE(signals.size() == 1);
	CHECK(Array(Dictionary(signals[0])["arguments"]).size() == 1);
	CHECK(parameter_of(graph.script, "a", 1, "signal") == Variant(quoted("points")));
	CHECK(parameter_of(graph.script, "a", 2, "signal") == Variant(quoted("points")));
	CHECK(parameter_of(graph.script, "a", 3, "signal") == Variant(quoted("scored")));
	CHECK(parameter_of(graph.script, "a", 5, "signal") == Variant(quoted("other")));
	CHECK(parameter_of(graph.script, "b", 1, "signal") == Variant(quoted("points")));
	CHECK(parameter_of(graph.script, "b", 2, "signal") == Variant(quoted("scored")));
}

TEST_CASE("[GraphScript] A rename that is not valid is refused and changes nothing") {
	Graph graph("go");
	Graph other(graph.script, "other");
	graph.variable("total", Variant::INT);
	graph.variable("count", Variant::INT);
	graph.add_named(1, "get", "total");
	CHECK(graph.script->add_signal("scored") == OK);
	CHECK(graph.script->add_signal("missed") == OK);
	const Array functions = graph.script->get_functions();
	const Array variables = graph.script->get_variables();
	const Array signals = graph.script->get_signals();

	CHECK(graph.script->rename_variable("nowhere", "sum") == ERR_DOES_NOT_EXIST);
	CHECK(graph.script->rename_variable("total", "not valid") == ERR_INVALID_PARAMETER);
	CHECK(graph.script->rename_variable("total", "") == ERR_INVALID_PARAMETER);
	CHECK(graph.script->rename_variable("total", "count") == ERR_ALREADY_EXISTS);
	CHECK(graph.script->rename_variable("total", "total") == ERR_ALREADY_EXISTS);
	CHECK(graph.script->rename_function("nowhere", "run") == ERR_DOES_NOT_EXIST);
	CHECK(graph.script->rename_function("go", "not valid") == ERR_INVALID_PARAMETER);
	CHECK(graph.script->rename_function("go", "other") == ERR_ALREADY_EXISTS);
	CHECK(graph.script->rename_signal("nowhere", "hit") == ERR_DOES_NOT_EXIST);
	CHECK(graph.script->rename_signal("scored", "not valid") == ERR_INVALID_PARAMETER);
	CHECK(graph.script->rename_signal("scored", "missed") == ERR_ALREADY_EXISTS);

	CHECK(graph.script->get_functions() == functions);
	CHECK(graph.script->get_variables() == variables);
	CHECK(graph.script->get_signals() == signals);
}

TEST_CASE("[GraphScript] Setting the type of a variable resets a default that no longer fits") {
	Graph graph("go");
	graph.variable("speed", Variant::INT, "4");

	CHECK(graph.script->set_variable_type("speed", Variant::INT) == OK);
	CHECK(graph.script->get_variable("speed")->default_value == Variant(4));

	CHECK(graph.script->set_variable_type("speed", Variant::FLOAT) == OK);
	const GraphVariable *speed = graph.script->get_variable("speed");
	CHECK(speed->type == Variant::FLOAT);
	CHECK(speed->default_value == Variant(0.0));
	CHECK(graph.script->set_variable_default("speed", "1.5") == OK);
}

TEST_CASE("[GraphScript] Setting the type of a variable refuses a name or type the script does not know") {
	Graph graph("go");
	graph.variable("speed", Variant::INT);

	CHECK(graph.script->set_variable_type("nowhere", Variant::INT) == ERR_DOES_NOT_EXIST);
	CHECK(graph.script->set_variable_type("speed", -1) == ERR_INVALID_PARAMETER);
	CHECK(graph.script->set_variable_type("speed", Variant::VARIANT_MAX) == ERR_INVALID_PARAMETER);
	CHECK(graph.script->get_variable("speed")->type == Variant::INT);
}

} // namespace TestGraphRename
