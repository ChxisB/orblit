/**************************************************************************/
/*  test_graph_functions.h                                                */

#pragma once

#include "../graph_step_catalogue.h"
#include "test_graph_script_run.h"

#include "core/object/object.h"
#include "scene/stage/entity.h"

namespace TestGraphFunctions {

using TestGraphScriptRun::attach;
using TestGraphScriptRun::Graph;

constexpr int RUN = 0;
constexpr int THEN = 0;
constexpr int OUT = 0;
constexpr int VALUE = 1;
constexpr int A = 0;
constexpr int B = 1;
// The data outputs of a start step come after "then", one per declared argument.
constexpr int FIRST_ARGUMENT = 1;
// The argument inputs of a call function step come after "run" and "target".
constexpr int CALL_TARGET = 1;
constexpr int CALL_ARGUMENT = 2;
constexpr int CALL_RESULT = 1;

String entry_text(const String &p_name, Variant::Type p_type) {
	return vformat("{\"name\": \"%s\", \"type\": %d}", p_name, int(p_type));
}

String list_text(const String &p_first, const String &p_second = String()) {
	return p_second.is_empty() ? "[" + p_first + "]" : "[" + p_first + ", " + p_second + "]";
}

// A function "sum" that takes two ints and gives their total.
void add_sum(Graph &p_graph) {
	p_graph.add(1, "entry");
	p_graph.set(1, "arguments", list_text(entry_text("a", Variant::INT), entry_text("b", Variant::INT)));
	p_graph.set(1, "returns", itos(Variant::INT));
	p_graph.add_operator(2, "+");
	p_graph.add(3, "return");
	p_graph.wire(1, THEN, 3, RUN);
	p_graph.wire(1, FIRST_ARGUMENT, 2, A);
	p_graph.wire(1, FIRST_ARGUMENT + 1, 2, B);
	p_graph.wire(2, OUT, 3, VALUE);
}

Callable::CallError call_error_of(Entity *p_entity, const StringName &p_method, const Variant &p_first, const Variant &p_second) {
	const Variant *args[2] = { &p_first, &p_second };
	Callable::CallError error;
	p_entity->callp(p_method, args, 2, error);
	return error;
}

void check_fails_with(Entity *p_entity, const StringName &p_method, const String &p_text) {
	{
		ErrorDetector errors;
		ERR_PRINT_OFF;
		p_entity->call(p_method);
		ERR_PRINT_ON;
		CHECK(errors.has_error);
	}
	CHECK(GraphScriptLanguage::get_singleton()->debug_get_error().contains(p_text));
}

TEST_CASE("[Stage][GraphScript] A function with typed arguments gives a result from them") {
	Graph graph("sum");
	add_sum(graph);

	Entity *entity = attach(graph.script);
	CHECK(entity->call("sum", 2, 3) == Variant(5));
	// A float goes into an int argument.
	CHECK(entity->call("sum", 2.0, 3) == Variant(5));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A call with the wrong arguments is refused") {
	Graph graph("sum");
	add_sum(graph);

	Entity *entity = attach(graph.script);
	const Variant text = "x";
	const Variant number = 3;
	Callable::CallError error = call_error_of(entity, "sum", text, number);
	CHECK(error.error == Callable::CallError::CALL_ERROR_INVALID_ARGUMENT);
	CHECK(error.argument == 0);
	CHECK(error.expected == Variant::INT);

	const Variant *one[1] = { &number };
	entity->callp("sum", one, 1, error);
	CHECK(error.error == Callable::CallError::CALL_ERROR_TOO_FEW_ARGUMENTS);
	CHECK(error.expected == 2);

	const Variant *three[3] = { &number, &number, &number };
	entity->callp("sum", three, 3, error);
	CHECK(error.error == Callable::CallError::CALL_ERROR_TOO_MANY_ARGUMENTS);
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A function gives the type it declares or fails") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.set(1, "returns", itos(Variant::INT));
	graph.add(2, "constant");
	graph.set(2, "value", "\"text\"");
	graph.add(3, "return");
	graph.wire(1, THEN, 3, RUN);
	graph.wire(2, OUT, 3, VALUE);

	Entity *entity = attach(graph.script);
	check_fails_with(entity, "go", "cannot return a String");
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A declared local starts as the default of its type and keeps its type") {
	Graph graph("read");
	graph.add(1, "entry");
	graph.set(1, "locals", list_text(entry_text("count", Variant::INT)));
	graph.add_named(2, "local", "count");
	graph.add(3, "return");
	graph.wire(1, THEN, 3, RUN);
	graph.wire(2, OUT, 3, VALUE);
	Graph write(graph.script, "write");
	write.add(1, "entry");
	write.set(1, "locals", list_text(entry_text("count", Variant::INT)));
	write.add_named(2, "set_local", "count");
	write.set(2, "value", "\"text\"");
	write.wire(1, THEN, 2, RUN);

	Entity *entity = attach(graph.script);
	CHECK(entity->call("read") == Variant(0));
	check_fails_with(entity, "write", "cannot store a String in the local 'count'");
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Call function passes arguments and reads the result") {
	Graph graph("go");
	Graph sum(graph.script, "sum");
	add_sum(sum);
	graph.add(1, "entry");
	graph.add(2, "call_function");
	graph.set(2, "function", "\"sum\"");
	CHECK(graph.script->set_step_pins("go", 2, 2) == OK);
	graph.add(3, "constant");
	graph.set(3, "value", "4");
	graph.set(2, "argument1", "5");
	graph.add(4, "return");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, CALL_ARGUMENT);
	graph.wire(2, THEN, 4, RUN);
	graph.wire(2, CALL_RESULT, 4, VALUE);

	CHECK(graph.script->get_error().is_empty());
	Entity *entity = attach(graph.script);
	CHECK(entity->call("go") == Variant(9));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Call function reaches a function of another script through its target") {
	Graph other("sum");
	add_sum(other);
	Graph graph("go");
	graph.variable("other", Variant::OBJECT);
	graph.add(1, "entry");
	graph.add(2, "call_function");
	graph.set(2, "function", "\"sum\"");
	CHECK(graph.script->set_step_pins("go", 2, 2) == OK);
	graph.set(2, "argument0", "6");
	graph.set(2, "argument1", "1");
	graph.add_named(3, "get", "other");
	graph.add(4, "return");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, CALL_TARGET);
	graph.wire(2, THEN, 4, RUN);
	graph.wire(2, CALL_RESULT, 4, VALUE);

	CHECK(graph.script->get_error().is_empty());
	Entity *caller = attach(graph.script);
	Entity *callee = attach(other.script);
	caller->set("other", callee);
	CHECK(caller->call("go") == Variant(7));
	memdelete(caller);
	memdelete(callee);
}

TEST_CASE("[Stage][GraphScript] Call function on a missing function reports it") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "call_function");
	graph.set(2, "function", "\"nothing\"");
	graph.wire(1, THEN, 2, RUN);

	Entity *entity = attach(graph.script);
	check_fails_with(entity, "go", "nothing");
	memdelete(entity);
}

TEST_CASE("[GraphScript] The method list gives the arguments and the return type") {
	Graph graph("sum");
	add_sum(graph);

	const MethodInfo info = graph.script->get_method_info("sum");
	CHECK(info.name == StringName("sum"));
	REQUIRE(info.arguments.size() == 2);
	CHECK(info.arguments[1].name == StringName("b"));
	CHECK(info.arguments[1].type == Variant::INT);
	CHECK(info.return_val.type == Variant::INT);

	List<MethodInfo> methods;
	graph.script->get_script_method_list(&methods);
	REQUIRE(methods.size() == 1);
	CHECK(methods.front()->get().arguments.size() == 2);
}

TEST_CASE("[GraphScript] A function with a bad argument list fails to compile") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.set(1, "arguments", list_text(entry_text("a", Variant::INT), entry_text("a", Variant::INT)));
	CHECK(graph.script->get_error().contains("'a' is declared twice"));
}

TEST_CASE("[Stage][GraphScript] A declared signal with arguments reaches a connected function") {
	Graph receiver("take");
	receiver.variable("total", Variant::INT);
	receiver.add(1, "entry");
	receiver.set(1, "arguments", list_text(entry_text("amount", Variant::INT)));
	receiver.add_named(2, "set", "total");
	receiver.wire(1, THEN, 2, RUN);
	receiver.wire(1, FIRST_ARGUMENT, 2, VALUE);

	Graph graph("go");
	CHECK(graph.script->add_signal("hit") == OK);
	CHECK(graph.script->set_signal_arguments("hit", list_text(entry_text("amount", Variant::INT))) == OK);
	graph.add(1, "entry");
	graph.add(2, "emit");
	graph.set(2, "signal", "\"hit\"");
	CHECK(graph.script->set_step_pins("go", 2, 1) == OK);
	graph.set(2, "argument0", "8");
	graph.wire(1, THEN, 2, RUN);

	List<MethodInfo> signals;
	graph.script->get_script_signal_list(&signals);
	REQUIRE(signals.size() == 1);
	REQUIRE(signals.front()->get().arguments.size() == 1);
	CHECK(signals.front()->get().arguments[0].name == StringName("amount"));
	CHECK(signals.front()->get().arguments[0].type == Variant::INT);

	Entity *emitter = attach(graph.script);
	Entity *listener = attach(receiver.script);
	CHECK(emitter->connect("hit", Callable(listener, "take")) == OK);
	emitter->call("go");
	CHECK(listener->get("total") == Variant(8));
	memdelete(emitter);
	memdelete(listener);
}

TEST_CASE("[GraphScript] Signal arguments are refused when malformed and kept in the file arrays") {
	Ref<GraphScript> script;
	script.instantiate();
	CHECK(script->add_signal("hit") == OK);
	CHECK(script->set_signal_arguments("missing", "[]") == ERR_DOES_NOT_EXIST);
	CHECK(script->set_signal_arguments("hit", "not a value {") == ERR_PARSE_ERROR);
	CHECK(script->set_signal_arguments("hit", "[{\"name\": \"1x\", \"type\": 2}]") == ERR_INVALID_DATA);
	CHECK(script->get_signals() == Array({ String("hit") }));
	CHECK(script->set_signal_arguments("hit", list_text(entry_text("amount", Variant::INT))) == OK);

	Ref<GraphScript> loaded;
	loaded.instantiate();
	loaded->set_signals(script->get_signals());
	CHECK(loaded->get_signals() == script->get_signals());
	List<MethodInfo> signals;
	loaded->get_script_signal_list(&signals);
	REQUIRE(signals.size() == 1);
	CHECK(signals.front()->get().arguments.size() == 1);
}

TEST_CASE("[Stage][GraphScript] A signal received function runs when its owner emits the signal") {
	Graph graph("react");
	graph.variable("total", Variant::INT);
	CHECK(graph.script->add_signal("hit") == OK);
	CHECK(graph.script->set_signal_arguments("hit", list_text(entry_text("amount", Variant::INT))) == OK);
	graph.add(1, "signal_received");
	graph.set(1, "signal", "\"hit\"");
	graph.set(1, "arguments", list_text(entry_text("amount", Variant::INT)));
	graph.add_named(2, "set", "total");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(1, FIRST_ARGUMENT, 2, VALUE);
	CHECK(graph.script->get_error().is_empty());

	Entity *entity = attach(graph.script);
	Stage::get_singleton()->get_root()->add_child(entity);
	entity->emit_signal("hit", 3);
	CHECK(entity->get("total") == Variant(3));
	memdelete(entity);
}

TEST_CASE("[GraphScript] A function has one start step and signal received needs a signal") {
	Graph graph("go");
	graph.add(1, "signal_received");
	CHECK(graph.script->get_error().contains("signal"));
}

TEST_CASE("[GraphScript] A function marked remote lists its settings with the defaults filled in") {
	Graph graph("shout");
	graph.add(1, "entry");
	graph.set(1, "rpc", "{\"call_local\": true, \"channel\": 2}");
	Graph quiet(graph.script, "quiet");
	quiet.add(1, "entry");

	const Dictionary config = graph.script->get_rpc_config();
	CHECK(config.size() == 1);
	REQUIRE(config.has("shout"));
	const Dictionary shout = config["shout"];
	CHECK(shout["rpc_mode"] == Variant(2));
	CHECK(shout["transfer_mode"] == Variant(2));
	CHECK(shout["call_local"] == Variant(true));
	CHECK(shout["channel"] == Variant(2));
}

TEST_CASE("[GraphScript] A remote function with an unknown key or a value out of range fails to compile") {
	Graph graph("shout");
	graph.add(1, "entry");
	graph.set(1, "rpc", "{\"loud\": true}");
	CHECK(graph.script->get_error().contains("'loud'"));
	graph.set(1, "rpc", "{\"rpc_mode\": 7}");
	CHECK(graph.script->get_error().contains("'rpc_mode'"));
	graph.set(1, "rpc", "{\"call_local\": 1}");
	CHECK(graph.script->get_error().contains("'call_local'"));
	graph.set(1, "rpc", "{\"channel\": -1}");
	CHECK(graph.script->get_error().contains("'channel'"));
	graph.set(1, "rpc", "{}");
	CHECK(graph.script->get_error().is_empty());
	CHECK(graph.script->get_rpc_config().operator Dictionary().size() == 1);
}

#ifdef DEBUG_ENABLED
TEST_CASE("[GraphScript] Overrides lists the virtual functions of a class and the ones above it") {
	const Vector<GraphStepEntry> entries = GraphStepCatalogue::find_overrides("Entity");
	bool found = false;
	for (const GraphStepEntry &entry : entries) {
		if (entry.label == "_frame") {
			found = true;
			CHECK(entry.kind == StringName("event"));
			CHECK(entry.group == "Entity");
			const Array arguments = entry.parameters["arguments"];
			CHECK(arguments.size() == 1);
		}
	}
	CHECK(found);
}
#endif // DEBUG_ENABLED

} // namespace TestGraphFunctions
