/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "spatial/navigation_system_spatial.h"

#ifdef TOOLS_ENABLED
#include "editor/navigation_link_spatial_editor_plugin.h"
#include "editor/navigation_obstacle_spatial_editor_plugin.h"
#include "editor/navigation_region_spatial_editor_plugin.h"
#endif

#include "core/config/engine.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "systems/navigation_spatial/navigation_system_spatial.h"
#include "systems/navigation_spatial/navigation_system_spatial_manager.h"

static NavigationSystem3D *_createOrblitNavigation3DCallback() {
	return memnew(OrblitNavigationSystem3D);
}

void initialize_navigation_spatial_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		NavigationSystem3DManager::get_singleton()->register_system("OrblitNavigation3D", callable_mp_static(_createOrblitNavigation3DCallback));
		NavigationSystem3DManager::get_singleton()->set_default_system("OrblitNavigation3D");
	}

#ifdef TOOLS_ENABLED
	if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		EditorPlugins::add_by_type<NavigationLink3DEditorPlugin>();
		EditorPlugins::add_by_type<NavigationRegion3DEditorPlugin>();
		EditorPlugins::add_by_type<NavigationObstacle3DEditorPlugin>();
	}
#endif
}

void uninitialize_navigation_spatial_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		return;
	}
}
