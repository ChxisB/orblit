/**************************************************************************/
/*  graph_script_language.cpp                                             */

#include "graph_script_language.h"

#include "graph_script.h"

#include "core/object/class_registry.h"

GraphScriptLanguage *GraphScriptLanguage::singleton = nullptr;

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
