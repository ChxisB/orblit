/**************************************************************************/
/*  multimesh_editor_plugin.h                                             */

#pragma once

#include "editor/plugins/editor_plugin.h"
#include "scene/spatial/multimesh_instance_spatial.h"
#include "scene/ui/slider.h"
#include "scene/ui/spin_box.h"

class AcceptDialog;
class ConfirmationDialog;
class MenuButton;
class OptionButton;
class StageDialog;

class MultiMeshEditor : public Control {
	ORBLIT_CLASS(MultiMeshEditor, Control);

	friend class MultiMeshEditorPlugin;

	AcceptDialog *err_dialog = nullptr;
	MenuButton *options = nullptr;
	MultiMeshInstance3D *_last_pp_node = nullptr;
	bool browsing_source = false;

	Panel *panel = nullptr;
	MultiMeshInstance3D *node = nullptr;

	LineEdit *surface_source = nullptr;
	LineEdit *mesh_source = nullptr;

	StageDialog *std = nullptr;

	ConfirmationDialog *populate_dialog = nullptr;
	OptionButton *populate_axis = nullptr;
	HSlider *populate_rotate_random = nullptr;
	HSlider *populate_tilt_random = nullptr;
	SpinBox *populate_scale_random = nullptr;
	SpinBox *populate_scale = nullptr;
	SpinBox *populate_amount = nullptr;

	enum Menu {
		MENU_OPTION_POPULATE
	};

	void _browsed(const EntityPath &p_path);
	void _menu_option(int);
	void _populate();
	void _browse(bool p_source);

protected:
	void _node_removed(Entity *p_node);

public:
	void edit(MultiMeshInstance3D *p_multimesh);
	MultiMeshEditor();
};

class MultiMeshEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(MultiMeshEditorPlugin, EditorPlugin);

	MultiMeshEditor *multimesh_editor = nullptr;

public:
	virtual String get_plugin_name() const override { return "MultiMesh"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	MultiMeshEditorPlugin();
};
