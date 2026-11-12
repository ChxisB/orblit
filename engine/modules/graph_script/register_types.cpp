/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "graph_call.h"
#include "graph_script.h"
#include "graph_script_language.h"

#include "core/config/project_settings.h"
#include "core/object/class_registry.h"

static GraphScriptLanguage *script_language_graph = nullptr;

void initialize_graph_script_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
	GLOBAL_DEF(PropertyInfo(Variant::INT, "graph_script/runtime/step_budget", PROPERTY_HINT_RANGE, "1,100000000,1"), 1000000);
	ORBLIT_REGISTER_CLASS(GraphScript);
	ORBLIT_REGISTER_ABSTRACT_CLASS(GraphCall);
	script_language_graph = memnew(GraphScriptLanguage);
	ScriptSystem::register_language(script_language_graph);
}

void uninitialize_graph_script_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
	ScriptSystem::unregister_language(script_language_graph);
	memdelete(script_language_graph);
	script_language_graph = nullptr;
}
