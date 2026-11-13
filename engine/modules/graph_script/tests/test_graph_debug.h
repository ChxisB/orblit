/**************************************************************************/
/*  test_graph_debug.h                                                    */

#pragma once

#include "../graph_call.h"
#include "../graph_debug.h"
#include "test_graph_functions.h"

#include "core/debugger/engine_debugger.h"
#include "core/debugger/script_debugger.h"

namespace TestGraphDebug {

using TestGraphFunctions::entry_text;
using TestGraphFunctions::list_text;
using TestGraphScriptRun::attach;
using TestGraphScriptRun::BODY;
using TestGraphScriptRun::BUDGET_SETTING;
using TestGraphScriptRun::Graph;
using TestGraphScriptRun::ITEM;
using TestGraphScriptRun::OUT;
using TestGraphScriptRun::RUN;
using TestGraphScriptRun::THEN;
using TestGraphScriptRun::VALUE;

// What the debugger sees at one stop.
struct Stop {
	String reason;
	bool is_error = false;
	// Each level as "function:step", the innermost first.
	String stack;
	String source;
	Dictionary locals;
	Dictionary members;
	// The value of each probe the debugger asked for at this stop, and whether reading them moved
	// the random generator of the call.
	Array wires;
	bool random_moved = false;
};

// Stands in for the engine's debugger. It writes down each stop and answers with the next
// command in the list, or a continue when the list is empty. The commands set the same lines
// left and depth that the console debugger sets.
class FakeDebugger : public EngineDebugger {
public:
	enum Command {
		CONTINUE,
		STEP_INTO,
		STEP_OVER,
		STEP_OUT,
	};

	Vector<Stop> stops;
	Vector<Command> commands;
	// Wires to read at each stop, as level, step id and input port.
	Vector<Vector3i> probes;
	int max_depth = -1;

	FakeDebugger() {
		singleton = this;
		script_debugger = memnew(ScriptDebugger);
	}

	ScriptDebugger *get() const { return script_debugger; }

	void stop_at(const Graph &p_graph, const String &p_function, int p_step) const {
		script_debugger->insert_breakpoint(p_step, GraphDebug::make_source(p_graph.script->get_path(), p_function));
	}

	void clear_at(const Graph &p_graph, const String &p_function, int p_step) const {
		script_debugger->remove_breakpoint(p_step, GraphDebug::make_source(p_graph.script->get_path(), p_function));
	}

	// The innermost level of each stop, such as "go:2, go:3".
	String trace() const {
		String text;
		for (const Stop &stop : stops) {
			text += (text.is_empty() ? "" : ", ") + stop.stack.get_slice(" ", 0);
		}
		return text;
	}

	virtual void send_message(const String &p_msg, const Array &p_data) override {}
	virtual void send_error(const String &p_func, const String &p_file, int p_line, const String &p_err, const String &p_descr, bool p_editor_notify, ErrorHandlerType p_type) override {}

	virtual void debug(bool p_can_continue, bool p_is_error_breakpoint) override {
		ScriptLanguage *language = script_debugger->get_break_language();
		stops.push_back(_describe(*language, p_is_error_breakpoint));
		const Command command = commands.is_empty() ? CONTINUE : commands[0];
		if (!commands.is_empty()) {
			commands.remove_at(0);
		}
		static const int DEPTH[] = { -1, -1, 0, 1 };
		script_debugger->set_depth(DEPTH[command]);
		script_debugger->set_lines_left(command == CONTINUE ? -1 : 1);
	}

private:
	static Dictionary _pairs(const List<String> &p_names, const List<Variant> &p_values) {
		Dictionary pairs;
		List<Variant>::ConstIterator value = p_values.begin();
		for (const String &name : p_names) {
			pairs[name] = *value;
			++value;
		}
		return pairs;
	}

	void _probe(Stop &r_stop) const {
		const GraphCall *call = GraphDebug::get_level(0);
		if (!call) {
			return;
		}
		const uint64_t before = call->get_random().get_state();
		for (const Vector3i &probe : probes) {
			r_stop.wires.push_back(GraphDebug::get_wire_value(probe.x, probe.y, probe.z));
		}
		r_stop.random_moved = call->get_random().get_state() != before;
	}

	Stop _describe(ScriptLanguage &p_language, bool p_is_error) const {
		Stop stop;
		stop.reason = p_language.debug_get_error();
		stop.is_error = p_is_error;
		for (int level = 0; level < p_language.debug_get_stack_level_count(); level++) {
			stop.stack += (level == 0 ? "" : " ") + vformat("%s:%d", p_language.debug_get_stack_level_function(level), p_language.debug_get_stack_level_line(level));
		}
		stop.source = p_language.debug_get_stack_level_source(0);
		_probe(stop);
		List<String> names;
		List<Variant> values;
		p_language.debug_get_stack_level_locals(0, &names, &values, -1, max_depth);
		stop.locals = _pairs(names, values);
		names.clear();
		values.clear();
		p_language.debug_get_stack_level_members(0, &names, &values, -1, max_depth);
		stop.members = _pairs(names, values);
		return stop;
	}
};

// "helper" sets total twice, at steps 2 and 3. "go" takes n, has a local named count, sets
// total, calls "helper" at step 3 and sets total again at step 4. Both functions have a step 3,
// so a breakpoint has to name its function.
Graph stepping_graph() {
	Graph helper("helper");
	helper.variable("total", Variant::INT);
	helper.variable("list", Variant::ARRAY, "[[1, 2], [3]]");
	helper.add(1, "entry");
	helper.add_named(2, "set", "total");
	helper.set(2, "value", "10");
	helper.add_named(3, "set", "total");
	helper.set(3, "value", "11");
	helper.wire(1, THEN, 2, RUN);
	helper.wire(2, THEN, 3, RUN);

	Graph go(helper.script, "go");
	go.add(1, "entry");
	go.set(1, "arguments", list_text(entry_text("n", Variant::INT)));
	go.set(1, "locals", list_text(entry_text("count", Variant::INT)));
	go.add_named(2, "set", "total");
	go.set(2, "value", "1");
	go.add(3, "call_function");
	go.set(3, "function", "\"helper\"");
	go.add_named(4, "set", "total");
	go.set(4, "value", "2");
	go.wire(1, THEN, 2, RUN);
	go.wire(2, THEN, 3, RUN);
	go.wire(3, THEN, 4, RUN);
	return go;
}

TEST_CASE("[Stage][GraphScript] A breakpoint stops before its step and shows the stack and the values") {
	Graph graph = stepping_graph();
	FakeDebugger debugger;
	debugger.stop_at(graph, "go", 3);

	Entity *entity = attach(graph.script);
	entity->call("go", 5);

	REQUIRE(debugger.stops.size() == 1);
	const Stop &stop = debugger.stops[0];
	CHECK(stop.reason == "Breakpoint");
	CHECK(!stop.is_error);
	CHECK(stop.stack == "go:3");
	CHECK(stop.source == GraphDebug::make_source(graph.script->get_path(), "go"));
	CHECK(stop.locals.size() == 2);
	CHECK(stop.locals["n"] == Variant(5));
	CHECK(stop.locals["count"] == Variant(0));
	// Step 2 has run and step 4 has not.
	CHECK(stop.members["total"] == Variant(1));
	CHECK(entity->get("total") == Variant(2));
	CHECK(GraphDebug::get_level_count() == 0);
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A step stop says it is a step") {
	Graph graph = stepping_graph();
	FakeDebugger debugger;
	debugger.stop_at(graph, "go", 2);
	debugger.commands.push_back(FakeDebugger::STEP_OVER);

	Entity *entity = attach(graph.script);
	entity->call("go", 0);

	REQUIRE(debugger.stops.size() == 2);
	CHECK(debugger.stops[0].reason == "Breakpoint");
	CHECK(debugger.stops[1].reason == "Step");
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Step over runs a called function without stopping in it") {
	Graph graph = stepping_graph();
	FakeDebugger debugger;
	debugger.stop_at(graph, "go", 2);
	debugger.commands.push_back(FakeDebugger::STEP_OVER);
	debugger.commands.push_back(FakeDebugger::STEP_OVER);

	Entity *entity = attach(graph.script);
	entity->call("go", 0);

	CHECK(debugger.trace() == "go:2, go:3, go:4");
	CHECK(entity->get("total") == Variant(2));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Step into enters a called function and shows both levels") {
	Graph graph = stepping_graph();
	FakeDebugger debugger;
	debugger.stop_at(graph, "go", 3);
	debugger.commands.push_back(FakeDebugger::STEP_INTO);
	debugger.commands.push_back(FakeDebugger::STEP_INTO);

	Entity *entity = attach(graph.script);
	entity->call("go", 0);

	REQUIRE(debugger.stops.size() == 3);
	CHECK(debugger.stops[1].stack == "helper:2 go:3");
	CHECK(debugger.stops[2].stack == "helper:3 go:3");
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Step out finishes the called function and stops in its caller") {
	Graph graph = stepping_graph();
	FakeDebugger debugger;
	debugger.stop_at(graph, "helper", 2);
	debugger.commands.push_back(FakeDebugger::STEP_OUT);

	Entity *entity = attach(graph.script);
	entity->call("go", 0);

	REQUIRE(debugger.stops.size() == 2);
	CHECK(debugger.stops[0].stack == "helper:2 go:3");
	CHECK(debugger.stops[1].stack == "go:4");
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Continue runs to the next breakpoint") {
	Graph graph = stepping_graph();
	FakeDebugger debugger;
	debugger.stop_at(graph, "go", 2);
	debugger.stop_at(graph, "go", 4);

	Entity *entity = attach(graph.script);
	entity->call("go", 0);

	CHECK(debugger.trace() == "go:2, go:4");
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A breakpoint names its function and not only its step") {
	Graph graph = stepping_graph();
	FakeDebugger debugger;
	debugger.stop_at(graph, "helper", 3);

	Entity *entity = attach(graph.script);
	entity->call("go", 0);

	// Step 3 of "go" is a different step and does not stop.
	CHECK(debugger.trace() == "helper:3");
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A cleared breakpoint does not stop") {
	Graph graph = stepping_graph();
	FakeDebugger debugger;
	debugger.stop_at(graph, "go", 3);
	debugger.clear_at(graph, "go", 3);

	Entity *entity = attach(graph.script);
	entity->call("go", 0);

	CHECK(debugger.stops.is_empty());
	CHECK(entity->get("total") == Variant(2));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A stop shows values to the depth the debugger asks for") {
	Graph graph = stepping_graph();
	FakeDebugger debugger;
	debugger.max_depth = 1;
	debugger.stop_at(graph, "helper", 2);

	Entity *entity = attach(graph.script);
	entity->call("go", 0);

	REQUIRE(debugger.stops.size() == 1);
	const Array list = debugger.stops[0].members["list"];
	REQUIRE(list.size() == 2);
	CHECK(list[0] == Variant("Array (2 items)"));
	CHECK(list[1] == Variant("Array (1 items)"));
	memdelete(entity);
}

TEST_CASE("[GraphScript] A value is cut to the depth and the count the caller asks for") {
	Array inner = { 1, 2, 3 };
	Array outer = { inner, 4 };
	CHECK(GraphDebug::limit(outer, -1, -1) == Variant(outer));
	CHECK(GraphDebug::limit(outer, -1, 0) == Variant("Array (2 items)"));
	CHECK(GraphDebug::limit(inner, 2, -1) == Variant(Array({ 1, 2, "... 1 more" })));
	Dictionary pair;
	pair["a"] = 1;
	pair["b"] = 2;
	const Dictionary cut = GraphDebug::limit(pair, 1, -1);
	CHECK(cut.size() == 2);
	CHECK(cut["a"] == Variant(1));
	CHECK(cut["..."] == Variant("1 more"));
	CHECK(GraphDebug::limit(Variant(7), 1, 0) == Variant(7));
}

// A failing call stops like a breakpoint, because a runtime error is what a person asks the debugger about.
TEST_CASE("[Stage][GraphScript] A failing call breaks when the debugger asks for error breaks") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "while");
	graph.set(2, "condition", "true");
	graph.wire(1, THEN, 2, RUN);
	ProjectSettings::get_singleton()->set_setting(BUDGET_SETTING, 50);

	Entity *entity = attach(graph.script);
	{
		FakeDebugger debugger;
		ErrorDetector errors;
		ERR_PRINT_OFF;
		entity->call("go");
		ERR_PRINT_ON;
		REQUIRE(debugger.stops.size() == 1);
		CHECK(debugger.stops[0].is_error);
		CHECK(debugger.stops[0].stack == "go:2");
		CHECK(debugger.stops[0].reason.contains("step 2"));
	}
	{
		FakeDebugger debugger;
		debugger.get()->set_ignore_error_breaks(true);
		ErrorDetector errors;
		ERR_PRINT_OFF;
		entity->call("go");
		ERR_PRINT_ON;
		CHECK(debugger.stops.is_empty());
	}
	ProjectSettings::get_singleton()->set_setting(BUDGET_SETTING, 1000000);
	memdelete(entity);
}

// A call with no debugger attached pays one check of EngineDebugger::is_active a step. Nothing
// here measures that cost. The cases above only show that the call is the same without one.
TEST_CASE("[Stage][GraphScript] A call with no debugger attached runs as before") {
	Graph graph = stepping_graph();
	Entity *entity = attach(graph.script);
	entity->call("go", 0);
	CHECK(entity->get("total") == Variant(2));
	CHECK(GraphDebug::get_level_count() == 0);
	memdelete(entity);
}

// "go" sets total from the sum of 2 and 3, which sit on the inputs of an Operator.
Graph sum_graph() {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.add(1, "entry");
	graph.add_named(2, "set", "total");
	graph.add_operator(3, "+");
	graph.set(3, "a", "2");
	graph.set(3, "b", "3");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, VALUE);
	return graph;
}

TEST_CASE("[Stage][GraphScript] A stop reads the value on a wire and on an input with no wire") {
	Graph graph = sum_graph();
	FakeDebugger debugger;
	debugger.stop_at(graph, "go", 2);
	debugger.probes.push_back(Vector3i(0, 2, VALUE));
	debugger.probes.push_back(Vector3i(0, 3, 0));

	Entity *entity = attach(graph.script);
	entity->call("go");

	REQUIRE(debugger.stops.size() == 1);
	CHECK(debugger.stops[0].wires[0] == Variant(5));
	CHECK(debugger.stops[0].wires[1] == Variant(2));
	CHECK(entity->get("total") == Variant(5));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A wire read gives null for a level, a step or a port that is not there") {
	Graph graph = sum_graph();
	FakeDebugger debugger;
	debugger.stop_at(graph, "go", 2);
	debugger.probes.push_back(Vector3i(1, 2, VALUE));
	debugger.probes.push_back(Vector3i(-1, 2, VALUE));
	debugger.probes.push_back(Vector3i(0, 9, VALUE));
	debugger.probes.push_back(Vector3i(0, 2, 7));
	debugger.probes.push_back(Vector3i(0, 2, -1));

	Entity *entity = attach(graph.script);
	entity->call("go");

	REQUIRE(debugger.stops.size() == 1);
	for (const Variant &wire : debugger.stops[0].wires) {
		CHECK(wire.get_type() == Variant::NIL);
	}
	CHECK(GraphDebug::get_wire_value(0, 2, VALUE).get_type() == Variant::NIL);
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A wire from a step that ran gives the value that step stored") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.variable("items", Variant::ARRAY, "[4, 5, 6]");
	graph.add(1, "entry");
	graph.add(2, "each");
	graph.add_named(3, "get", "items");
	graph.add_named(4, "set", "total");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, 1);
	graph.wire(2, BODY, 4, RUN);
	graph.wire(2, ITEM, 4, VALUE);
	FakeDebugger debugger;
	debugger.stop_at(graph, "go", 4);
	debugger.probes.push_back(Vector3i(0, 4, VALUE));

	Entity *entity = attach(graph.script);
	entity->call("go");

	REQUIRE(debugger.stops.size() == 3);
	CHECK(debugger.stops[0].wires[0] == Variant(4));
	CHECK(debugger.stops[1].wires[0] == Variant(5));
	CHECK(debugger.stops[2].wires[0] == Variant(6));
	CHECK(entity->get("total") == Variant(6));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A wire read leaves the random generator where it was") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.add(1, "entry");
	graph.add_named(2, "set", "total");
	graph.add(3, "pick_random");
	graph.set(3, "items", "[4, 5, 6]");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, VALUE);
	FakeDebugger debugger;
	debugger.stop_at(graph, "go", 2);
	debugger.probes.push_back(Vector3i(0, 2, VALUE));

	Entity *entity = attach(graph.script);
	entity->call("go");

	REQUIRE(debugger.stops.size() == 1);
	const int64_t picked = debugger.stops[0].wires[0];
	CHECK(picked >= 4);
	CHECK(picked <= 6);
	CHECK(!debugger.stops[0].random_moved);
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A wire read that fails reports nothing and does not fail the call") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.add(1, "entry");
	graph.add_named(2, "set", "total");
	graph.set(2, "value", "1");
	graph.add_named(5, "set", "total");
	graph.add(6, "pick_random");
	graph.set(6, "items", "[]");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(6, OUT, 5, VALUE);
	FakeDebugger debugger;
	debugger.stop_at(graph, "go", 2);
	debugger.probes.push_back(Vector3i(0, 5, VALUE));

	Entity *entity = attach(graph.script);
	entity->call("go");

	REQUIRE(debugger.stops.size() == 1);
	CHECK(debugger.stops[0].wires[0].get_type() == Variant::NIL);
	CHECK(entity->get("total") == Variant(1));
	memdelete(entity);
}

} // namespace TestGraphDebug
