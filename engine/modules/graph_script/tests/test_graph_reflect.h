/**************************************************************************/
/*  test_graph_reflect.h                                                  */

#pragma once

#include "../graph_step_catalogue.h"
#include "test_graph_script_run.h"

#include "core/object/object.h"
#include "scene/stage/entity.h"

namespace TestGraphReflect {

using TestGraphScriptRun::attach;
using TestGraphScriptRun::Graph;

// Port numbers follow the port lists that graph_step_reflect.cpp derives. A flow step has
// the run input first. A pure step has none.
constexpr int RUN = 0;
constexpr int THEN = 0;
constexpr int OUT = 0;
constexpr int VALUE = 1;
constexpr int FLOW_TARGET = 1;
constexpr int FLOW_ARGUMENT = 2;
constexpr int FLOW_RESULT = 1;
constexpr int PURE_TARGET = 0;
constexpr int NEW_OBJECT = 1;
constexpr int FREE_OBJECT = 1;

String quoted(const String &p_text) {
	return "\"" + p_text + "\"";
}

// Adds a step that names a class and one member of it.
void add_member(Graph &p_graph, int p_id, const String &p_kind, const String &p_class, const char *p_parameter, const String &p_member) {
	p_graph.add(p_id, p_kind);
	p_graph.set(p_id, "class", quoted(p_class));
	p_graph.set(p_id, p_parameter, quoted(p_member));
}

void add_method(Graph &p_graph, int p_id, const String &p_kind, const String &p_class, const String &p_method) {
	add_member(p_graph, p_id, p_kind, p_class, "method", p_method);
}

// Adds a Return step that gives the output of another step.
void add_return(Graph &p_graph, int p_id, int p_from, int p_port) {
	p_graph.add(p_id, "return");
	p_graph.wire(p_from, p_port, p_id, VALUE);
}

int64_t id_of(Entity *p_entity) {
	return int64_t(p_entity->get("id"));
}

// Makes an object of a class, stores its id in the variable "id" and frees it.
Entity *attach_new_and_free(Graph &p_graph, const String &p_class) {
	p_graph.variable("id", Variant::INT);
	p_graph.add(1, "entry");
	p_graph.add(2, "new");
	p_graph.set(2, "class", quoted(p_class));
	add_method(p_graph, 3, "query_method", "Object", "get_instance_id");
	p_graph.add_named(4, "set", "id");
	p_graph.add(5, "free");
	p_graph.wire(1, THEN, 2, RUN);
	p_graph.wire(2, THEN, 4, RUN);
	p_graph.wire(4, THEN, 5, RUN);
	p_graph.wire(2, NEW_OBJECT, 3, PURE_TARGET);
	p_graph.wire(3, OUT, 4, VALUE);
	p_graph.wire(2, NEW_OBJECT, 5, FREE_OBJECT);
	return attach(p_graph.script);
}

bool has_entry(const Vector<GraphStepEntry> &p_entries, const String &p_kind, const String &p_group, const char *p_parameter, const String &p_member) {
	for (const GraphStepEntry &entry : p_entries) {
		if (entry.kind == StringName(p_kind) && entry.group == p_group && String(entry.parameters.get(p_parameter, "")) == p_member) {
			return true;
		}
	}
	return false;
}

TEST_CASE("[Stage][GraphScript] Call method runs a method of another entity and a query reads it back") {
	Graph graph("go");
	graph.variable("other", Variant::OBJECT);
	graph.add(1, "entry");
	add_method(graph, 2, "call_method", "Entity", "set_name");
	graph.set(2, "argument_0", quoted("Renamed"));
	graph.add_named(3, "get", "other");
	add_method(graph, 4, "query_method", "Entity", "get_name");
	add_return(graph, 5, 4, OUT);
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, FLOW_TARGET);
	graph.wire(3, OUT, 4, PURE_TARGET);
	graph.wire(2, THEN, 5, RUN);

	Entity *entity = attach(graph.script);
	Entity *other = memnew(Entity);
	entity->set("other", other);
	CHECK(String(entity->call("go")) == "Renamed");
	CHECK(other->get_name() == StringName("Renamed"));
	memdelete(other);
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A call with no target wired runs on the owner of the script") {
	Graph graph("go");
	graph.add(1, "entry");
	add_method(graph, 2, "query_method", "Entity", "get_child_count");
	add_return(graph, 3, 2, OUT);
	graph.wire(1, THEN, 3, RUN);

	Entity *entity = attach(graph.script);
	entity->add_child(memnew(Entity));
	entity->add_child(memnew(Entity));
	CHECK(entity->call("go") == Variant(2));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Call method passes the extra arguments of a method that takes any number") {
	Graph graph("go");
	graph.variable("other", Variant::OBJECT);
	graph.add(1, "entry");
	add_method(graph, 2, "call_method", "Object", "call");
	graph.add_named(3, "get", "other");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, FLOW_TARGET);
	CHECK(graph.script->set_step_pins(graph.function, 2, 1) == OK);
	graph.set(2, "argument_0", quoted("set_name"));
	graph.set(2, "extra0", quoted("ViaCall"));

	Entity *entity = attach(graph.script);
	Entity *other = memnew(Entity);
	entity->set("other", other);
	entity->call("go");
	CHECK(other->get_name() == StringName("ViaCall"));
	memdelete(other);
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A failed call ends the function and names the step") {
	Graph graph("go");
	graph.variable("after", Variant::INT);
	graph.add(1, "entry");
	add_method(graph, 2, "call_method", "Entity", "get_child_count");
	graph.add_named(3, "set", "after");
	graph.set(3, "value", "1");
	graph.add(4, "constant");
	graph.set(4, "value", "5");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 3, RUN);
	// A number is no object, so the call has nothing to run on.
	graph.wire(4, OUT, 2, FLOW_TARGET);

	Entity *entity = attach(graph.script);
	ErrorDetector errors;
	ERR_PRINT_OFF;
	entity->call("go");
	ERR_PRINT_ON;
	CHECK(errors.has_error);
	CHECK(GraphScriptLanguage::get_singleton()->debug_get_error().contains("step 2"));
	CHECK(entity->get("after") == Variant(0));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Set property and get property write and read a property") {
	Graph graph("go");
	graph.add(1, "entry");
	add_member(graph, 2, "set_property", "Entity", "property", "run_priority");
	graph.set(2, "value", "7");
	add_member(graph, 3, "get_property", "Entity", "property", "run_priority");
	add_return(graph, 4, 3, OUT);
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 4, RUN);

	Entity *entity = attach(graph.script);
	CHECK(entity->call("go") == Variant(7));
	CHECK(entity->get("run_priority") == Variant(7));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Emit reaches a function that Connect attached to the signal") {
	Graph graph("go");
	CHECK(graph.script->add_signal("ping") == OK);
	graph.variable("heard", Variant::INT);
	Graph listener(graph.script, "on_ping");
	listener.add(1, "entry");
	listener.add_named(2, "set", "heard");
	listener.set(2, "value", "1");
	listener.wire(1, THEN, 2, RUN);
	graph.add(1, "entry");
	graph.add(2, "connect");
	graph.set(2, "signal", quoted("ping"));
	graph.set(2, "method", quoted("on_ping"));
	graph.add(3, "emit");
	graph.set(3, "signal", quoted("ping"));
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 3, RUN);

	Entity *entity = attach(graph.script);
	entity->call("go");
	CHECK(entity->get("heard") == Variant(1));
	// A second run connects the same pair again, and that is fine.
	entity->set("heard", 0);
	{
		ErrorDetector errors;
		entity->call("go");
		CHECK_FALSE(errors.has_error);
	}
	CHECK(entity->get("heard") == Variant(1));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Emit on sends a signal of a class from another object") {
	Graph graph("go");
	graph.variable("heard", Variant::INT);
	Graph listener(graph.script, "on_renamed");
	listener.add(1, "entry");
	listener.add_named(2, "set", "heard");
	listener.set(2, "value", "1");
	listener.wire(1, THEN, 2, RUN);
	graph.add(1, "entry");
	graph.add(2, "connect");
	graph.set(2, "signal", quoted("renamed"));
	graph.set(2, "method", quoted("on_renamed"));
	graph.add(3, "emit_on");
	graph.set(3, "class", quoted("Entity"));
	graph.set(3, "signal", quoted("renamed"));
	graph.add(4, "self");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 3, RUN);
	graph.wire(4, OUT, 2, 1);
	graph.wire(4, OUT, 3, FLOW_TARGET);

	Entity *entity = attach(graph.script);
	entity->call("go");
	CHECK(entity->get("heard") == Variant(1));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Call builtin runs methods of a string and of an array") {
	Graph graph("go");
	graph.variable("list", Variant::ARRAY);
	graph.variable("shout", Variant::STRING);
	graph.add(1, "entry");
	graph.add(2, "call_builtin");
	graph.set(2, "type", quoted("Array"));
	graph.set(2, "method", quoted("append"));
	graph.set(2, "argument_0", "5");
	graph.add_named(3, "get", "list");
	graph.add(4, "query_builtin");
	graph.set(4, "type", quoted("String"));
	graph.set(4, "method", quoted("to_upper"));
	graph.set(4, "target", quoted("abc"));
	graph.add_named(5, "set", "shout");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, FLOW_TARGET);
	graph.wire(2, THEN, 5, RUN);
	graph.wire(4, OUT, 5, VALUE);

	Entity *entity = attach(graph.script);
	entity->call("go");
	const Array list = entity->get("list");
	REQUIRE(list.size() == 1);
	CHECK(list[0] == Variant(5));
	CHECK(entity->get("shout") == Variant("ABC"));
	memdelete(entity);
}

TEST_CASE("[GraphScript] Call builtin refuses a method that changes a type copied into the step") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "call_builtin");
	graph.set(2, "type", quoted("String"));
	graph.set(2, "method", quoted("erase"));

	CHECK(graph.script->get_error().contains("step 2"));
}

TEST_CASE("[Stage][GraphScript] Call utility and query utility run utility functions") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "call_utility");
	graph.set(2, "function", quoted("randomize"));
	graph.add(3, "query_utility");
	graph.set(3, "function", quoted("maxi"));
	graph.set(3, "argument_0", "3");
	graph.set(3, "argument_1", "8");
	add_return(graph, 4, 3, OUT);
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 4, RUN);

	Entity *entity = attach(graph.script);
	CHECK(entity->call("go") == Variant(8));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Call static runs a static method of a class") {
	Graph graph("go");
	graph.add(1, "entry");
	add_method(graph, 2, "call_static", "JSON", "stringify");
	graph.set(2, "argument_0", "[1, 2]");
	add_return(graph, 3, 2, FLOW_RESULT);
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 3, RUN);

	Entity *entity = attach(graph.script);
	CHECK(entity->call("go") == Variant("[1,2]"));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] New then Free makes an object and deletes it at once") {
	Graph graph("go");
	Entity *entity = attach_new_and_free(graph, "Object");
	entity->call("go");
	CHECK(ObjectRegistry::get_instance(ObjectID(uint64_t(id_of(entity)))) == nullptr);
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Free queues an entity instead of deleting it in the middle of a frame") {
	Graph graph("go");
	Entity *entity = attach_new_and_free(graph, "Entity");
	entity->call("go");
	Object *made = ObjectRegistry::get_instance(ObjectID(uint64_t(id_of(entity))));
	REQUIRE(made != nullptr);
	CHECK(made->is_queued_for_deletion());
	memdelete(made);
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Free refuses an object that counts its references") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "new");
	graph.set(2, "class", quoted("RefCounted"));
	graph.add(3, "free");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 3, RUN);
	graph.wire(2, NEW_OBJECT, 3, FREE_OBJECT);

	Entity *entity = attach(graph.script);
	{
		ErrorDetector errors;
		ERR_PRINT_OFF;
		entity->call("go");
		ERR_PRINT_ON;
		CHECK(errors.has_error);
	}
	CHECK(GraphScriptLanguage::get_singleton()->debug_get_error().contains("step 3"));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Call function runs another function of the script and reads its result") {
	Graph graph("go");
	Graph helper(graph.script, "helper");
	helper.add(1, "entry");
	helper.add(2, "return");
	helper.set(2, "value", "9");
	helper.wire(1, THEN, 2, RUN);
	graph.add(1, "entry");
	graph.add(2, "call_function");
	graph.set(2, "function", quoted("helper"));
	add_return(graph, 3, 2, FLOW_RESULT);
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 3, RUN);

	Entity *entity = attach(graph.script);
	CHECK(entity->call("go") == Variant(9));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A function that calls itself stops at the depth limit") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "call_function");
	graph.set(2, "function", quoted("go"));
	graph.wire(1, THEN, 2, RUN);

	Entity *entity = attach(graph.script);
	{
		ErrorDetector errors;
		ERR_PRINT_OFF;
		entity->call("go");
		ERR_PRINT_ON;
		CHECK(errors.has_error);
	}
	CHECK(GraphScriptLanguage::get_singleton()->debug_get_error().contains("deeper"));
	memdelete(entity);
}

TEST_CASE("[GraphScript] A wire whose type cannot convert fails to compile") {
	Graph graph("go");
	graph.add(1, "entry");
	add_method(graph, 2, "call_method", "Entity", "set_name");
	graph.add(3, "make_array");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, FLOW_ARGUMENT);

	const String error = graph.script->get_error();
	CHECK(error.contains("step 2"));
	CHECK(error.contains("needs a StringName"));
}

TEST_CASE("[GraphScript] A value typed in by hand must convert to the type of its input") {
	Graph graph("go");
	graph.add(1, "entry");
	add_method(graph, 2, "call_method", "Entity", "set_name");
	graph.set(2, "argument_0", "[1]");
	graph.wire(1, THEN, 2, RUN);

	const String error = graph.script->get_error();
	CHECK(error.contains("step 2"));
	CHECK(error.contains("'name'"));
}

TEST_CASE("[GraphScript] A query step refuses a method that changes something") {
	Graph graph("go");
	graph.add(1, "entry");
	add_method(graph, 2, "query_method", "Entity", "set_name");
	graph.set(2, "argument_0", quoted("x"));

	CHECK(graph.script->get_error().contains("step 2"));
}

TEST_CASE("[GraphScript] A step that names a class or method this build lacks fails to compile") {
	Graph graph("go");
	graph.add(1, "entry");
	add_method(graph, 2, "call_method", "NoSuchClass", "go");
	CHECK(graph.script->get_error().contains("NoSuchClass"));

	graph.set(2, "class", quoted("Entity"));
	graph.set(2, "method", quoted("no_such_method"));
	CHECK(graph.script->get_error().contains("no_such_method"));
}

TEST_CASE("[GraphScript] Changing the method of a step drops the wires its new ports cannot hold") {
	Graph graph("go");
	graph.add(1, "entry");
	add_method(graph, 2, "call_method", "Entity", "set_name");
	graph.add(3, "constant");
	graph.set(3, "value", quoted("x"));
	graph.wire(1, THEN, 2, RUN);
	graph.wire(3, OUT, 2, FLOW_ARGUMENT);
	CHECK(graph.script->get_function(graph.function)->get_wire_count() == 2);

	graph.set(2, "method", quoted("get_name"));
	CHECK(graph.script->get_function(graph.function)->get_wire_count() == 1);
	CHECK(graph.script->get_error().is_empty());
}

TEST_CASE("[GraphScript] A saved wire on a step whose class is missing survives a load") {
	GraphFunction function("go");
	CHECK(function.add_step(1, "entry") == OK);
	CHECK(function.add_step(2, "call_method") == OK);
	CHECK(function.set_step_parameter(2, "class", "Entity") == OK);
	CHECK(function.set_step_parameter(2, "method", "get_name") == OK);
	CHECK(function.connect_steps({ 1, 0, 2, 0 }) == OK);

	Dictionary saved = function.to_dictionary();
	const Array steps = saved["steps"];
	const Dictionary step = steps[1];
	Dictionary parameters = step["parameters"];
	parameters["class"] = "Missing";

	GraphFunction loaded;
	CHECK(GraphFunction::from_dictionary(saved, loaded) == OK);
	CHECK(loaded.get_wire_count() == 1);
}

#ifndef SPATIAL_DISABLED
TEST_CASE("[GraphScript] The catalogue finds a spatial method from a part of its name") {
	const Vector<GraphStepEntry> found = GraphStepCatalogue::find("translate", GraphWireFilter());
	CHECK(has_entry(found, "call_method", "Entity3D", "method", "translate"));
}

TEST_CASE("[GraphScript] The catalogue offers a Set step for a vector only when the dragged wire is a vector") {
	GraphWireFilter vector;
	vector.dragged_from = GraphPinSide::OUTPUT;
	vector.type = Variant::VECTOR3;
	GraphWireFilter flag;
	flag.dragged_from = GraphPinSide::OUTPUT;
	flag.type = Variant::BOOL;

	CHECK(has_entry(GraphStepCatalogue::find("position", vector), "set_property", "Entity3D", "property", "position"));
	CHECK_FALSE(has_entry(GraphStepCatalogue::find("position", flag), "set_property", "Entity3D", "property", "position"));
}
#endif // SPATIAL_DISABLED

TEST_CASE("[GraphScript] The catalogue lists the table steps for no text and the engine for some") {
	const Vector<GraphStepEntry> table = GraphStepCatalogue::find("", GraphWireFilter());
	bool has_branch = false;
	for (const GraphStepEntry &entry : table) {
		has_branch = has_branch || (entry.kind == StringName("branch") && entry.group == "Flow" && entry.label == "Branch");
		CHECK(String(entry.kind) != "call_method");
	}
	CHECK(has_branch);

	const Vector<GraphStepEntry> found = GraphStepCatalogue::find("count loop", GraphWireFilter());
	REQUIRE(found.size() >= 1);
	CHECK(found[0].kind == StringName("count_loop"));
}

TEST_CASE("[GraphScript] A label row gives a builtin method a plain name") {
	const Vector<GraphStepEntry> found = GraphStepCatalogue::find("add to array", GraphWireFilter());
	REQUIRE(found.size() >= 1);
	CHECK(found[0].kind == StringName("call_builtin"));
	CHECK(found[0].label == "Add to array");
	CHECK(String(found[0].parameters["type"]) == "Array");
	CHECK(String(found[0].parameters["method"]) == "append");
}

} // namespace TestGraphReflect
