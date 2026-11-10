/**************************************************************************/
/*  register_types.cpp                                                    */

#include "register_types.h"

#include "planar/navigation_system_planar.h"

#include "core/object/callable_mp.h"
#include "systems/navigation_planar/navigation_system_planar.h"
#include "systems/navigation_planar/navigation_system_planar_manager.h"

#ifdef TOOLS_ENABLED
#include "editor/navigation_link_planar_editor_plugin.h"
#include "editor/navigation_obstacle_planar_editor_plugin.h"
#include "editor/navigation_region_planar_editor_plugin.h"
#endif

static NavigationSystem2D *_createOrblitNavigation2DCallback() {
	return memnew(OrblitNavigationSystem2D);
}

void initialize_navigation_planar_module(ModuleInitializationLevel p_level) {
	if (p_level == MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		NavigationSystem2DManager::get_singleton()->register_system("OrblitNavigation2D", callable_mp_static(_createOrblitNavigation2DCallback));
		NavigationSystem2DManager::get_singleton()->set_default_system("OrblitNavigation2D");
	}

#ifdef TOOLS_ENABLED
	if (p_level == MODULE_INITIALIZATION_LEVEL_EDITOR) {
		EditorPlugins::add_by_type<NavigationLink2DEditorPlugin>();
		EditorPlugins::add_by_type<NavigationRegion2DEditorPlugin>();
		EditorPlugins::add_by_type<NavigationObstacle2DEditorPlugin>();
	}
#endif
}

void uninitialize_navigation_planar_module(ModuleInitializationLevel p_level) {
	if (p_level != MODULE_INITIALIZATION_LEVEL_SYSTEMS) {
		return;
	}
}
