/**************************************************************************/
/*  test_graph_latent.h                                                   */

#pragma once

#include "../graph_call.h"
#include "test_graph_functions.h"

#include "tests/signal_watcher.h"

namespace TestGraphLatent {

using TestGraphFunctions::CALL_RESULT;
using TestGraphFunctions::entry_text;
using TestGraphFunctions::FIRST_ARGUMENT;
using TestGraphFunctions::list_text;
using TestGraphScriptRun::A;
using TestGraphScriptRun::attach;
using TestGraphScriptRun::B;
using TestGraphScriptRun::BODY;
using TestGraphScriptRun::BUDGET_SETTING;
using TestGraphScriptRun::DONE;
using TestGraphScriptRun::Graph;
using TestGraphScriptRun::INDEX;
using TestGraphScriptRun::OUT;
using TestGraphScriptRun::RUN;
using TestGraphScriptRun::THEN;
using TestGraphScriptRun::VALUE;

constexpr int SECONDS = 1; // The input of Wait after "run".
constexpr int ARGUMENTS = 1; // The output of Await signal after "then".

// The stage runs its timers from the delta it is given, so a test moves the clock by hand.
void advance(double p_seconds) {
	Stage::get_singleton()->process(p_seconds);
}

Entity *enter_stage(const Ref<GraphScript> &p_script) {
	Entity *entity = attach(p_script);
	Stage::get_singleton()->get_root()->add_child(entity);
	return entity;
}

// What the signal watcher records for one emit with one argument.
Array one_emit(const Variant &p_argument) {
	Array arguments;
	arguments.push_back(p_argument);
	Array emits;
	emits.push_back(arguments);
	return emits;
}

// A function "go" that waits and then sets the variable "done" to 1.
Graph waiting_graph(const String &p_seconds) {
	Graph graph("go");
	graph.variable("done", Variant::INT);
	graph.add(1, "entry");
	graph.add(2, "wait");
	graph.set(2, "seconds", p_seconds);
	graph.add_named(3, "set", "done");
	graph.set(3, "value", "1");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 3, RUN);
	return graph;
}

// A function that gives back a number: waits first when p_seconds is not empty.
void add_giver(Graph &p_graph, const String &p_seconds, int p_number) {
	p_graph.add(1, "entry");
	p_graph.set(1, "returns", itos(Variant::INT));
	p_graph.add(3, "return");
	p_graph.set(3, "value", itos(p_number));
	if (p_seconds.is_empty()) {
		p_graph.wire(1, THEN, 3, RUN);
		return;
	}
	p_graph.add(2, "wait");
	p_graph.set(2, "seconds", p_seconds);
	p_graph.wire(1, THEN, 2, RUN);
	p_graph.wire(2, THEN, 3, RUN);
}

// A function "go" that calls "giver" and sets "done" to what it gives back.
void add_caller(const Ref<GraphScript> &p_script, bool p_wait) {
	Graph caller(p_script, "go");
	caller.add(1, "entry");
	caller.add(2, "call_function");
	caller.set(2, "function", "\"giver\"");
	if (p_wait) {
		caller.set(2, "wait", "true");
	}
	caller.add_named(3, "set", "done");
	caller.wire(1, THEN, 2, RUN);
	caller.wire(2, THEN, 3, RUN);
	caller.wire(2, CALL_RESULT, 3, VALUE);
}

TEST_CASE("[Stage][GraphScript] A Wait holds the rest of the function until its time has passed") {
	Graph graph = waiting_graph("0.5");
	Entity *entity = enter_stage(graph.script);
	entity->call("go");
	CHECK(entity->get("done") == Variant(0));
	advance(0.3);
	CHECK(entity->get("done") == Variant(0));
	advance(0.3);
	CHECK(entity->get("done") == Variant(1));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A function that waits gives the caller a frame that ends with the time") {
	Graph graph = waiting_graph("0.5");
	Entity *entity = enter_stage(graph.script);
	Ref<GraphCall> frame = entity->call("go");
	REQUIRE(frame.is_valid());
	CHECK(frame->is_waiting());
	advance(0.6);
	CHECK_FALSE(frame->is_waiting());
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A function that never waits gives back its value and no frame") {
	Graph graph("giver");
	add_giver(graph, String(), 7);
	Entity *entity = enter_stage(graph.script);
	CHECK(entity->call("giver") == Variant(7));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Await signal resumes on the emit and not before") {
	Graph graph("go");
	graph.variable("done", Variant::INT);
	CHECK(graph.script->add_signal("ping") == OK);
	graph.add(1, "entry");
	graph.add(2, "await_signal");
	graph.set(2, "signal", "\"ping\"");
	graph.add_named(3, "set", "done");
	graph.set(3, "value", "1");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 3, RUN);

	Entity *entity = enter_stage(graph.script);
	entity->call("go");
	advance(5.0);
	CHECK(entity->get("done") == Variant(0));
	entity->emit_signal("ping");
	CHECK(entity->get("done") == Variant(1));
	// The wait was one shot. A second emit finds nothing to resume.
	entity->set("done", 0);
	entity->emit_signal("ping");
	CHECK(entity->get("done") == Variant(0));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Await signal hands the signal arguments on as an array") {
	Graph graph("go");
	graph.variable("heard", Variant::ARRAY);
	CHECK(graph.script->add_signal("ping") == OK);
	CHECK(graph.script->set_signal_arguments("ping", list_text(entry_text("n", Variant::INT))) == OK);
	graph.add(1, "entry");
	graph.add(2, "await_signal");
	graph.set(2, "signal", "\"ping\"");
	graph.add_named(3, "set", "heard");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 3, RUN);
	graph.wire(2, ARGUMENTS, 3, VALUE);

	Entity *entity = enter_stage(graph.script);
	entity->call("go");
	entity->emit_signal("ping", 7);
	Array expected;
	expected.push_back(7);
	CHECK(entity->get("heard") == Variant(expected));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Two waiting calls resume in the order their times pass") {
	Graph graph("go");
	graph.variable("order", Variant::INT);
	graph.add(1, "entry");
	graph.set(1, "arguments", list_text(entry_text("seconds", Variant::FLOAT), entry_text("tag", Variant::INT)));
	graph.add(2, "wait");
	graph.add_named(3, "set", "order");
	graph.add_operator(4, "+");
	graph.add_operator(5, "*");
	graph.add_named(6, "get", "order");
	graph.set(5, "b", "10");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(1, FIRST_ARGUMENT, 2, SECONDS);
	graph.wire(2, THEN, 3, RUN);
	graph.wire(4, OUT, 3, VALUE);
	graph.wire(5, OUT, 4, A);
	graph.wire(1, FIRST_ARGUMENT + 1, 4, B);
	graph.wire(6, OUT, 5, A);

	Entity *entity = enter_stage(graph.script);
	entity->call("go", 0.5, 1);
	entity->call("go", 0.2, 2);
	advance(0.3);
	CHECK(entity->get("order") == Variant(2));
	advance(0.3);
	// The call with the longer wait added its tag last.
	CHECK(entity->get("order") == Variant(21));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] An entity freed while its call waits does not resume it") {
	Graph graph = waiting_graph("0.5");
	Entity *entity = enter_stage(graph.script);
	Ref<GraphCall> frame = entity->call("go");
	REQUIRE(frame.is_valid());
	SIGNAL_WATCH(frame.ptr(), "completed");
	memdelete(entity);
	CHECK_FALSE(frame->is_waiting());
	{
		ErrorDetector errors;
		advance(1.0);
		CHECK_FALSE(errors.has_error);
	}
	SIGNAL_CHECK_FALSE("completed");
	SIGNAL_UNWATCH(frame.ptr(), "completed");
}

TEST_CASE("[Stage][GraphScript] An entity freed while its call waits for a signal does not resume it") {
	Graph graph("go");
	CHECK(graph.script->add_signal("ping") == OK);
	graph.add(1, "entry");
	graph.add(2, "await_signal");
	graph.set(2, "signal", "\"ping\"");
	graph.wire(1, THEN, 2, RUN);

	Entity *entity = enter_stage(graph.script);
	Ref<GraphCall> frame = entity->call("go");
	REQUIRE(frame.is_valid());
	memdelete(entity);
	CHECK_FALSE(frame->is_waiting());
}

TEST_CASE("[Stage][GraphScript] A reload while a call waits logs it and drops the call") {
	Graph graph = waiting_graph("0.5");
	Entity *entity = enter_stage(graph.script);
	Ref<GraphCall> frame = entity->call("go");
	REQUIRE(frame.is_valid());
	{
		ErrorDetector errors;
		ERR_PRINT_OFF;
		CHECK(graph.script->reload(true) == OK);
		ERR_PRINT_ON;
		CHECK(errors.has_error);
	}
	CHECK(GraphScriptLanguage::get_singleton()->debug_get_error().contains("waiting"));
	CHECK_FALSE(frame->is_waiting());
	advance(1.0);
	CHECK(entity->get("done") == Variant(0));

	// The reloaded script waits and runs on as before.
	entity->call("go");
	advance(1.0);
	CHECK(entity->get("done") == Variant(1));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A loop of 1000 Waits takes one round for each time the clock moves") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.add(1, "entry");
	graph.add(2, "count_loop");
	graph.set(2, "from", "0");
	graph.set(2, "to", "999");
	graph.add(3, "wait");
	graph.set(3, "seconds", "0.0");
	graph.add_running_total(10, "total", 2, INDEX);
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, BODY, 3, RUN);
	graph.wire(3, THEN, 10, RUN);

	Entity *entity = enter_stage(graph.script);
	Ref<GraphCall> frame = entity->call("go");
	REQUIRE(frame.is_valid());
	// A timer made while the stage goes through its timers waits for the next pass, so one pass
	// is one round and a round never starts inside another.
	for (int pass = 0; pass < 3; pass++) {
		advance(0.0);
	}
	CHECK(entity->get("total") == Variant(0 + 1 + 2));
	for (int pass = 3; pass < 1000; pass++) {
		advance(0.0);
	}
	CHECK(entity->get("total") == Variant(499500));
	CHECK_FALSE(frame->is_waiting());
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A call to a function that waits can wait for it and take its result") {
	Graph graph("giver");
	graph.variable("done", Variant::INT);
	add_giver(graph, "0.5", 42);
	add_caller(graph.script, true);

	Entity *entity = enter_stage(graph.script);
	entity->call("go");
	CHECK(entity->get("done") == Variant(0));
	advance(0.3);
	CHECK(entity->get("done") == Variant(0));
	advance(0.3);
	CHECK(entity->get("done") == Variant(42));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A frame says it is complete and gives the result of its function") {
	Graph graph("giver");
	add_giver(graph, "0.5", 42);

	Entity *entity = enter_stage(graph.script);
	Ref<GraphCall> frame = entity->call("giver");
	REQUIRE(frame.is_valid());
	SIGNAL_WATCH(frame.ptr(), "completed");
	advance(0.6);
	SIGNAL_CHECK("completed", one_emit(42));
	SIGNAL_UNWATCH(frame.ptr(), "completed");
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A call flagged to wait carries on at once when the function does not wait") {
	Graph graph("giver");
	graph.variable("done", Variant::INT);
	add_giver(graph, String(), 7);
	add_caller(graph.script, true);

	Entity *entity = enter_stage(graph.script);
	Variant outcome = entity->call("go");
	CHECK(outcome.get_type() == Variant::NIL);
	CHECK(entity->get("done") == Variant(7));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A call not flagged to wait does not wait for a function that waits") {
	Graph graph("giver");
	graph.variable("done", Variant::INT);
	graph.variable("after", Variant::INT);
	add_giver(graph, "0.5", 42);

	Graph caller(graph.script, "go");
	caller.add(1, "entry");
	caller.add(2, "call_function");
	caller.set(2, "function", "\"giver\"");
	caller.add_named(3, "set", "after");
	caller.set(3, "value", "1");
	caller.wire(1, THEN, 2, RUN);
	caller.wire(2, THEN, 3, RUN);

	Entity *entity = enter_stage(graph.script);
	Variant outcome = entity->call("go");
	CHECK(outcome.get_type() == Variant::NIL);
	CHECK(entity->get("after") == Variant(1));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] The step budget counts again from each resume") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.add(1, "entry");
	graph.add(2, "count_loop");
	graph.set(2, "from", "0");
	graph.set(2, "to", "14");
	graph.add(3, "wait");
	graph.set(3, "seconds", "0.5");
	graph.add(4, "count_loop");
	graph.set(4, "from", "0");
	graph.set(4, "to", "14");
	graph.add_running_total(10, "total", 2, INDEX);
	graph.add_running_total(20, "total", 4, INDEX);
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, BODY, 10, RUN);
	graph.wire(2, DONE, 3, RUN);
	graph.wire(3, THEN, 4, RUN);
	graph.wire(4, BODY, 20, RUN);

	// Each half takes about 35 steps and the two together take about 70.
	ProjectSettings::get_singleton()->set_setting(BUDGET_SETTING, 50);
	Entity *entity = enter_stage(graph.script);
	{
		ErrorDetector errors;
		entity->call("go");
		advance(0.6);
		CHECK_FALSE(errors.has_error);
	}
	CHECK(entity->get("total") == Variant(210));
	ProjectSettings::get_singleton()->set_setting(BUDGET_SETTING, 1000000);
	memdelete(entity);
}

TEST_CASE("[GraphScript] Await signal needs a signal name and a flagged call needs a true or false") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "await_signal");
	graph.wire(1, THEN, 2, RUN);
	CHECK(graph.script->get_error().contains("signal"));

	Graph caller("go");
	caller.add(1, "entry");
	caller.add(2, "call_function");
	caller.set(2, "function", "\"giver\"");
	caller.set(2, "wait", "\"yes\"");
	caller.wire(1, THEN, 2, RUN);
	CHECK(caller.script->get_error().contains("wait"));
}

} // namespace TestGraphLatent
