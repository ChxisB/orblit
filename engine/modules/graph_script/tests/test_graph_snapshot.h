/**************************************************************************/
/*  test_graph_snapshot.h                                                 */

#pragma once

#include "test_graph_document.h"

namespace TestGraphSnapshot {

using TestGraphScriptRun::Graph;

constexpr int RUN = 0;
constexpr int THEN = 0;
constexpr int OUT = 0;
constexpr int VALUE = 1;

TEST_CASE("[Stage][GraphScript] A snapshot restores the document at any point of ten edits, back and forth") {
	Graph graph("main");
	Ref<GraphScript> script = graph.script;
	Vector<String> snapshots;
	Vector<String> documents;
	const auto note = [&]() {
		snapshots.push_back(script->get_snapshot());
		documents.push_back(script->get_document());
	};

	note();
	graph.add(1, "entry");
	note();
	graph.variable("total", Variant::INT);
	note();
	graph.add_named(2, "set", "total");
	note();
	graph.add(3, "constant");
	note();
	graph.set(3, "value", "7");
	note();
	graph.wire(1, THEN, 2, RUN);
	note();
	graph.wire(3, OUT, 2, VALUE);
	note();
	CHECK(script->add_signal("done") == OK);
	note();
	CHECK(script->move_step("main", 3, 40, 50) == OK);
	note();
	CHECK(script->rename_variable("total", "sum") == OK);
	note();

	for (int at = snapshots.size() - 1; at >= 0; at--) {
		CAPTURE(at);
		CHECK(script->restore_snapshot(snapshots[at]) == OK);
		CHECK(script->get_document() == documents[at]);
	}
	for (int at = 0; at < snapshots.size(); at++) {
		CAPTURE(at);
		CHECK(script->restore_snapshot(snapshots[at]) == OK);
		CHECK(script->get_document() == documents[at]);
	}
}

TEST_CASE("[Stage][GraphScript] A snapshot holds the base type") {
	Ref<GraphScript> script;
	script.instantiate();
	script->set_base_type("Entity");
	const String with_base = script->get_snapshot();
	script->set_base_type(StringName());
	CHECK(script->get_base_type() == StringName());

	CHECK(script->restore_snapshot(with_base) == OK);
	CHECK(script->get_base_type() == StringName("Entity"));
}

TEST_CASE("[Stage][GraphScript] Restoring text that is not a snapshot is refused and changes nothing") {
	Graph graph("main");
	graph.add(1, "entry");
	const String before = graph.script->get_document();

	CHECK(graph.script->restore_snapshot("not a snapshot") != OK);
	CHECK(graph.script->restore_snapshot("[1, 2]") != OK);
	CHECK(graph.script->restore_snapshot("{\"functions\": 3}") != OK);
	CHECK(graph.script->restore_snapshot("{\"base_type\": \"NoSuchClass\", \"functions\": [], \"variables\": [], \"signals\": []}") != OK);
	CHECK(graph.script->get_document() == before);
}

} // namespace TestGraphSnapshot
