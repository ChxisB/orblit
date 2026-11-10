/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "snapshot_collector.h"

#ifdef TOOLS_ENABLED
#include "editor/object_registry_profiler_plugin.h"
#endif // TOOLS_ENABLED

void initialize_object_registry_profiler_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		SnapshotCollector::initialize();
	}
#ifdef TOOLS_ENABLED
	else if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		EditorPlugins::add_by_type<ObjectRegistryProfilerPlugin>();
	}
#endif // TOOLS_ENABLED
}

void uninitialize_object_registry_profiler_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		SnapshotCollector::deinitialize();
	}
}
