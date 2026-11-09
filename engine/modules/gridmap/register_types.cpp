/**************************************************************************/
/*  register_types.cpp                                                    */

#ifndef SPATIAL_DISABLED

#include "register_types.h"

#include "grid_map.h"

#ifdef TOOLS_ENABLED
#include "editor/grid_map_editor_plugin.h"
#endif

#include "core/object/class_registry.h"

void initialize_gridmap_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SCENE) {
		ORBLIT_REGISTER_CLASS(GridMap);
#ifndef NAVIGATION_SPATIAL_DISABLED
		GridMap::navmesh_parse_init();
#endif // NAVIGATION_SPATIAL_DISABLED
	}
#ifdef TOOLS_ENABLED
	if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		ORBLIT_REGISTER_VIRTUAL_CLASS(GridMapEditorPlugin);
		EditorPlugins::add_by_type<GridMapEditorPlugin>();
	}
#endif
}

void uninitialize_gridmap_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SCENE) {
		return;
	}
}

#endif // SPATIAL_DISABLED
