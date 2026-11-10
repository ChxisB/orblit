/**************************************************************************/
/*  navigation_link_spatial_editor_plugin.h                                    */

#pragma once

#include "navigation_link_spatial_gizmo_plugin.h"

#include "editor/plugins/editor_plugin.h"

class NavigationLink3DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(NavigationLink3DEditorPlugin, EditorPlugin);

	Ref<NavigationLink3DGizmoPlugin> gizmo_plugin;

public:
	virtual String get_plugin_name() const override { return "NavigationLink3D"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;

	NavigationLink3DEditorPlugin();
};
