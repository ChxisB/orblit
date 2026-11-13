/**************************************************************************/
/*  test_graph_dialogue.h                                                 */

#pragma once

#include "../dialogue_box.h"
#include "../graph_script_language.h"
#include "../graph_step_catalogue.h"
#include "test_graph_latent.h"

#include "scene/resources/prefab.h"

namespace TestGraphDialogue {

using TestGraphLatent::advance;
using TestGraphLatent::enter_stage;
using TestGraphScriptRun::Graph;
using TestGraphScriptRun::OUT;
using TestGraphScriptRun::RUN;
using TestGraphScriptRun::THEN;
using TestGraphScriptRun::VALUE;

// Port numbers follow the rows in graph_step_kinds.cpp. The choice inputs come after the fixed ones.
constexpr int MESSAGE_STEP = 2;
constexpr int PREFAB_INPUT = 3;
constexpr int FIRST_CHOICE_INPUT = 4;
constexpr int FIRST_CHOICE_OUTPUT = 1;
constexpr const char *PREFAB_SETTING = "graph_script/dialogue/prefab";

// What the box offers, as one line.
String shown(const DialogueBox *p_box) {
	return String(",").join(p_box->get_choices());
}

// The box a message put on the stage, or null when none is waiting for an answer.
DialogueBox *find_box() {
	Entity *root = Stage::get_singleton()->get_root();
	for (int i = 0; i < root->get_child_count(); i++) {
		DialogueBox *box = Object::cast_to<DialogueBox>(root->get_child(i));
		if (box && !box->is_queued_for_deletion()) {
			return box;
		}
	}
	return nullptr;
}

// A function "go" that shows a message with p_pins choices. Choice number i, counted from zero,
// sets "picked" to i + 1. The "then" output sets it to 9.
Graph dialogue_graph(int p_pins) {
	Graph graph("go");
	graph.variable("picked", Variant::INT);
	graph.variable("source", Variant::OBJECT);
	graph.add(1, "entry");
	graph.add(MESSAGE_STEP, "dialogue_message");
	CHECK(graph.script->set_step_pins("go", MESSAGE_STEP, p_pins) == OK);
	graph.set(MESSAGE_STEP, "speaker", "\"Ann\"");
	graph.set(MESSAGE_STEP, "text", "\"Hello\"");
	graph.wire(1, THEN, MESSAGE_STEP, RUN);
	for (int i = 0; i < p_pins; i++) {
		graph.add(10 + i, "dialogue_choice");
		graph.set(10 + i, "text", vformat("\"Choice %d\"", i));
		graph.wire(10 + i, OUT, MESSAGE_STEP, FIRST_CHOICE_INPUT + i);
		graph.add_named(20 + i, "set", "picked");
		graph.set(20 + i, "value", itos(i + 1));
		graph.wire(MESSAGE_STEP, FIRST_CHOICE_OUTPUT + i, 20 + i, RUN);
	}
	graph.add_named(30, "set", "picked");
	graph.set(30, "value", "9");
	graph.wire(MESSAGE_STEP, THEN, 30, RUN);
	return graph;
}

// Packs a one entity prefab, so a test can hand the message a box of its own.
Ref<Prefab> pack_root(Entity *p_root) {
	Ref<Prefab> prefab;
	prefab.instantiate();
	REQUIRE(prefab->pack(p_root) == OK);
	memdelete(p_root);
	return prefab;
}

// Runs "go" and says whether it reported an error.
bool runs_with_error(Entity *p_entity) {
	ErrorDetector errors;
	ERR_PRINT_OFF;
	p_entity->call("go");
	ERR_PRINT_ON;
	return errors.has_error;
}

TEST_CASE("[Stage][GraphScript] A message with no choices shows a Continue button and follows then") {
	Graph graph = dialogue_graph(0);
	Entity *entity = enter_stage(graph.script);
	entity->call("go");
	DialogueBox *box = find_box();
	REQUIRE(box != nullptr);
	CHECK(box->get_speaker() == "Ann");
	CHECK(box->get_text() == "Hello");
	CHECK(shown(box).is_empty());
	CHECK(entity->get("picked") == Variant(0));

	box->choose(-1);
	CHECK(entity->get("picked") == Variant(9));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A choice follows its own output") {
	Graph graph = dialogue_graph(3);
	Entity *entity = enter_stage(graph.script);
	entity->call("go");
	DialogueBox *box = find_box();
	REQUIRE(box != nullptr);
	CHECK(shown(box) == "Choice 0,Choice 1,Choice 2");

	box->choose(1);
	CHECK(entity->get("picked") == Variant(2));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A hidden choice is not shown and the others keep their outputs") {
	Graph graph = dialogue_graph(3);
	graph.set(11, "visible", "false");
	Entity *entity = enter_stage(graph.script);
	entity->call("go");
	DialogueBox *box = find_box();
	REQUIRE(box != nullptr);
	CHECK(shown(box) == "Choice 0,Choice 2");

	box->choose(1);
	CHECK(entity->get("picked") == Variant(3));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A choice with no wire is not shown") {
	Graph graph = dialogue_graph(2);
	CHECK(graph.script->disconnect_steps("go", 10, OUT, MESSAGE_STEP, FIRST_CHOICE_INPUT) == OK);
	Entity *entity = enter_stage(graph.script);
	entity->call("go");
	DialogueBox *box = find_box();
	REQUIRE(box != nullptr);
	CHECK(shown(box) == "Choice 1");

	box->choose(0);
	CHECK(entity->get("picked") == Variant(2));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A message whose choices are all hidden follows then") {
	Graph graph = dialogue_graph(2);
	graph.set(10, "visible", "false");
	graph.set(11, "visible", "false");
	Entity *entity = enter_stage(graph.script);
	entity->call("go");
	DialogueBox *box = find_box();
	REQUIRE(box != nullptr);
	CHECK(shown(box).is_empty());

	box->choose(-1);
	CHECK(entity->get("picked") == Variant(9));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] The box is queued for removal after a choice") {
	Graph graph = dialogue_graph(1);
	Entity *entity = enter_stage(graph.script);
	entity->call("go");
	DialogueBox *box = find_box();
	REQUIRE(box != nullptr);
	const ObjectID id = box->get_instance_id();

	box->choose(0);
	CHECK(box->is_queued_for_deletion());
	CHECK(find_box() == nullptr);
	advance(0.0);
	CHECK(ObjectRegistry::get_instance(id) == nullptr);
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A packed box on the prefab input is the box that shows") {
	Graph graph = dialogue_graph(1);
	graph.add_named(40, "get", "source");
	graph.wire(40, OUT, MESSAGE_STEP, PREFAB_INPUT);
	DialogueBox *custom = memnew(DialogueBox);
	custom->set_name("Custom");

	Entity *entity = enter_stage(graph.script);
	entity->set("source", pack_root(custom));
	entity->call("go");
	DialogueBox *box = find_box();
	REQUIRE(box != nullptr);
	CHECK(box->get_name() == StringName("Custom"));

	box->choose(0);
	CHECK(entity->get("picked") == Variant(1));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A prefab with no show_message method fails the call") {
	Graph graph = dialogue_graph(1);
	graph.add_named(40, "get", "source");
	graph.wire(40, OUT, MESSAGE_STEP, PREFAB_INPUT);
	Entity *plain = memnew(Entity);
	plain->set_name("Plain");

	Entity *entity = enter_stage(graph.script);
	entity->set("source", pack_root(plain));
	CHECK(runs_with_error(entity));
	CHECK(GraphScriptLanguage::get_singleton()->debug_get_error().contains("show_message"));
	CHECK(find_box() == nullptr);
	CHECK(entity->get("picked") == Variant(0));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A project setting that names a missing prefab fails the call") {
	Graph graph = dialogue_graph(1);
	ProjectSettings::get_singleton()->set_setting(PREFAB_SETTING, "res://missing_box.oscn");
	Entity *entity = enter_stage(graph.script);
	CHECK(runs_with_error(entity));
	CHECK(GraphScriptLanguage::get_singleton()->debug_get_error().contains(PREFAB_SETTING));
	CHECK(find_box() == nullptr);
	ProjectSettings::get_singleton()->set_setting(PREFAB_SETTING, "");
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] Fewer pins drop the wires of the pins that go") {
	Graph graph = dialogue_graph(3);
	CHECK(graph.script->set_step_pins("go", MESSAGE_STEP, 1) == OK);
	CHECK(graph.script->set_step_pins("go", MESSAGE_STEP, 3) == OK);
	Entity *entity = enter_stage(graph.script);
	entity->call("go");
	DialogueBox *box = find_box();
	REQUIRE(box != nullptr);
	CHECK(shown(box) == "Choice 0");
	memdelete(entity);
}

TEST_CASE("[GraphScript] A message holds at most ten choices") {
	Graph graph = dialogue_graph(10);
	CHECK(graph.script->set_step_pins("go", MESSAGE_STEP, 11) == ERR_INVALID_PARAMETER);
	CHECK(graph.script->set_step_pins("go", MESSAGE_STEP, 0) == OK);
}

TEST_CASE("[GraphScript] The catalogue offers both dialogue steps with no text") {
	bool has_message = false;
	bool has_choice = false;
	for (const GraphStepEntry &entry : GraphStepCatalogue::find("", GraphWireFilter())) {
		has_message = has_message || (entry.kind == StringName("dialogue_message") && entry.group == "Dialogue");
		has_choice = has_choice || (entry.kind == StringName("dialogue_choice") && entry.group == "Dialogue");
	}
	CHECK(has_message);
	CHECK(has_choice);
}

} // namespace TestGraphDialogue
