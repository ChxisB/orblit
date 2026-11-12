/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "graph_script.h"

#include "core/object/class_registry.h"

void initialize_graph_script_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		ORBLIT_REGISTER_CLASS(GraphScript);
	}
}

void uninitialize_graph_script_module(ModuleInitializationLevel p_level) {}
