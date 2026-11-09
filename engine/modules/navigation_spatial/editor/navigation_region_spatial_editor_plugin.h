/**************************************************************************/
/*  navigation_region_spatial_editor_plugin.h                                  */

#pragma once

#include "navigation_region_spatial_gizmo_plugin.h"

#include "editor/plugins/editor_plugin.h"

class AcceptDialog;
class Button;
class ConfirmationDialog;
class HBoxContainer;
class Label;
class NavigationRegion3D;

class NavigationRegion3DEditor : public Control {
	friend class NavigationRegion3DEditorPlugin;

	ORBLIT_CLASS(NavigationRegion3DEditor, Control);

	AcceptDialog *err_dialog = nullptr;
	ConfirmationDialog *multibake_dialog = nullptr;

	HBoxContainer *bake_hbox = nullptr;
	Button *button_bake = nullptr;
	Button *button_reset = nullptr;
	Label *bake_info = nullptr;

	LocalVector<NavigationRegion3D *> selected_regions;

	LocalVector<NavigationRegion3D *> regions_to_bake;
	LocalVector<NavigationRegion3D *> regions_with_navmesh_to_bake;

	int processed_regions_to_bake_count = 0;
	int processed_regions_to_bake_count_max = 0;
	bool region_baking_canceled = false;
	NavigationRegion3D *currently_baking_region = nullptr;

	bool bake_in_process = false;

	void _bake_pressed();
	void _clear_pressed();

	void _on_navmesh_multibake_confirmed();
	void _on_navmesh_multibake_canceled();
	void _process_regions_to_bake();

protected:
	void _node_removed(Entity *p_node);
	void _notification(int p_what);

public:
	void edit(LocalVector<NavigationRegion3D *> p_regions);
	NavigationRegion3DEditor();
};

class NavigationRegion3DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(NavigationRegion3DEditorPlugin, EditorPlugin);

	NavigationRegion3DEditor *navigation_region_editor = nullptr;

	Ref<NavigationRegion3DGizmoPlugin> gizmo_plugin;

public:
	virtual String get_plugin_name() const override { return "NavigationRegion3D"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	NavigationRegion3DEditorPlugin();
};
