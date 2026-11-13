/**************************************************************************/
/*  test_graph_frames.h                                                   */

#pragma once

#include "test_graph_document.h"

#include "core/io/resource_loader.h"
#include "core/io/resource_saver.h"
#include "tests/test_utils.h"

namespace TestGraphFrames {

using TestGraphDocument::first_function;
using TestGraphDocument::read_document;
using TestGraphScriptRun::Graph;

constexpr int THEN = 0;
constexpr int OUT = 0;
constexpr int VALUE = 1;
constexpr int RUN = 0;

const char *const FRAME = "{\"id\": 1, \"title\": \"Setup\", \"x\": 10, \"y\": 20, \"width\": 300, \"height\": 150, \"collapsed\": false}";

TEST_CASE("[GraphScript] A frame is stored with its title, place and size") {
	Graph graph("main");
	CHECK(graph.script->set_frame("main", FRAME) == OK);

	const Array frames = first_function(read_document(graph.script))["frames"];
	REQUIRE(frames.size() == 1);
	const Dictionary frame = frames[0];
	CHECK(int(frame["id"]) == 1);
	CHECK(frame["title"] == Variant("Setup"));
	CHECK(float(frame["x"]) == 10.0f);
	CHECK(float(frame["height"]) == 150.0f);
	CHECK_FALSE(bool(frame["collapsed"]));
}

TEST_CASE("[GraphScript] Setting a frame again replaces the frame with that id") {
	Graph graph("main");
	CHECK(graph.script->set_frame("main", FRAME) == OK);
	CHECK(graph.script->set_frame("main", "{\"id\": 1, \"title\": \"Moved\", \"x\": 0, \"y\": 0, \"width\": 50, \"height\": 50, \"collapsed\": true}") == OK);
	CHECK(graph.script->set_frame("main", "{\"id\": 2, \"title\": \"Other\", \"x\": 0, \"y\": 0, \"width\": 50, \"height\": 50, \"collapsed\": false}") == OK);

	const Array frames = first_function(read_document(graph.script))["frames"];
	REQUIRE(frames.size() == 2);
	CHECK(Dictionary(frames[0])["title"] == Variant("Moved"));
	CHECK(bool(Dictionary(frames[0])["collapsed"]));
}

TEST_CASE("[GraphScript] A frame that is not valid is refused and changes nothing") {
	Graph graph("main");
	CHECK(graph.script->set_frame("main", FRAME) == OK);
	const Array before = graph.script->get_functions();

	CHECK(graph.script->set_frame("main", "not json") == ERR_PARSE_ERROR);
	CHECK(graph.script->set_frame("main", "[1, 2]") == ERR_INVALID_DATA);
	CHECK(graph.script->set_frame("main", "{\"id\": 0, \"title\": \"\", \"x\": 0, \"y\": 0, \"width\": 50, \"height\": 50, \"collapsed\": false}") == ERR_INVALID_PARAMETER);
	CHECK(graph.script->set_frame("main", "{\"id\": 3, \"title\": \"\", \"x\": 0, \"y\": 0, \"width\": 0, \"height\": 50, \"collapsed\": false}") == ERR_INVALID_PARAMETER);
	CHECK(graph.script->set_frame("main", "{\"id\": 3, \"title\": 5, \"x\": 0, \"y\": 0, \"width\": 50, \"height\": 50, \"collapsed\": false}") == ERR_INVALID_DATA);
	CHECK(graph.script->set_frame("nowhere", FRAME) == ERR_DOES_NOT_EXIST);
	CHECK(graph.script->get_functions() == before);
}

TEST_CASE("[GraphScript] A frame is removed by its id") {
	Graph graph("main");
	CHECK(graph.script->set_frame("main", FRAME) == OK);

	CHECK(graph.script->remove_frame("main", 2) == ERR_DOES_NOT_EXIST);
	CHECK(graph.script->remove_frame("main", 1) == OK);
	CHECK(Array(first_function(read_document(graph.script))["frames"]).is_empty());
}

TEST_CASE("[GraphScript] A frame does not change how the function compiles or runs") {
	Graph graph("main");
	graph.add(1, "entry");
	CHECK(graph.script->is_script_valid());
	CHECK(graph.script->set_frame("main", FRAME) == OK);
	CHECK(graph.script->is_script_valid());
	CHECK(graph.script->remove_step("main", 1) == OK);
	CHECK(Array(first_function(read_document(graph.script))["frames"]).size() == 1);
}

TEST_CASE("[GraphScript] Frames survive a save and a load") {
	Graph graph("main");
	CHECK(graph.script->set_frame("main", FRAME) == OK);

	const String path = TestUtils::get_temp_path("frames.ograph");
	CHECK(ResourceSaver::save(graph.script, path) == OK);
	Ref<GraphScript> loaded = ResourceLoader::load(path, "", ResourceLoader::CACHE_MODE_IGNORE);
	REQUIRE(loaded.is_valid());
	CHECK(loaded->get_functions() == graph.script->get_functions());
	CHECK(loaded->get_function("main")->get_frame_count() == 1);
}

TEST_CASE("[GraphScript] A file with no frames key loads with no frames") {
	GraphFunction function;
	Dictionary entry;
	entry["name"] = "main";
	entry["steps"] = Array();
	entry["wires"] = Array();
	CHECK(GraphFunction::from_dictionary(entry, function) == OK);
	CHECK(function.get_frame_count() == 0);

	entry["frames"] = "wrong";
	GraphFunction broken;
	CHECK(GraphFunction::from_dictionary(entry, broken) == ERR_INVALID_DATA);
}

TEST_CASE("[Stage][GraphScript] A data reroute passes its value on") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.add(1, "entry");
	graph.add(2, "constant");
	graph.set(2, "value", "7");
	graph.add(3, "reroute");
	graph.add_named(4, "set", "total");
	graph.wire(1, THEN, 4, RUN);
	graph.wire(2, OUT, 3, 0);
	graph.wire(3, OUT, 4, VALUE);

	Entity *entity = TestGraphScriptRun::attach(graph.script);
	entity->call("go");
	CHECK(entity->get("total") == Variant(7));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A flow reroute passes the order on") {
	Graph graph("go");
	graph.variable("total", Variant::INT);
	graph.add(1, "entry");
	graph.add(2, "reroute_flow");
	graph.add_named(3, "set", "total");
	graph.set(3, "value", "4");
	graph.wire(1, THEN, 2, RUN);
	graph.wire(2, THEN, 3, RUN);

	Entity *entity = TestGraphScriptRun::attach(graph.script);
	entity->call("go");
	CHECK(entity->get("total") == Variant(4));
	memdelete(entity);
}

TEST_CASE("[GraphScript] A reroute takes a wire of any type and a flow reroute only takes flow") {
	Graph graph("go");
	graph.add(1, "entry");
	graph.add(2, "constant");
	graph.add(3, "reroute");
	graph.add(4, "reroute_flow");

	CHECK(graph.script->connect_steps("go", 2, OUT, 3, 0) == OK);
	CHECK(graph.script->connect_steps("go", 2, OUT, 4, RUN) == ERR_INVALID_PARAMETER);
	CHECK(graph.script->connect_steps("go", 1, THEN, 4, RUN) == OK);
}

} // namespace TestGraphFrames
