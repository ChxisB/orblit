/**************************************************************************/
/*  test_graph_steps.h                                                    */

#pragma once

#include "../graph_script_instance.h"
#include "test_graph_script_run.h"

#include "core/config/project_settings.h"
#include "core/input/input.h"
#include "core/input/input_map.h"
#include "core/io/resource_loader.h"
#include "core/math/random_number_generator.h"
#include "core/os/keyboard.h"
#include "core/string/print_string.h"
#include "scene/resources/prefab.h"

namespace TestGraphSteps {

using TestGraphScriptRun::attach;
using TestGraphScriptRun::Graph;

// Port numbers follow the port lists in graph_step_kinds.cpp.
constexpr int RUN = 0;
constexpr int THEN = 0;
constexpr int ELSE = 1;
constexpr int VALUE = 1;
constexpr int DEFAULT = 0;
constexpr int CASE_0 = 1;
constexpr int CASE_1 = 2;
constexpr int OUT_PORT = 0;
constexpr int VALID = 1;
constexpr int PART_1 = 1;
constexpr int PREFAB = 1;
constexpr int MADE = 1;

// Adds a Return step that gives the output of another step.
void add_return(Graph &p_graph, int p_id, int p_from, int p_port) {
	p_graph.add(p_id, "return");
	p_graph.wire(p_from, p_port, p_id, VALUE);
}

// Adds a Return step that gives a fixed value.
void add_return_value(Graph &p_graph, int p_id, const String &p_text) {
	p_graph.add(p_id, "return");
	p_graph.set(p_id, "value", p_text);
}

void add_increment(Graph &p_graph, int p_id, const String &p_variable) {
	p_graph.add_named(p_id, "set", p_variable);
	p_graph.add_operator(p_id + 1, "+");
	p_graph.add_named(p_id + 2, "get", p_variable);
	p_graph.set(p_id + 1, "b", "1");
	p_graph.wire(p_id + 1, OUT_PORT, p_id, VALUE);
	p_graph.wire(p_id + 2, OUT_PORT, p_id + 1, 0);
}

void seed(Entity *p_entity, uint64_t p_seed) {
	Ref<RandomNumberGenerator> random;
	random.instantiate();
	random->set_seed(p_seed);
	static_cast<GraphScriptInstance *>(p_entity->get_script_instance())->set_random(random);
}

TEST_CASE("[Stage][GraphScript] Chance takes both ways and repeats with the same seed") {
	Graph graph("go");
	graph.variable("hits", Variant::INT);
	graph.variable("misses", Variant::INT);
	graph.add(1, "entry");
	graph.add(2, "chance");
	graph.set(2, "chance", "0.5");
	graph.wire(1, THEN, 2, RUN);
	add_increment(graph, 10, "hits");
	add_increment(graph, 20, "misses");
	graph.wire(2, THEN, 10, RUN);
	graph.wire(2, ELSE, 20, RUN);

	Entity *entity = attach(graph.script);
	int64_t first_hits = 0;
	for (int round = 0; round < 2; round++) {
		entity->set("hits", 0);
		entity->set("misses", 0);
		seed(entity, 7);
		for (int i = 0; i < 40; i++) {
			entity->call("go");
		}
		const int64_t hits = entity->get("hits");
		CHECK(hits > 0);
		CHECK(int64_t(entity->get("misses")) > 0);
		CHECK(hits + int64_t(entity->get("misses")) == 40);
		if (round == 0) {
			first_hits = hits;
		} else {
			CHECK(hits == first_hits);
		}
	}

	graph.set(2, "chance", "1.0");
	entity->set("hits", 0);
	for (int i = 0; i < 10; i++) {
		entity->call("go");
	}
	CHECK(entity->get("hits") == Variant(10));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Pick random gives an item and fails on an empty array") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "pick_random");
	graph.set(2, "items", "[4, 5, 6]");
	add_return(graph, 3, 2, OUT_PORT);
	graph.wire(1, THEN, 3, RUN);

	Entity *entity = attach(graph.script);
	seed(entity, 3);
	bool seen[3] = {};
	for (int i = 0; i < 30; i++) {
		const int64_t picked = entity->call("go");
		REQUIRE(picked >= 4);
		REQUIRE(picked <= 6);
		seen[picked - 4] = true;
	}
	CHECK(seen[0]);
	CHECK(seen[1]);
	CHECK(seen[2]);

	graph.set(2, "items", "[]");
	{
		ERR_PRINT_OFF;
		entity->call("go");
		ERR_PRINT_ON;
	}
	CHECK(GraphScriptLanguage::get_singleton()->debug_get_error().contains("step 2"));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Select gives the value its condition chooses") {
	Graph graph("go");
	graph.variable("flag", Variant::BOOL);
	graph.add(1, "entry");
	graph.add(2, "select");
	graph.add_named(3, "get", "flag");
	graph.set(2, "a", "10");
	graph.set(2, "b", "20");
	graph.wire(3, OUT_PORT, 2, 0);
	add_return(graph, 4, 2, OUT_PORT);
	graph.wire(1, THEN, 4, RUN);

	Entity *entity = attach(graph.script);
	entity->set("flag", true);
	CHECK(entity->call("go") == Variant(10));
	entity->set("flag", false);
	CHECK(entity->call("go") == Variant(20));
	memdelete(entity);
}

// Builds a Switch on the variable "n" whose default and two cases each return a number.
Graph switch_graph(Variant::Type p_type, const String &p_case_type, const String &p_case_0, const String &p_case_1) {
	Graph graph("go");
	graph.variable("n", p_type);
	graph.add(1, "entry");
	graph.add(2, "switch");
	graph.add_named(3, "get", "n");
	CHECK(graph.script->set_step_pins("go", 2, 2) == OK);
	graph.set(2, "case_type", "\"" + p_case_type + "\"");
	graph.set(2, "case0", p_case_0);
	graph.set(2, "case1", p_case_1);
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT_PORT, 2, VALUE);
	add_return_value(graph, 10, "100");
	add_return_value(graph, 11, "101");
	add_return_value(graph, 12, "102");
	graph.wire(2, DEFAULT, 10, RUN);
	graph.wire(2, CASE_0, 11, RUN);
	graph.wire(2, CASE_1, 12, RUN);
	return graph;
}

TEST_CASE("[Stage][GraphScript] Switch picks a case and falls back to the default") {
	Graph integers = switch_graph(Variant::INT, "integer", "1", "2");
	Entity *entity = attach(integers.script);
	entity->set("n", 1);
	CHECK(entity->call("go") == Variant(101));
	entity->set("n", 2);
	CHECK(entity->call("go") == Variant(102));
	entity->set("n", 5);
	CHECK(entity->call("go") == Variant(100));
	memdelete(entity);

	Graph strings = switch_graph(Variant::STRING, "string", "\"left\"", "\"right\"");
	entity = attach(strings.script);
	entity->set("n", "right");
	CHECK(entity->call("go") == Variant(102));
	entity->set("n", "up");
	CHECK(entity->call("go") == Variant(100));
	memdelete(entity);

	Graph named = switch_graph(Variant::INT, "enum", "\"KEY_ESCAPE\"", "\"KEY_SPACE\"");
	entity = attach(named.script);
	entity->set("n", int64_t(Key::SPACE));
	CHECK(entity->call("go") == Variant(102));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Compose builds a Vector3 and Decompose gives the parts back") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "compose");
	graph.set(2, "type", "\"Vector3\"");
	CHECK(graph.script->set_step_pins("go", 2, 3) == OK);
	graph.set(2, "argument0", "1.0");
	graph.set(2, "argument1", "2.0");
	graph.set(2, "argument2", "3.0");
	graph.add(3, "decompose");
	graph.set(3, "type", "\"Vector3\"");
	CHECK(graph.script->set_step_pins("go", 3, 3) == OK);
	graph.wire(2, OUT_PORT, 3, 0);
	graph.variable("made", Variant::VECTOR3);
	graph.add_named(4, "set", "made");
	graph.wire(2, OUT_PORT, 4, VALUE);
	graph.wire(1, THEN, 4, RUN);
	add_return(graph, 5, 3, PART_1);
	graph.wire(4, THEN, 5, RUN);

	Entity *entity = attach(graph.script);
	CHECK(entity->call("go") == Variant(2.0));
	CHECK(entity->get("made") == Variant(Vector3(1, 2, 3)));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Convert changes the type of a value") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "convert");
	graph.set(2, "value", "\"42\"");
	graph.set(2, "to", "\"int\"");
	add_return(graph, 3, 2, OUT_PORT);
	graph.wire(1, THEN, 3, RUN);

	Entity *entity = attach(graph.script);
	CHECK(entity->call("go") == Variant(42));
	graph.set(2, "value", "2.7");
	CHECK(entity->call("go") == Variant(2));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Cast to a wrong class gives null and a false flag") {
	Graph graph("go");
	graph.variable("held", Variant::OBJECT);
	graph.variable("fits", Variant::BOOL);
	graph.add(1, "entry");
	graph.add(2, "self");
	graph.add(3, "cast");
	graph.set(3, "class", "\"Resource\"");
	graph.wire(2, OUT_PORT, 3, 0);
	graph.add_named(4, "set", "held");
	graph.add_named(5, "set", "fits");
	graph.wire(3, OUT_PORT, 4, VALUE);
	graph.wire(3, VALID, 5, VALUE);
	graph.wire(1, THEN, 4, RUN);
	graph.wire(4, THEN, 5, RUN);

	Entity *entity = attach(graph.script);
	entity->set("fits", true);
	entity->call("go");
	CHECK(entity->get("fits") == Variant(false));
	CHECK(entity->get("held").get_validated_object() == nullptr);

	graph.set(3, "class", "\"Entity\"");
	entity->call("go");
	CHECK(entity->get("fits") == Variant(true));
	CHECK(entity->get("held").get_validated_object() == entity);
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Make array and Make dictionary keep the order of their pins") {
	Graph graph("go");
	graph.variable("list", Variant::ARRAY);
	graph.variable("table", Variant::DICTIONARY);
	graph.add(1, "entry");
	graph.add(2, "make_array");
	CHECK(graph.script->set_step_pins("go", 2, 3) == OK);
	graph.set(2, "item0", "3");
	graph.set(2, "item1", "1");
	graph.set(2, "item2", "2");
	graph.add(3, "make_dictionary");
	CHECK(graph.script->set_step_pins("go", 3, 3) == OK);
	const char *keys[] = { "b", "a", "c" };
	for (int i = 0; i < 3; i++) {
		graph.set(3, vformat("key%d", i), String("\"") + keys[i] + "\"");
		graph.set(3, vformat("value%d", i), itos(i));
	}
	graph.add_named(4, "set", "list");
	graph.add_named(5, "set", "table");
	graph.wire(2, OUT_PORT, 4, VALUE);
	graph.wire(3, OUT_PORT, 5, VALUE);
	graph.wire(1, THEN, 4, RUN);
	graph.wire(4, THEN, 5, RUN);

	Entity *entity = attach(graph.script);
	entity->call("go");
	CHECK(entity->get("list") == Variant(Array{ 3, 1, 2 }));
	CHECK(Dictionary(entity->get("table")).keys() == Array{ "b", "a", "c" });
	CHECK(Dictionary(entity->get("table"))["c"] == Variant(2));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Each goes through arrays, dictionaries and counts") {
	struct Case {
		Variant::Type type;
		const char *text;
		int64_t sum;
	};
	const Case cases[] = {
		{ Variant::PACKED_INT32_ARRAY, "PackedInt32Array(1, 2, 3)", 6 },
		{ Variant::DICTIONARY, "{ 4: \"a\", 5: \"b\" }", 9 },
		{ Variant::INT, "4", 6 },
	};
	for (const Case &test : cases) {
		Graph graph("go");
		graph.variable("total", Variant::INT);
		graph.variable("items", test.type, test.text);
		graph.add(1, "entry");
		graph.add(2, "each");
		graph.add_named(3, "get", "items");
		graph.wire(1, THEN, 2, RUN);
		graph.wire(3, OUT_PORT, 2, 1);
		graph.add_running_total(4, "total", 2, 2);
		graph.wire(2, 0, 4, RUN);

		Entity *entity = attach(graph.script);
		entity->call("go");
		CHECK_MESSAGE(entity->get("total") == Variant(test.sum), test.text);
		memdelete(entity);
	}

	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "each");
	graph.set(2, "items", "true");
	graph.wire(1, THEN, 2, RUN);
	Entity *entity = attach(graph.script);
	{
		ERR_PRINT_OFF;
		entity->call("go");
		ERR_PRINT_ON;
	}
	CHECK(GraphScriptLanguage::get_singleton()->debug_get_error().contains("step 2"));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Constant reads each source") {
	struct Case {
		const char *label;
		const char *source;
		const char *type;
		const char *name;
		Variant expected;
	};
	const Case cases[] = {
		{ "literal", "literal", nullptr, nullptr, Variant(7) },
		{ "builtin type", "type", "Vector3", "UP", Variant(Vector3(0, 1, 0)) },
		{ "math", "math", nullptr, "pi", Variant(Math::PI) },
		{ "global", "global", nullptr, "KEY_ESCAPE", Variant(int64_t(Key::ESCAPE)) },
		{ "class", "class", "Object", "CONNECT_ONE_SHOT", Variant(int64_t(Object::CONNECT_ONE_SHOT)) },
		{ "singleton", "singleton", "ResourceLoader", "CACHE_MODE_REUSE", Variant(int64_t(ResourceLoader::CACHE_MODE_REUSE)) },
	};
	for (const Case &test : cases) {
		Graph graph("go");
		graph.add(1, "entry");
		graph.add(2, "constant");
		graph.set(2, "source", String("\"") + test.source + "\"");
		if (String(test.source) == "literal") {
			graph.set(2, "value", "7");
		} else if (String(test.source) == "singleton") {
			graph.set(2, "singleton", String("\"") + test.type + "\"");
		} else if (test.type) {
			graph.set(2, String(test.source) == "type" ? "type" : "class", String("\"") + test.type + "\"");
		}
		if (test.name) {
			graph.set(2, "name", String("\"") + test.name + "\"");
		}
		add_return(graph, 3, 2, OUT_PORT);
		graph.wire(1, THEN, 3, RUN);

		Entity *entity = attach(graph.script);
		CHECK_MESSAGE(entity->call("go") == test.expected, test.label);
		memdelete(entity);
	}
}

struct Param {
	const char *name;
	const char *text;
};

// A step of this kind with these parameters stops the compile, and the error names step 2.
void check_refused(const char *p_kind, std::initializer_list<Param> p_parameters, int p_pins, const char *p_expected) {
	// Loading a missing file prints an error that the test does not want in its log.
	ERR_PRINT_OFF;
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, p_kind);
	if (p_pins > 0) {
		CHECK(graph.script->set_step_pins("go", 2, p_pins) == OK);
	}
	for (const Param &parameter : p_parameters) {
		graph.set(2, parameter.name, parameter.text);
	}
	CHECK_MESSAGE(graph.script->reload() == ERR_COMPILATION_FAILED, p_kind);
	CHECK_MESSAGE(graph.script->get_error().contains("step 2"), p_kind);
	CHECK_MESSAGE(graph.script->get_error().contains(p_expected), graph.script->get_error().utf8().get_data());
	ERR_PRINT_ON;
}

TEST_CASE("[GraphScript] The compile refuses steps whose parameters are wrong") {
	check_refused("constant", { { "source", "\"nowhere\"" } }, 0, "source");
	check_refused("constant", { { "source", "\"global\"" }, { "name", "\"NOT_A_CONSTANT\"" } }, 0, "NOT_A_CONSTANT");
	check_refused("constant", { { "source", "\"type\"" }, { "type", "\"Vector3\"" }, { "name", "\"NOPE\"" } }, 0, "NOPE");
	check_refused("switch", { { "case_type", "\"integer\"" } }, 1, "case0");
	check_refused("compose", { { "type", "\"Vector3\"" } }, 2, "constructor");
	check_refused("decompose", { { "type", "\"Vector3\"" } }, 4, "member");
	check_refused("singleton", { { "name", "\"NoSuchSingleton\"" } }, 0, "NoSuchSingleton");
	check_refused("input_action", { { "action", "\"jump\"" }, { "state", "\"wiggled\"" } }, 0, "state");
	check_refused("convert", { { "value", "1" }, { "to", "\"Nonsense\"" } }, 0, "Nonsense");
	check_refused("cast", { { "value", "1" }, { "class", "\"Nonsense\"" } }, 0, "Nonsense");
	check_refused("make_dictionary", { { "value0", "1" } }, 1, "key0");
	check_refused("preload", { { "path", "\"res://no_such_file.tres\"" } }, 0, "no_such_file");
}

TEST_CASE("[GraphScript] A wire between types with no conversion stops the compile") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "input_action");
	graph.set(2, "action", "\"jump\"");
	graph.set(2, "state", "\"pressed\"");
	graph.add(3, "pick_random");
	graph.wire(2, OUT_PORT, 3, 0);
	CHECK(graph.script->reload() == ERR_COMPILATION_FAILED);
	const String error = graph.script->get_error();
	CHECK(error.contains("step 3"));
	CHECK(error.contains("bool"));
	CHECK(error.contains("Array"));
}

TEST_CASE("[GraphScript] Pins grow and shrink through one method") {
	Ref<GraphScript> script;
	script.instantiate();
	CHECK(script->add_function("go") == OK);
	CHECK(script->add_step("go", 1, "sequence") == OK);
	CHECK(script->add_step("go", 2, "print") == OK);
	CHECK(script->add_step("go", 3, "print") == OK);
	CHECK(script->connect_steps("go", 1, 1, 2, RUN) == OK);
	CHECK(script->connect_steps("go", 1, 2, 3, RUN) == ERR_INVALID_PARAMETER);

	CHECK(script->set_step_pins("go", 1, 3) == OK);
	CHECK(script->connect_steps("go", 1, 2, 3, RUN) == OK);

	Ref<GraphScript> copy;
	copy.instantiate();
	copy->set_functions(script->get_functions());
	CHECK(copy->get_function("go")->has_wire({ 1, 2, 3, RUN }));

	CHECK(script->set_step_pins("go", 1, 2) == OK);
	CHECK_FALSE(script->get_function("go")->has_wire({ 1, 2, 3, RUN }));
	CHECK(script->get_function("go")->has_wire({ 1, 1, 2, RUN }));

	CHECK(script->set_step_pins("go", 1, 1) == ERR_INVALID_PARAMETER);
	CHECK(script->set_step_pins("go", 2, 3) == ERR_INVALID_PARAMETER);
	CHECK(script->set_step_pins("go", 9, 3) == ERR_DOES_NOT_EXIST);
	CHECK(script->set_step_parameter("go", 1, "pins", "5") == ERR_INVALID_PARAMETER);
}

TEST_CASE("[Stage][GraphScript] Entity at path finds a child and names the step when the path is bad") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "entity_at_path");
	graph.set(2, "path", "\"Child\"");
	add_return(graph, 3, 2, OUT_PORT);
	graph.wire(1, THEN, 3, RUN);

	Entity *entity = attach(graph.script);
	Entity *child = memnew(Entity);
	child->set_name("Child");
	entity->add_child(child);
	CHECK(entity->call("go").get_validated_object() == child);

	graph.set(2, "path", "\"Missing\"");
	{
		ERR_PRINT_OFF;
		entity->call("go");
		ERR_PRINT_ON;
	}
	const String error = GraphScriptLanguage::get_singleton()->debug_get_error();
	CHECK(error.contains("step 2"));
	CHECK(error.contains("Missing"));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Self, Stage, Singleton and Autoload give their objects") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "self");
	graph.add(3, "stage");
	graph.add(4, "singleton");
	graph.set(4, "name", "\"ProjectSettings\"");
	graph.add(5, "autoload");
	graph.set(5, "name", "\"GraphTestAutoload\"");
	graph.add(6, "make_array");
	CHECK(graph.script->set_step_pins("go", 6, 4) == OK);
	for (int i = 0; i < 4; i++) {
		graph.wire(2 + i, OUT_PORT, 6, i);
	}
	add_return(graph, 7, 6, OUT_PORT);
	graph.wire(1, THEN, 7, RUN);

	Entity *entity = attach(graph.script);
	Entity *autoload = memnew(Entity);
	autoload->set_name("GraphTestAutoload");
	Stage::get_singleton()->get_root()->add_child(entity);
	Stage::get_singleton()->get_root()->add_child(autoload);

	const Array found = entity->call("go");
	REQUIRE(found.size() == 4);
	CHECK(found[0].get_validated_object() == entity);
	CHECK(found[1].get_validated_object() == Stage::get_singleton());
	CHECK(found[2].get_validated_object() == ProjectSettings::get_singleton());
	CHECK(found[3].get_validated_object() == autoload);
	memdelete(entity);
	memdelete(autoload);
}

TEST_CASE("[Stage][GraphScript] Instantiate prefab returns a detached root") {
	Graph graph("go");
	graph.variable("source", Variant::OBJECT);
	graph.variable("made", Variant::OBJECT);
	graph.add(1, "entry");
	graph.add(2, "instantiate_prefab");
	graph.add_named(3, "get", "source");
	graph.add_named(4, "set", "made");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT_PORT, 2, PREFAB);
	graph.wire(2, THEN, 4, RUN);
	graph.wire(2, MADE, 4, VALUE);

	Entity *packed = memnew(Entity);
	packed->set_name("Packed");
	Ref<Prefab> prefab;
	prefab.instantiate();
	REQUIRE(prefab->pack(packed) == OK);
	memdelete(packed);

	Entity *entity = attach(graph.script);
	entity->set("source", prefab);
	entity->call("go");
	Entity *made = Object::cast_to<Entity>(entity->get("made").get_validated_object());
	REQUIRE(made != nullptr);
	CHECK(made->get_name() == StringName("Packed"));
	CHECK(made->get_parent() == nullptr);
	memdelete(made);
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Resource path gives its path and Preload loads a resource") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "resource_path");
	graph.set(2, "path", "\"res://icon.svg\"");
	add_return(graph, 3, 2, OUT_PORT);
	graph.wire(1, THEN, 3, RUN);

	Entity *entity = attach(graph.script);
	CHECK(entity->call("go") == Variant("res://icon.svg"));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Input action reads the pressed state") {
	InputMap::get_singleton()->add_action("graph_test_jump");
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "input_action");
	graph.set(2, "action", "\"graph_test_jump\"");
	graph.set(2, "state", "\"pressed\"");
	add_return(graph, 3, 2, OUT_PORT);
	graph.wire(1, THEN, 3, RUN);

	Entity *entity = attach(graph.script);
	CHECK(entity->call("go") == Variant(false));
	Input::get_singleton()->action_press("graph_test_jump");
	CHECK(entity->call("go") == Variant(true));
	graph.set(2, "state", "\"released\"");
	CHECK(entity->call("go") == Variant(false));
	Input::get_singleton()->action_release("graph_test_jump");
	CHECK(entity->call("go") == Variant(true));
	memdelete(entity);
	InputMap::get_singleton()->erase_action("graph_test_jump");
}

void collect_print(void *p_text, const String &p_string, bool p_error, bool p_rich) {
	*static_cast<String *>(p_text) += p_string;
}

TEST_CASE("[Stage][GraphScript] Print writes its text to the log and goes on") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "print");
	graph.set(2, "text", "\"hello graph\"");
	add_return_value(graph, 3, "9");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 3, RUN);

	String text;
	PrintHandlerList handler;
	handler.printfunc = collect_print;
	handler.userdata = &text;
	add_print_handler(&handler);
	Entity *entity = attach(graph.script);
	CHECK(entity->call("go") == Variant(9));
	remove_print_handler(&handler);
	CHECK(text.contains("hello graph"));
	memdelete(entity);
}

} // namespace TestGraphSteps
