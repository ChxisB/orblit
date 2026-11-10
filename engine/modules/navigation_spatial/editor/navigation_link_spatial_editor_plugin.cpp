/**************************************************************************/
/*  navigation_link_spatial_editor_plugin.cpp                                  */

#include "navigation_link_spatial_editor_plugin.h"

#include "editor/scene/spatial/entity_spatial_editor_plugin.h"
#include "scene/spatial/navigation/navigation_link_spatial.h"

void NavigationLink3DEditorPlugin::edit(Object *p_object) {
}

bool NavigationLink3DEditorPlugin::handles(Object *p_object) const {
	return Object::cast_to<NavigationLink3D>(p_object) != nullptr;
}

NavigationLink3DEditorPlugin::NavigationLink3DEditorPlugin() {
	gizmo_plugin.instantiate();
	Entity3DEditor::get_singleton()->add_gizmo_plugin(gizmo_plugin);
}
