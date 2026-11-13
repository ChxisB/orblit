/**************************************************************************/
/*  test_graph_payload.h                                                  */

#pragma once

#include "test_graph_document.h"

namespace TestGraphPayload {

using TestGraphDocument::find_step;
using TestGraphDocument::read_document;
using TestGraphScriptRun::Graph;

constexpr int RUN = 0;
constexpr int THEN = 0;
constexpr int OUT = 0;
constexpr int VALUE = 1;

// Entry (1), Set total (2) and a Constant 7 (3) feeding it, in a function called main.
Graph copy_source() {
	Graph graph("main");
	graph.variable("total", Variant::INT);
	graph.add(1, "entry");
	graph.add_named(2, "set", "total");
	graph.add(3, "constant");
	graph.set(3, "value", "7");
	graph.move(2, 100, 60);
	graph.move(3, 20, 80);
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, VALUE);
	return graph;
}

Dictionary pasted(const Ref<GraphScript> &p_script, const String &p_function, const String &p_payload, const String &p_conflict = "", float p_x = 0, float p_y = 0) {
	const Variant result = JSON::parse_string(p_script->paste_payload(p_function, p_payload, p_x, p_y, p_conflict));
	REQUIRE(result.get_type() == Variant::DICTIONARY);
	return result;
}

Dictionary function_of(const Ref<GraphScript> &p_script, const String &p_name) {
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

TEST_CASE("[Stage][GraphScript] Two wired steps and their variable paste into another script with the wire kept") {
	Graph source = copy_source();
	const String payload = source.script->get_payload("main", "[2, 3]");
	REQUIRE_FALSE(payload.is_empty());
	Graph target("go");
	target.add(1, "entry");

	const Dictionary result = pasted(target.script, "go", payload);

	CHECK(int(result["error"]) == OK);
	const Array ids = result["steps"];
	REQUIRE(ids.size() == 2);
	const GraphVariable *variable = target.script->get_variable("total");
	REQUIRE(variable != nullptr);
	CHECK(variable->type == Variant::INT);
	const GraphFunction *function = target.script->get_function("go");
	CHECK(function->get_step_count() == 3);
	CHECK(function->get_wire_count() == 1);
	const GraphWire wire = function->get_wire_at(0);
	CHECK(function->get_step(wire.from_step)->kind == StringName("constant"));
	CHECK(function->get_step(wire.to_step)->kind == StringName("set"));
	CHECK(function->get_step(wire.from_step)->parameters["value"] == Variant(7));
	CHECK(int(ids[0]) != 1);
}

TEST_CASE("[Stage][GraphScript] The upper left step of a paste lands where it was asked to") {
	Graph source = copy_source();
	Graph target("go");

	pasted(target.script, "go", source.script->get_payload("main", "[2, 3]"), "", 300, 400);

	const GraphFunction *function = target.script->get_function("go");
	// The constant was at (20, 80) and the set at (100, 60), so the corner is (20, 60).
	const GraphWire wire = function->get_wire_at(0);
	CHECK(function->get_step(wire.from_step)->position == Vector2(300, 420));
	CHECK(function->get_step(wire.to_step)->position == Vector2(380, 400));
}

TEST_CASE("[Stage][GraphScript] The entry step does not copy, because a function has one") {
	Graph source = copy_source();

	const String payload = source.script->get_payload("main", "[1, 2, 3]");
	Graph target("go");
	pasted(target.script, "go", payload);

	CHECK(target.script->get_function("go")->get_step_count() == 2);
	CHECK(source.script->get_payload("main", "[1]").is_empty());
}

TEST_CASE("[Stage][GraphScript] An event step does not copy either, because a function has one start") {
	Graph source("on_start");
	source.add(1, "event");
	source.add(2, "print");
	source.set(2, "text", "\"hi\"");
	source.wire(1, THEN, 2, RUN);

	CHECK(source.script->get_payload("on_start", "[1]").is_empty());
	const String payload = source.script->get_payload("on_start", "[1, 2]");
	Graph target("go");
	target.add(1, "entry");
	pasted(target.script, "go", payload);

	CHECK(target.script->get_function("go")->get_step_count() == 2);
	CHECK(target.script->get_error().is_empty());
}

TEST_CASE("[Stage][GraphScript] A payload asks for a choice when a variable of that name has another type") {
	Graph source = copy_source();
	const String payload = source.script->get_payload("main", "[2, 3]");
	Graph target("go");
	target.variable("total", Variant::STRING);
	const String before = target.script->get_document();

	const Dictionary asked = pasted(target.script, "go", payload);

	CHECK(int(asked["error"]) == ERR_ALREADY_EXISTS);
	const Array conflicts = asked["conflicts"];
	REQUIRE(conflicts.size() == 1);
	CHECK(Dictionary(conflicts[0])["kind"] == Variant("variable"));
	CHECK(Dictionary(conflicts[0])["name"] == Variant("total"));
	CHECK(target.script->get_document() == before);
}

TEST_CASE("[Stage][GraphScript] Reuse pastes steps that name the variable already there") {
	Graph source = copy_source();
	const String payload = source.script->get_payload("main", "[2, 3]");
	Graph target("go");
	target.variable("total", Variant::STRING);

	const Dictionary result = pasted(target.script, "go", payload, "reuse");

	CHECK(int(result["error"]) == OK);
	CHECK(target.script->get_variable("total")->type == Variant::STRING);
	CHECK(target.script->get_declared_variables().size() == 1);
	CHECK(target.script->get_function("go")->get_step_count() == 2);
}

TEST_CASE("[Stage][GraphScript] Keep both declares the variable again under a new name and points the steps at it") {
	Graph source = copy_source();
	const String payload = source.script->get_payload("main", "[2, 3]");
	Graph target("go");
	target.variable("total", Variant::STRING);

	const Dictionary result = pasted(target.script, "go", payload, "keep_both");

	CHECK(int(result["error"]) == OK);
	CHECK(target.script->get_variable("total")->type == Variant::STRING);
	REQUIRE(target.script->get_variable("total_2") != nullptr);
	CHECK(target.script->get_variable("total_2")->type == Variant::INT);
	const int set_id = int(Array(result["steps"])[0]);
	const GraphFunction *function = target.script->get_function("go");
	CHECK(function->get_step(set_id)->parameters["name"] == Variant("total_2"));
}

TEST_CASE("[Stage][GraphScript] A choice that is not reuse or keep both is refused") {
	Graph source = copy_source();
	const String payload = source.script->get_payload("main", "[2, 3]");
	Graph target("go");
	target.variable("total", Variant::STRING);
	const String before = target.script->get_document();

	CHECK(int(pasted(target.script, "go", payload, "overwrite")["error"]) == ERR_INVALID_PARAMETER);
	CHECK(target.script->get_document() == before);
}

TEST_CASE("[Stage][GraphScript] A signal with other arguments is a conflict too") {
	Graph source("main");
	CHECK(source.script->add_signal("done") == OK);
	CHECK(source.script->set_signal_arguments("done", "[{\"name\":\"score\",\"type\":2}]") == OK);
	source.add(1, "entry");
	source.add(2, "emit");
	source.set(2, "signal", "\"done\"");
	const String payload = source.script->get_payload("main", "[2]");
	Graph target("go");
	CHECK(target.script->add_signal("done") == OK);

	const Dictionary asked = pasted(target.script, "go", payload);

	CHECK(int(asked["error"]) == ERR_ALREADY_EXISTS);
	CHECK(Dictionary(Array(asked["conflicts"])[0])["kind"] == Variant("signal"));
	CHECK(int(pasted(target.script, "go", payload, "keep_both")["error"]) == OK);
	CHECK(target.script->get_declared_signals().size() == 2);
}

TEST_CASE("[Stage][GraphScript] A signal whose argument has another type is a conflict") {
	Graph source("main");
	CHECK(source.script->add_signal("done") == OK);
	CHECK(source.script->set_signal_arguments("done", "[{\"name\":\"score\",\"type\":2}]") == OK);
	source.add(1, "emit");
	source.set(1, "signal", "\"done\"");
	Graph target("go");
	CHECK(target.script->add_signal("done") == OK);
	CHECK(target.script->set_signal_arguments("done", "[{\"name\":\"score\",\"type\":3}]") == OK);

	CHECK(int(pasted(target.script, "go", source.script->get_payload("main", "[1]"))["error"]) == ERR_ALREADY_EXISTS);
}

TEST_CASE("[Stage][GraphScript] A payload declares a signal and a function the target lacks") {
	Graph source("main");
	CHECK(source.script->add_signal("done") == OK);
	CHECK(source.script->add_function("helper") == OK);
	source.add(1, "entry");
	source.add(2, "emit");
	source.set(2, "signal", "\"done\"");
	source.add(3, "call_function");
	source.set(3, "function", "\"helper\"");
	const String payload = source.script->get_payload("main", "[2, 3]");
	Graph target("go");

	CHECK(int(pasted(target.script, "go", payload)["error"]) == OK);

	CHECK(target.script->get_declared_signals().size() == 1);
	CHECK(target.script->get_function("helper") != nullptr);
}

TEST_CASE("[Stage][GraphScript] A return does not paste into a function that has no entry") {
	Graph source("main");
	source.add(1, "entry");
	source.add(2, "return");
	const String payload = source.script->get_payload("main", "[2]");
	Graph event_function("on_start");
	event_function.add(1, "event");
	const String before = event_function.script->get_document();

	const Dictionary refused = pasted(event_function.script, "on_start", payload);

	CHECK(int(refused["error"]) == ERR_INVALID_PARAMETER);
	CHECK(event_function.script->get_document() == before);
	Graph function("go");
	function.add(1, "entry");
	CHECK(int(pasted(function.script, "go", payload)["error"]) == OK);
}

TEST_CASE("[Stage][GraphScript] Text that is not a payload is refused and changes nothing") {
	Graph target("go");
	target.add(1, "entry");
	const String before = target.script->get_document();

	for (const String &text : { String("hello"), String("[1, 2]"), String("{\"steps\": 1}"), String() }) {
		CAPTURE(text);
		CHECK(int(pasted(target.script, "go", text)["error"]) == ERR_INVALID_DATA);
	}
	Graph source = copy_source();
	CHECK(int(pasted(target.script, "missing", source.script->get_payload("main", "[2]"))["error"]) == ERR_DOES_NOT_EXIST);
	CHECK(target.script->get_document() == before);
}

TEST_CASE("[Stage][GraphScript] A payload of no steps is empty") {
	Graph source = copy_source();

	CHECK(source.script->get_payload("main", "[]").is_empty());
	CHECK(source.script->get_payload("main", "[99]").is_empty());
	CHECK(source.script->get_payload("missing", "[2]").is_empty());
	CHECK(source.script->get_payload("main", "oops").is_empty());
}

} // namespace TestGraphPayload
