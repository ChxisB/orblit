/**************************************************************************/
/*  graph_script_language.cpp                                             */

#include "graph_script_language.h"

#include "graph_call.h"
#include "graph_debug.h"
#include "graph_script.h"

#include "core/object/class_registry.h"

GraphScriptLanguage *GraphScriptLanguage::singleton = nullptr;

namespace {

void limit_values(List<Variant> *r_values, int p_max_subitems, int p_max_depth) {
	for (Variant &value : *r_values) {
		value = GraphDebug::limit(value, p_max_subitems, p_max_depth);
	}
}

} // namespace

GraphScriptLanguage::GraphScriptLanguage() {
	singleton = this;
}

GraphScriptLanguage::~GraphScriptLanguage() {
	singleton = nullptr;
}

void GraphScriptLanguage::report_error(const String &p_text) {
	last_error = p_text;
	ERR_PRINT(p_text);
}

int GraphScriptLanguage::debug_get_stack_level_count() const {
	return GraphDebug::get_level_count();
}

// The line is the id of the step the call runs, since a graph has no lines.
int GraphScriptLanguage::debug_get_stack_level_line(int p_level) const {
	const GraphCall *call = GraphDebug::get_level(p_level);
	ERR_FAIL_NULL_V(call, 0);
	return call->get_running_step().id;
}

String GraphScriptLanguage::debug_get_stack_level_function(int p_level) const {
	const GraphCall *call = GraphDebug::get_level(p_level);
	ERR_FAIL_NULL_V(call, String());
	return call->get_function().name;
}

String GraphScriptLanguage::debug_get_stack_level_source(int p_level) const {
	const GraphCall *call = GraphDebug::get_level(p_level);
	ERR_FAIL_NULL_V(call, String());
	return GraphDebug::get_source(*call);
}

void GraphScriptLanguage::debug_get_stack_level_locals(int p_level, List<String> *r_locals, List<Variant> *r_values, int p_max_subitems, int p_max_depth) {
	const GraphCall *call = GraphDebug::get_level(p_level);
	ERR_FAIL_NULL(call);
	call->list_locals(r_locals, r_values);
	limit_values(r_values, p_max_subitems, p_max_depth);
}

void GraphScriptLanguage::debug_get_stack_level_members(int p_level, List<String> *r_members, List<Variant> *r_values, int p_max_subitems, int p_max_depth) {
	const GraphCall *call = GraphDebug::get_level(p_level);
	ERR_FAIL_NULL(call);
	call->get_instance().list_members(r_members, r_values);
	limit_values(r_values, p_max_subitems, p_max_depth);
}

ScriptInstance *GraphScriptLanguage::debug_get_stack_level_instance(int p_level) {
	const GraphCall *call = GraphDebug::get_level(p_level);
	ERR_FAIL_NULL_V(call, nullptr);
	return &call->get_instance();
}

void GraphScriptLanguage::reload_scripts(const Array &p_scripts) {
	for (const Variant &script : p_scripts) {
		reload_tool_script(script);
	}
}

// A graph script reloads as a plain resource, so this does not come back here.
void GraphScriptLanguage::reload_tool_script(const Ref<Script> &p_script) {
	if (p_script.is_valid()) {
		p_script->reload_from_file();
	}
}

// Every base type gets the same empty graph, so the create dialog lists one per class it walks.
Vector<ScriptLanguage::ScriptTemplate> GraphScriptLanguage::get_built_in_templates(const StringName &p_object) {
	ScriptTemplate empty;
	empty.inherit = p_object;
	empty.name = "Empty graph";
	empty.description = "A graph script with no functions.";
	Vector<ScriptTemplate> templates;
	templates.push_back(empty);
	return templates;
}

// A graph has no source text, so the template text is not read. The dialog passes a quoted path
// when the base is a script, and a graph script cannot extend one, so it keeps no base type then.
Ref<Script> GraphScriptLanguage::make_template(const String &p_template, const String &p_class_name, const String &p_base_class_name) const {
	Ref<GraphScript> script;
	script.instantiate();
	if (ClassRegistry::class_exists(p_base_class_name)) {
		script->set_base_type(p_base_class_name);
	}
	return script;
}
