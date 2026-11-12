/**************************************************************************/
/*  test_graph_script_run.h                                               */

#pragma once

#include "../graph_script.h"
#include "../graph_script_language.h"

#include "core/config/project_settings.h"
#include "scene/stage/entity.h"
#include "scene/stage/stage.h"
#include "scene/stage/window.h"
#include "tests/test_macros.h"
#include "tests/test_tools.h"

namespace TestGraphScriptRun {

// Port numbers follow the order of the rows in graph_step_kinds.cpp.
constexpr int RUN = 0;
constexpr int THEN = 0;
constexpr int ELSE = 1;
constexpr int BODY = 0;
constexpr int DONE = 1;
constexpr int FIRST = 0;
constexpr int SECOND = 1;
constexpr int VALUE = 1; // The value input of Set and Return.
constexpr int OUT = 0; // The only output of Get, Local, Constant and Operator.
constexpr int CONDITION = 1;
constexpr int INDEX = 2; // The index output of a count loop.
constexpr int ITEM = 2; // The item output of Each.
constexpr int A = 0;
constexpr int B = 1;

constexpr const char *BUDGET_SETTING = "graph_script/runtime/step_budget";

// One function of a new script, with short helpers so a test reads as a list of steps.
struct Graph {
	Ref<GraphScript> script;
	String function;

	explicit Graph(const String &p_function) :
			function(p_function) {
		script.instantiate();
		CHECK(script->add_function(function) == OK);
	}

	// Adds another function to a script a test has made.
	Graph(const Ref<GraphScript> &p_script, const String &p_function) :
			script(p_script), function(p_function) {
		CHECK(script->add_function(function) == OK);
	}

	void variable(const String &p_name, Variant::Type p_type, const String &p_default_text = String()) {
		CHECK(script->add_variable(p_name, p_type) == OK);
		if (!p_default_text.is_empty()) {
			CHECK(script->set_variable_default(p_name, p_default_text) == OK);
		}
	}

	void add(int p_id, const String &p_kind) {
		CHECK(script->add_step(function, p_id, p_kind) == OK);
	}

	void set(int p_id, const String &p_parameter, const String &p_text) {
		CHECK(script->set_step_parameter(function, p_id, p_parameter, p_text) == OK);
	}

	// Adds a step that names a variable or a local.
	void add_named(int p_id, const String &p_kind, const String &p_name) {
		add(p_id, p_kind);
		set(p_id, "name", "\"" + p_name + "\"");
	}

	void add_operator(int p_id, const String &p_symbol) {
		add(p_id, "operator");
		set(p_id, "operator", "\"" + p_symbol + "\"");
	}

	void wire(int p_from, int p_from_port, int p_to, int p_to_port) {
		CHECK(script->connect_steps(function, p_from, p_from_port, p_to, p_to_port) == OK);
	}

	// Sets a variable from another variable plus the index of a loop: step ids p_id to p_id + 2.
	void add_running_total(int p_id, const String &p_total, int p_index_step, int p_index_port) {
		add_named(p_id, "set", p_total);
		add_operator(p_id + 1, "+");
		add_named(p_id + 2, "get", p_total);
		wire(p_id + 1, OUT, p_id, VALUE);
		wire(p_id + 2, OUT, p_id + 1, A);
		wire(p_index_step, p_index_port, p_id + 1, B);
	}
};

// The caller frees it.
Entity *attach(const Ref<GraphScript> &p_script) {
	Entity *entity = memnew(Entity);
	entity->set_script(p_script);
	return entity;
}

TEST_CASE("[Stage][GraphScript] An entity runs the start event and sets a variable to 2 plus 3") {
	Graph graph("_start");
	graph.variable("total", Variant::INT);
	graph.add(1, "event");
	graph.add_named(2, "set", "total");
	graph.add_operator(3, "+");
	graph.add(4, "constant");
	graph.set(4, "value", "2");
	graph.set(3, "b", "3");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, VALUE);
	graph.wire(4, OUT, 3, A);

	Entity *entity = attach(graph.script);
	CHECK(entity->get("total") == Variant(0));
	Stage::get_singleton()->get_root()->add_child(entity);
	CHECK(entity->get("total") == Variant(5));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Branch takes the path its condition chooses") {
	Graph graph("go");
	graph.variable("flag", Variant::BOOL);
	graph.variable("path", Variant::INT);
	graph.add(1, "entry");
	graph.add(2, "branch");
	graph.add_named(3, "get", "flag");
	graph.add_named(4, "set", "path");
	graph.set(4, "value", "1");
	graph.add_named(5, "set", "path");
	graph.set(5, "value", "2");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, CONDITION);
	graph.wire(2, THEN, 4, RUN);
	graph.wire(2, ELSE, 5, RUN);

	Entity *entity = attach(graph.script);
	entity->set("flag", true);
	entity->call("go");
	CHECK(entity->get("path") == Variant(1));
	entity->set("flag", false);
	entity->call("go");
	CHECK(entity->get("path") == Variant(2));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Sequence runs its outputs in order") {
	Graph graph("go");
	graph.variable("trace", Variant::STRING);
	graph.add(1, "entry");
	graph.add(2, "sequence");
	graph.wire(1, THEN, 2, RUN);
	// Each output appends one letter to the trace.
	const char *letters[] = { "A", "B" };
	for (int i = 0; i < 2; i++) {
		const int base = 10 + i * 10;
		graph.add_named(base, "set", "trace");
		graph.add_operator(base + 1, "+");
		graph.add_named(base + 2, "get", "trace");
		graph.set(base + 1, "b", String("\"") + letters[i] + "\"");
		graph.wire(base + 1, OUT, base, VALUE);
		graph.wire(base + 2, OUT, base + 1, A);
		graph.wire(2, i, base, RUN);
	}

	Entity *entity = attach(graph.script);
	entity->call("go");
	CHECK(entity->get("trace") == Variant("AB"));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A count loop from 1 to 10 sums to 55") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.add(1, "entry");
	graph.add(2, "count_loop");
	graph.set(2, "from", "1");
	graph.set(2, "to", "10");
	graph.wire(1, THEN, 2, RUN);
	graph.add_running_total(3, "total", 2, INDEX);
	graph.wire(2, BODY, 3, RUN);

	Entity *entity = attach(graph.script);
	entity->call("go");
	CHECK(entity->get("total") == Variant(55));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Each walks an array and gives its items") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.variable("items", Variant::ARRAY, "[4, 5, 6]");
	graph.add(1, "entry");
	graph.add(2, "each");
	graph.add_named(3, "get", "items");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, 1);
	graph.add_running_total(4, "total", 2, ITEM);
	graph.wire(2, BODY, 4, RUN);

	Entity *entity = attach(graph.script);
	entity->call("go");
	CHECK(entity->get("total") == Variant(15));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Break leaves the loop and runs its done output") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.variable("finished", Variant::BOOL);
	graph.add(1, "entry");
	graph.add(2, "count_loop");
	graph.set(2, "from", "1");
	graph.set(2, "to", "10");
	graph.wire(1, THEN, 2, RUN);
	graph.add(3, "branch");
	graph.add_operator(4, "==");
	graph.set(4, "b", "3");
	graph.add(5, "break");
	graph.wire(2, BODY, 3, RUN);
	graph.wire(2, INDEX, 4, A);
	graph.wire(4, OUT, 3, CONDITION);
	graph.wire(3, THEN, 5, RUN);
	graph.add_running_total(6, "total", 2, INDEX);
	graph.wire(3, ELSE, 6, RUN);
	graph.add_named(9, "set", "finished");
	graph.set(9, "value", "true");
	graph.wire(2, DONE, 9, RUN);

	Entity *entity = attach(graph.script);
	entity->call("go");
	CHECK(entity->get("total") == Variant(3));
	CHECK(entity->get("finished") == Variant(true));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Return ends the call with its value even inside a sequence") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.add(1, "entry");
	graph.add(2, "sequence");
	graph.add(3, "return");
	graph.set(3, "value", "9");
	graph.add_named(4, "set", "total");
	graph.set(4, "value", "1");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, FIRST, 3, RUN);
	graph.wire(2, SECOND, 4, RUN);

	Entity *entity = attach(graph.script);
	CHECK(entity->call("go") == Variant(9));
	CHECK(entity->get("total") == Variant(0));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A local lives for one call") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.add(1, "entry");
	graph.add_named(2, "set_local", "x");
	graph.set(2, "value", "7");
	graph.add_named(3, "set", "total");
	graph.add_named(4, "local", "x");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 3, RUN);
	graph.wire(4, OUT, 3, VALUE);

	Entity *entity = attach(graph.script);
	entity->call("go");
	CHECK(entity->get("total") == Variant(7));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A While that never ends stops at the step budget") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "while");
	graph.set(2, "condition", "true");
	graph.wire(1, THEN, 2, RUN);

	ProjectSettings::get_singleton()->set_setting(BUDGET_SETTING, 1000);
	Entity *entity = attach(graph.script);
	{
		ErrorDetector errors;
		ERR_PRINT_OFF;
		entity->call("go");
		ERR_PRINT_ON;
		CHECK(errors.has_error);
	}
	const String error = GraphScriptLanguage::get_singleton()->debug_get_error();
	CHECK(error.contains("'go'"));
	CHECK(error.contains("step 2"));
	CHECK(error.contains("1000"));
	ProjectSettings::get_singleton()->set_setting(BUDGET_SETTING, 1000000);
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A missing input stops the compile and names the step") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "branch");
	graph.wire(1, THEN, 2, RUN);

	CHECK(graph.script->reload() == ERR_COMPILATION_FAILED);
	CHECK_FALSE(graph.script->is_script_valid());
	CHECK(graph.script->get_error().contains("step 2"));
	CHECK(graph.script->get_error().contains("condition"));

	// A function that does not compile logs its error and does nothing.
	Entity *entity = attach(graph.script);
	{
		ErrorDetector errors;
		ERR_PRINT_OFF;
		entity->call("go");
		ERR_PRINT_ON;
		CHECK(errors.has_error);
	}
	memdelete(entity);

	graph.set(2, "condition", "true");
	CHECK(graph.script->reload() == OK);
	CHECK(graph.script->is_script_valid());
	CHECK(graph.script->get_error().is_empty());
}

TEST_CASE("[Stage][GraphScript] The compile refuses these graphs") {
	struct Case {
		const char *label;
		const char *expected;
	};
	const Case cases[] = {
		{ "two start steps", "start" },
		{ "a flow cycle outside a loop", "cycle" },
		{ "a data cycle", "cycle" },
		{ "an unknown operator", "operator" },
		{ "a variable step with no name", "name" },
	};
	for (const Case &test : cases) {
		CAPTURE(test.label);
		Graph graph("go");
		graph.add(1, "entry");
		const String label = test.label;
		if (label == "two start steps") {
			graph.add(2, "event");
		} else if (label == "a flow cycle outside a loop") {
			graph.add(2, "sequence");
			graph.add(3, "sequence");
			graph.wire(1, THEN, 2, RUN);
			graph.wire(2, SECOND, 3, RUN);
			graph.wire(3, SECOND, 2, RUN);
		} else if (label == "a data cycle") {
			graph.add_operator(2, "+");
			graph.add_operator(3, "+");
			graph.set(2, "b", "1");
			graph.set(3, "b", "1");
			graph.wire(2, OUT, 3, A);
			graph.wire(3, OUT, 2, A);
		} else if (label == "an unknown operator") {
			graph.add_operator(2, "?");
			graph.set(2, "a", "1");
			graph.set(2, "b", "1");
		} else {
			graph.add(2, "get");
		}
		CHECK(graph.script->reload() == ERR_COMPILATION_FAILED);
		CHECK(graph.script->get_error().contains(test.expected));
	}
}

TEST_CASE("[Stage][GraphScript] A flow cycle through a loop compiles") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "while");
	graph.set(2, "condition", "true");
	graph.add(3, "sequence");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, BODY, 3, RUN);
	graph.wire(3, FIRST, 2, RUN);

	CHECK(graph.script->reload() == OK);
}

TEST_CASE("[Stage][GraphScript] A step of an unknown kind fails the compile and names the step") {
	Graph graph("go");
	graph.add(1, "entry");
	Array functions = graph.script->get_functions();
	Dictionary function = functions[0];
	Array steps = function["steps"];
	Dictionary step = steps[0];
	step["kind"] = "future_kind";
	graph.script->set_functions(functions);

	CHECK(graph.script->reload() == ERR_COMPILATION_FAILED);
	CHECK(graph.script->get_error().contains("step 1"));
	CHECK(graph.script->get_error().contains("future_kind"));
}

TEST_CASE("[Stage][GraphScript] The instance answers calls and holds its variables") {
	Graph graph("go");
	graph.variable("total", Variant::INT, "3");
	graph.add(1, "entry");

	Entity *entity = attach(graph.script);
	REQUIRE(entity->get_script_instance() != nullptr);
	CHECK(entity->has_method("go"));
	CHECK_FALSE(entity->has_method("other"));
	CHECK(entity->get("total") == Variant(3));

	Callable::CallError error;
	entity->get_script_instance()->callp("other", nullptr, 0, error);
	CHECK(error.error == Callable::CallError::CALL_ERROR_INVALID_METHOD);

	// A float for an int variable converts. A string does not.
	entity->set("total", 4.0);
	CHECK(entity->get("total") == Variant(4));
	ERR_PRINT_OFF;
	entity->set("total", "text");
	ERR_PRINT_ON;
	CHECK(entity->get("total") == Variant(4));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Removing the script frees the instance") {
	Graph graph("go");
	graph.add(1, "entry");

	Entity *entity = attach(graph.script);
	CHECK(graph.script->get_reference_count() > 1);
	entity->set_script(Variant());
	CHECK(entity->get_script_instance() == nullptr);
	CHECK(graph.script->get_reference_count() == 1);

	entity->set_script(graph.script);
	memdelete(entity);
	CHECK(graph.script->get_reference_count() == 1);
}

} // namespace TestGraphScriptRun
