/**************************************************************************/
/*  test_graph_exports.h                                                  */

#pragma once

#include "test_graph_functions.h"

#include "core/config/engine.h"
#include "core/io/resource_loader.h"
#include "core/io/resource_saver.h"
#include "core/object/script_language.h"

namespace TestGraphExports {

using TestGraphFunctions::entry_text;
using TestGraphFunctions::list_text;
using TestGraphScriptRun::attach;
using TestGraphScriptRun::Graph;

constexpr int RUN = 0;

Ref<GraphScript> make_speed_script() {
	Ref<GraphScript> script;
	script.instantiate();
	CHECK(script->add_variable("speed", Variant::FLOAT) == OK);
	CHECK(script->set_variable_exported("speed", true) == OK);
	CHECK(script->set_variable_default("speed", "2.5") == OK);
	return script;
}

int find_property(const List<PropertyInfo> &p_list, const String &p_name) {
	int index = 0;
	for (const PropertyInfo &property : p_list) {
		if (property.name == p_name) {
			return index;
		}
		index++;
	}
	return -1;
}

bool has_warning(const Array &p_warnings, const String &p_kind, const String &p_name) {
	for (const Variant &value : p_warnings) {
		const Dictionary warning = value;
		if (warning["kind"] == Variant(p_kind) && warning["name"] == Variant(p_name)) {
			return true;
		}
	}
	return false;
}

// Makes the engine behave as the editor does: a script that is not a tool is not run.
struct EditorScripting {
	EditorScripting() {
		ScriptSystem::set_scripting_enabled(false);
		Engine::get_singleton()->set_editor_hint(true);
	}
	~EditorScripting() {
		ScriptSystem::set_scripting_enabled(true);
		Engine::get_singleton()->set_editor_hint(false);
	}
};

TEST_CASE("[Stage][GraphScript] An exported variable lists its hint and group, and edits survive a reload") {
	Ref<GraphScript> script = make_speed_script();
	CHECK(script->set_variable_hint("speed", PROPERTY_HINT_RANGE, "0,10,0.5") == OK);
	CHECK(script->set_variable_info("speed", "Movement", "How fast it goes.") == OK);

	List<PropertyInfo> properties;
	script->get_script_property_list(&properties);
	const int group = find_property(properties, "Movement");
	const int speed = find_property(properties, "speed");
	REQUIRE(group >= 0);
	REQUIRE(speed == group + 1);
	for (const PropertyInfo &property : properties) {
		if (property.name == "Movement") {
			CHECK((property.usage & PROPERTY_USAGE_GROUP) != 0);
		}
		if (property.name == "speed") {
			CHECK(property.hint == PROPERTY_HINT_RANGE);
			CHECK(property.hint_string == "0,10,0.5");
		}
	}

	Entity *entity = attach(script);
	CHECK(entity->get("speed") == Variant(2.5));
	entity->set("speed", 7.0);
	CHECK(script->reload() == OK);
	CHECK(entity->get("speed") == Variant(7.0));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] The hint, group and description of a variable are checked and saved") {
	Ref<GraphScript> script = make_speed_script();
	CHECK(script->set_variable_hint("speed", PROPERTY_HINT_MAX, "") == ERR_INVALID_PARAMETER);
	CHECK(script->set_variable_hint("speed", -1, "") == ERR_INVALID_PARAMETER);
	CHECK(script->set_variable_hint("missing", PROPERTY_HINT_NONE, "") == ERR_DOES_NOT_EXIST);
	CHECK(script->set_variable_info("missing", "", "") == ERR_DOES_NOT_EXIST);

	// A plain variable keeps the four keys older files have.
	const Dictionary plain = Array(script->get_variables())[0];
	CHECK(plain.size() == 4);

	CHECK(script->set_variable_hint("speed", PROPERTY_HINT_RANGE, "0,10") == OK);
	CHECK(script->set_variable_info("speed", "Movement", "How fast it goes.") == OK);
	const String path = TestUtils::get_temp_path("exports.ograph");
	CHECK(ResourceSaver::save(script, path) == OK);
	Ref<GraphScript> loaded = ResourceLoader::load(path, "", ResourceLoader::CACHE_MODE_IGNORE);
	REQUIRE(loaded.is_valid());
	CHECK(loaded->get_variables() == script->get_variables());
	CHECK(loaded->get_variable("speed")->category == "Movement");
	CHECK(loaded->get_variable("speed")->description == "How fast it goes.");
	CHECK(loaded->get_variable("speed")->hint_string == "0,10");
}

TEST_CASE("[Stage][GraphScript] A placeholder lists the exports and follows edits") {
	Ref<GraphScript> script = make_speed_script();
	CHECK(script->add_variable("hidden", Variant::INT) == OK);

	Entity *entity = memnew(Entity);
	ScriptInstance *placeholder = script->placeholder_instance_create(entity);
	REQUIRE(placeholder != nullptr);
	CHECK(placeholder->is_placeholder());
	List<PropertyInfo> properties;
	placeholder->get_property_list(&properties);
	CHECK(find_property(properties, "speed") >= 0);

	CHECK(script->add_variable("lives", Variant::INT) == OK);
	CHECK(script->set_variable_exported("lives", true) == OK);
	properties.clear();
	placeholder->get_property_list(&properties);
	CHECK(find_property(properties, "lives") >= 0);

	memdelete(placeholder);
	memdelete(entity);
	// The script drops the placeholder when it is deleted, so this edit has nobody to tell.
	CHECK(script->remove_variable("lives") == OK);
}

TEST_CASE("[Stage][GraphScript] An entity in the editor holds a placeholder and not a running script") {
	Ref<GraphScript> script = make_speed_script();
	{
		EditorScripting editor;
		CHECK_FALSE(script->can_instantiate());
		Entity *entity = attach(script);
		REQUIRE(entity->get_script_instance() != nullptr);
		CHECK(entity->get_script_instance()->is_placeholder());
		CHECK(entity->get("speed") == Variant(2.5));
		memdelete(entity);
	}
	CHECK(script->can_instantiate());
}

TEST_CASE("[Stage][GraphScript] A reload with a renamed variable drops its value and keeps the others") {
	Ref<GraphScript> script;
	script.instantiate();
	CHECK(script->add_variable("a", Variant::INT) == OK);
	CHECK(script->add_variable("b", Variant::INT) == OK);
	CHECK(script->add_variable("c", Variant::INT) == OK);
	Entity *entity = attach(script);
	entity->set("a", 5);
	entity->set("b", 6);
	entity->set("c", 7);

	// "a" is renamed, "b" keeps its name and changes type, "c" is untouched.
	Array variables = script->get_variables();
	Dictionary first = variables[0];
	first["name"] = "renamed";
	Dictionary second = variables[1];
	second["type"] = int(Variant::STRING);
	second["default"] = String();
	script->set_variables(variables);

	CHECK(entity->get("renamed") == Variant(0));
	CHECK(entity->get("b") == Variant(String()));
	CHECK(entity->get("c") == Variant(7));
	memdelete(entity);
}

TEST_CASE("[Stage][GraphScript] A base type limits the objects a script goes on") {
	Ref<GraphScript> script;
	script.instantiate();
	CHECK(script->get_instance_base_type() == StringName());
	script->set_base_type("Entity");
	CHECK(script->get_instance_base_type() == StringName("Entity"));

	Entity *entity = attach(script);
	CHECK(entity->get_script_instance() != nullptr);
	memdelete(entity);

	Ref<Resource> resource;
	resource.instantiate();
	{
		ErrorDetector errors;
		ERR_PRINT_OFF;
		resource->set_script(script);
		ERR_PRINT_ON;
		CHECK(errors.has_error);
	}
	CHECK(resource->get_script_instance() == nullptr);

	{
		ErrorDetector errors;
		ERR_PRINT_OFF;
		script->set_base_type("NoSuchClass");
		ERR_PRINT_ON;
		CHECK(errors.has_error);
	}
	CHECK(script->get_instance_base_type() == StringName("Entity"));

	const String path = TestUtils::get_temp_path("base_type.ograph");
	CHECK(ResourceSaver::save(script, path) == OK);
	Ref<GraphScript> loaded = ResourceLoader::load(path, "", ResourceLoader::CACHE_MODE_IGNORE);
	REQUIRE(loaded.is_valid());
	CHECK(loaded->get_instance_base_type() == StringName("Entity"));
}

TEST_CASE("[GraphScript] The language offers one empty graph per base type") {
	GraphScriptLanguage *language = GraphScriptLanguage::get_singleton();
	CHECK(language->is_using_templates());

	const Vector<ScriptLanguage::ScriptTemplate> templates = language->get_built_in_templates("Entity");
	REQUIRE(templates.size() == 1);
	CHECK(templates[0].inherit == "Entity");
	CHECK_FALSE(templates[0].name.is_empty());

	Ref<GraphScript> made = language->make_template(templates[0].content, "Player", "Entity");
	REQUIRE(made.is_valid());
	CHECK(made->get_instance_base_type() == StringName("Entity"));
	CHECK(made->get_functions().is_empty());

	// The dialog passes a quoted path when the base is another script, and no graph has one.
	Ref<GraphScript> from_path = language->make_template(String(), "Player", "\"res://base.ograph\"");
	REQUIRE(from_path.is_valid());
	CHECK(from_path->get_instance_base_type() == StringName());
}

TEST_CASE("[GraphScript] Variable and function descriptions reach the documentation") {
#ifdef TOOLS_ENABLED
	Ref<GraphScript> script = make_speed_script();
	CHECK(script->set_variable_info("speed", "", "How fast it goes.") == OK);
	Graph graph(script, "go");
	graph.add(1, "entry");
	graph.set(1, "description", "\"Starts moving.\"");

	// A script that was never saved has no name to document under.
	CHECK(script->get_documentation().is_empty());
	const String path = TestUtils::get_temp_path("Mover.ograph");
	CHECK(ResourceSaver::save(script, path) == OK);
	script->set_path(path, true);

	const Vector<DocData::ClassDoc> docs = script->get_documentation();
	REQUIRE(docs.size() == 1);
	CHECK(docs[0].name == "Mover");
	CHECK(script->get_doc_class_name() == StringName("Mover"));
	REQUIRE(docs[0].properties.size() == 1);
	CHECK(docs[0].properties[0].name == "speed");
	CHECK(docs[0].properties[0].description == "How fast it goes.");
	REQUIRE(docs[0].methods.size() == 1);
	CHECK(docs[0].methods[0].description == "Starts moving.");
#endif // TOOLS_ENABLED
}

TEST_CASE("[GraphScript] A variable no step reads or writes is reported, unless it is exported or starts with an underscore") {
	Ref<GraphScript> script;
	script.instantiate();
	CHECK(script->add_variable("spare", Variant::INT) == OK);
	CHECK(script->add_variable("used", Variant::INT) == OK);
	CHECK(script->add_variable("_quiet", Variant::INT) == OK);
	CHECK(script->add_variable("shown", Variant::INT) == OK);
	CHECK(script->set_variable_exported("shown", true) == OK);
	Graph graph(script, "main");
	graph.add(1, "entry");
	graph.add_named(2, "set", "used");
	graph.wire(1, 0, 2, RUN);

	const Array warnings = script->get_warnings();
	CHECK(has_warning(warnings, "unused_variable", "spare"));
	CHECK_FALSE(has_warning(warnings, "unused_variable", "used"));
	CHECK_FALSE(has_warning(warnings, "unused_variable", "_quiet"));
	CHECK_FALSE(has_warning(warnings, "unused_variable", "shown"));
}

TEST_CASE("[GraphScript] A local that no step uses is reported, and the function flag turns it off") {
	Graph graph("main");
	graph.add(1, "entry");
	graph.set(1, "locals", list_text(entry_text("temp", Variant::INT), entry_text("count", Variant::INT)));
	graph.add_named(2, "local", "count");
	graph.add(3, "print");
	graph.wire(1, 0, 3, RUN);
	graph.wire(2, 0, 3, 1);

	CHECK(has_warning(graph.script->get_warnings(), "unused_local", "temp"));
	CHECK_FALSE(has_warning(graph.script->get_warnings(), "unused_local", "count"));

	graph.set(1, "ignored_warnings", "[\"unused_local\"]");
	CHECK_FALSE(has_warning(graph.script->get_warnings(), "unused_local", "temp"));
}

TEST_CASE("[GraphScript] A step the start never reaches is reported, and the function flag turns it off") {
	Graph graph("main");
	graph.add(1, "entry");
	graph.add(2, "print");
	graph.set(2, "text", "\"reached\"");
	graph.add(3, "print");
	graph.set(3, "text", "\"alone\"");
	graph.wire(1, 0, 2, RUN);

	const Array warnings = graph.script->get_warnings();
	REQUIRE(warnings.size() == 1);
	const Dictionary warning = warnings[0];
	CHECK(warning["kind"] == Variant("unreachable_step"));
	CHECK(warning["function"] == Variant("main"));
	CHECK(warning["step"] == Variant(3));

	graph.set(1, "ignored_warnings", "[\"unreachable_step\"]");
	CHECK(graph.script->get_warnings().is_empty());
}

TEST_CASE("[GraphScript] An argument or local named like a variable is reported, and the function flag turns it off") {
	Graph graph("main");
	graph.variable("speed", Variant::INT);
	graph.add(1, "entry");
	graph.set(1, "arguments", list_text(entry_text("speed", Variant::INT)));
	graph.set(1, "locals", list_text(entry_text("lives", Variant::INT)));
	graph.variable("lives", Variant::INT);

	const Array warnings = graph.script->get_warnings();
	CHECK(has_warning(warnings, "shadowed_name", "speed"));
	CHECK(has_warning(warnings, "shadowed_name", "lives"));

	graph.set(1, "ignored_warnings", "[\"shadowed_name\"]");
	CHECK_FALSE(has_warning(graph.script->get_warnings(), "shadowed_name", "speed"));
}

TEST_CASE("[GraphScript] A function that does not compile gives no warnings of its own") {
	Graph graph("main");
	graph.add(1, "entry");
	graph.add(2, "print");
	graph.set(2, "text", "\"x\"");
	CHECK(graph.script->set_step_parameter("main", 1, "arguments", "5") == OK);
	CHECK_FALSE(graph.script->is_script_valid());
	CHECK(graph.script->get_warnings().is_empty());
}

} // namespace TestGraphExports
