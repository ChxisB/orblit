/**************************************************************************/
/*  test_graph_script.h                                                   */

#pragma once

#include "../graph_script.h"

#include "core/io/file_access.h"
#include "core/io/resource_loader.h"
#include "core/io/resource_saver.h"
#include "tests/test_macros.h"
#include "tests/test_utils.h"

namespace TestGraphScript {

// Port numbers follow the order of the rows in graph_step_kinds.cpp.
constexpr int ENTRY_THEN = 0;
constexpr int SET_RUN = 0;
constexpr int SET_VALUE = 1;
constexpr int CONSTANT_VALUE = 0;

Ref<GraphScript> make_script_with_main() {
	Ref<GraphScript> script;
	script.instantiate();
	CHECK(script->add_function("main") == OK);
	return script;
}

// Entry (1) wired to Set (2), with a Constant (3) feeding the value of the Set.
Ref<GraphScript> make_wired_script() {
	Ref<GraphScript> script = make_script_with_main();
	CHECK(script->add_step("main", 1, "entry") == OK);
	CHECK(script->add_step("main", 2, "set") == OK);
	CHECK(script->add_step("main", 3, "constant") == OK);
	CHECK(script->connect_steps("main", 1, ENTRY_THEN, 2, SET_RUN) == OK);
	CHECK(script->connect_steps("main", 3, CONSTANT_VALUE, 2, SET_VALUE) == OK);
	return script;
}

TEST_CASE("[Stage][GraphScript] Functions, variables and signals refuse bad names") {
	Ref<GraphScript> script;
	script.instantiate();

	CHECK(script->add_function("main") == OK);
	CHECK(script->add_function("main") == ERR_ALREADY_EXISTS);
	CHECK(script->add_function("not a name") == ERR_INVALID_PARAMETER);
	CHECK(script->add_function("") == ERR_INVALID_PARAMETER);
	CHECK(script->remove_function("other") == ERR_DOES_NOT_EXIST);
	CHECK(script->remove_function("main") == OK);
	CHECK(script->get_function("main") == nullptr);

	CHECK(script->add_variable("speed", Variant::FLOAT) == OK);
	CHECK(script->add_variable("speed", Variant::INT) == ERR_ALREADY_EXISTS);
	CHECK(script->add_variable("bad type", Variant::INT) == ERR_INVALID_PARAMETER);
	CHECK(script->add_variable("odd", Variant::VARIANT_MAX) == ERR_INVALID_PARAMETER);
	CHECK(script->get_variable("speed")->default_value == Variant(0.0));
	CHECK(script->set_variable_default("speed", "2.5") == OK);
	CHECK(script->get_variable("speed")->default_value == Variant(2.5));
	CHECK(script->set_variable_default("speed", "\"fast\"") == ERR_INVALID_PARAMETER);
	CHECK(script->set_variable_default("speed", "Vector2(") == ERR_PARSE_ERROR);
	CHECK(script->set_variable_default("other", "1") == ERR_DOES_NOT_EXIST);
	CHECK(script->get_variable("speed")->default_value == Variant(2.5));
	CHECK(script->remove_variable("speed") == OK);
	CHECK(script->remove_variable("speed") == ERR_DOES_NOT_EXIST);

	CHECK(script->add_signal("died") == OK);
	CHECK(script->add_signal("died") == ERR_ALREADY_EXISTS);
	CHECK(script->add_signal("1died") == ERR_INVALID_PARAMETER);
	CHECK(script->remove_signal("died") == OK);
	CHECK(script->remove_signal("died") == ERR_DOES_NOT_EXIST);
}

TEST_CASE("[Stage][GraphScript] Steps are added, moved and removed with their wires") {
	Ref<GraphScript> script = make_wired_script();
	const GraphFunction *function = script->get_function("main");

	CHECK(function->get_step_count() == 3);
	CHECK(function->get_wire_count() == 2);
	CHECK(script->get_valid_step_id("main") == 4);
	CHECK(script->get_valid_step_id("missing") == -1);

	CHECK(script->add_step("main", 2, "set") == ERR_ALREADY_EXISTS);
	CHECK(script->add_step("main", 0, "set") == ERR_INVALID_PARAMETER);
	CHECK(script->add_step("main", 9, "no_such_kind") == ERR_INVALID_PARAMETER);
	CHECK(script->add_step("missing", 9, "set") == ERR_DOES_NOT_EXIST);

	CHECK(script->move_step("main", 2, 40.0f, -8.0f) == OK);
	CHECK(function->get_step(2)->position == Vector2(40, -8));
	CHECK(script->move_step("main", 99, 0.0f, 0.0f) == ERR_DOES_NOT_EXIST);

	CHECK(script->set_step_parameter("main", 3, "value", "42") == OK);
	CHECK(function->get_step(3)->parameters["value"] == Variant(42));
	CHECK(script->set_step_parameter("main", 3, "value", "Vector2(") == ERR_PARSE_ERROR);
	CHECK(function->get_step(3)->parameters["value"] == Variant(42));
	CHECK(script->set_step_parameter("main", 99, "value", "1") == ERR_DOES_NOT_EXIST);

	CHECK(script->remove_step("main", 2) == OK);
	CHECK(function->get_step_count() == 2);
	CHECK(function->get_wire_count() == 0);
	CHECK(script->remove_step("main", 2) == ERR_DOES_NOT_EXIST);
}

TEST_CASE("[Stage][GraphScript] A wire joins an output to an input of the same sort") {
	Ref<GraphScript> script = make_wired_script();
	const Array before = script->get_functions();

	// Flow output to data input.
	CHECK(script->connect_steps("main", 1, ENTRY_THEN, 2, SET_VALUE) == ERR_INVALID_PARAMETER);
	// Data output to flow input.
	CHECK(script->connect_steps("main", 3, CONSTANT_VALUE, 2, SET_RUN) == ERR_INVALID_PARAMETER);
	// A port that does not exist.
	CHECK(script->connect_steps("main", 1, 5, 2, SET_RUN) == ERR_INVALID_PARAMETER);
	CHECK(script->connect_steps("main", 1, ENTRY_THEN, 2, -1) == ERR_INVALID_PARAMETER);
	// A step that does not exist, and a step wired to itself.
	CHECK(script->connect_steps("main", 1, ENTRY_THEN, 99, SET_RUN) == ERR_DOES_NOT_EXIST);
	CHECK(script->connect_steps("main", 2, 0, 2, SET_RUN) == ERR_INVALID_PARAMETER);
	// The same wire twice.
	CHECK(script->connect_steps("main", 3, CONSTANT_VALUE, 2, SET_VALUE) == ERR_ALREADY_EXISTS);

	CHECK(script->get_functions() == before);
}

TEST_CASE("[Stage][GraphScript] A flow output keeps one wire and a data input holds one wire") {
	Ref<GraphScript> script = make_wired_script();
	const GraphFunction *function = script->get_function("main");

	// A second constant cannot feed the same data input.
	CHECK(script->add_step("main", 4, "constant") == OK);
	CHECK(script->connect_steps("main", 4, CONSTANT_VALUE, 2, SET_VALUE) == ERR_ALREADY_IN_USE);
	CHECK(function->get_wire_count() == 2);

	// A data output may feed many inputs.
	CHECK(script->add_step("main", 5, "set") == OK);
	CHECK(script->connect_steps("main", 3, CONSTANT_VALUE, 5, SET_VALUE) == OK);

	// A new flow wire from one output replaces the first.
	CHECK(script->connect_steps("main", 1, ENTRY_THEN, 5, SET_RUN) == OK);
	CHECK(function->has_wire({ 1, ENTRY_THEN, 5, SET_RUN }));
	CHECK_FALSE(function->has_wire({ 1, ENTRY_THEN, 2, SET_RUN }));

	CHECK(script->disconnect_steps("main", 1, ENTRY_THEN, 5, SET_RUN) == OK);
	CHECK(script->disconnect_steps("main", 1, ENTRY_THEN, 5, SET_RUN) == ERR_DOES_NOT_EXIST);
}

TEST_CASE("[Stage][GraphScript] The data answers the script queries") {
	Ref<GraphScript> script = make_wired_script();
	CHECK(script->add_function("helper") == OK);
	CHECK(script->add_variable("speed", Variant::FLOAT) == OK);
	CHECK(script->add_variable("secret", Variant::INT) == OK);
	CHECK(script->set_variable_exported("speed", true) == OK);
	CHECK(script->set_variable_default("speed", "3.0") == OK);
	CHECK(script->add_signal("died") == OK);

	CHECK(script->has_method("main"));
	CHECK(script->has_method("helper"));
	CHECK_FALSE(script->has_method("other"));
	CHECK(script->get_method_info("main").name == "main");

	List<MethodInfo> methods;
	script->get_script_method_list(&methods);
	CHECK(methods.size() == 2);

	List<MethodInfo> signals;
	script->get_script_signal_list(&signals);
	CHECK(signals.size() == 1);
	CHECK(script->has_script_signal("died"));
	CHECK_FALSE(script->has_script_signal("born"));

	List<PropertyInfo> properties;
	script->get_script_property_list(&properties);
	CHECK(properties.size() == 2);
	CHECK((properties.front()->get().usage & PROPERTY_USAGE_EDITOR) != 0);
	CHECK((properties.back()->get().usage & PROPERTY_USAGE_EDITOR) == 0);

	Variant value;
	CHECK(script->get_property_default_value("speed", value));
	CHECK(value == Variant(3.0));
	CHECK_FALSE(script->get_property_default_value("other", value));

	CHECK(script->is_script_valid());
	CHECK_FALSE(script->can_instantiate());
	CHECK(script->get_language() == nullptr);
}

TEST_CASE("[Stage][GraphScript] A script saves and loads as text") {
	Ref<GraphScript> script = make_wired_script();
	CHECK(script->add_variable("speed", Variant::FLOAT) == OK);
	CHECK(script->set_variable_exported("speed", true) == OK);
	CHECK(script->add_signal("died") == OK);
	CHECK(script->set_step_parameter("main", 3, "value", "Vector2(1, 2)") == OK);
	CHECK(script->move_step("main", 2, 12.5f, 7.0f) == OK);

	const String path = TestUtils::get_temp_path("round_trip.ograph");
	CHECK(ResourceSaver::save(script, path) == OK);
	CHECK(ResourceLoader::get_resource_type(path) == "GraphScript");

	Ref<GraphScript> loaded = ResourceLoader::load(path);
	REQUIRE(loaded.is_valid());
	CHECK(loaded->get_functions() == script->get_functions());
	CHECK(loaded->get_variables() == script->get_variables());
	CHECK(loaded->get_signals() == script->get_signals());
	CHECK(loaded->get_function("main")->get_step(3)->parameters["value"] == Variant(Vector2(1, 2)));
}

TEST_CASE("[Stage][GraphScript] A step of an unknown kind survives a round trip") {
	Ref<GraphScript> script = make_wired_script();
	CHECK(script->add_step("main", 4, "set_local") == OK);
	CHECK(script->set_step_parameter("main", 4, "note", "\"kept\"") == OK);
	const String path = TestUtils::get_temp_path("newer.ograph");
	CHECK(ResourceSaver::save(script, path) == OK);

	// A newer build wrote a kind this build does not have.
	const String newer = FileAccess::get_file_as_string(path).replace("\"set_local\"", "\"future_kind\"");
	REQUIRE(newer.contains("future_kind"));
	{
		Ref<FileAccess> file = FileAccess::open(path, FileAccess::WRITE);
		file->store_string(newer);
	}

	Ref<GraphScript> loaded = ResourceLoader::load(path, "", ResourceLoader::CACHE_MODE_IGNORE);
	REQUIRE(loaded.is_valid());
	CHECK_FALSE(loaded->is_script_valid());
	CHECK(loaded->get_function("main")->has_missing_steps());
	CHECK(loaded->get_function("main")->get_step(4)->kind == "future_kind");
	CHECK(loaded->get_function("main")->get_step(4)->parameters["note"] == Variant("kept"));

	// A step that is missing cannot be wired, but saving it again loses nothing.
	CHECK(loaded->connect_steps("main", 1, ENTRY_THEN, 4, 0) == ERR_UNAVAILABLE);
	const String resaved_path = TestUtils::get_temp_path("resaved.ograph");
	CHECK(ResourceSaver::save(loaded, resaved_path) == OK);
	CHECK(FileAccess::get_file_as_string(resaved_path) == newer);
}

TEST_CASE("[Stage][GraphScript] A malformed list is refused and leaves the script as it was") {
	Ref<GraphScript> script = make_wired_script();
	const Array before = script->get_functions();

	Dictionary bad_step;
	bad_step["id"] = "one";
	Array steps;
	steps.push_back(bad_step);
	Dictionary bad_function;
	bad_function["name"] = "main";
	bad_function["steps"] = steps;
	bad_function["wires"] = Array();
	Array bad_list;
	bad_list.push_back(bad_function);

	ERR_PRINT_OFF;
	script->set_functions(bad_list);
	ERR_PRINT_ON;
	CHECK(script->get_functions() == before);
}

} // namespace TestGraphScript
