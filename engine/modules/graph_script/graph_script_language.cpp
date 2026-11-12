/**************************************************************************/
/*  graph_script_language.cpp                                             */

#include "graph_script_language.h"

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
