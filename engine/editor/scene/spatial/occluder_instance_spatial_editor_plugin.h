/**************************************************************************/
/*  occluder_instance_spatial_editor_plugin.h                                  */

#pragma once

#include "editor/plugins/editor_plugin.h"

class EditorFileDialog;
class OccluderInstance3D;

class OccluderInstance3DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(OccluderInstance3DEditorPlugin, EditorPlugin);

	OccluderInstance3D *occluder_instance = nullptr;

	Button *bake = nullptr;

	EditorFileDialog *file_dialog = nullptr;

	void _bake_select_file(const String &p_file);
	void _bake();

protected:
	static void register_members();

public:
	virtual String get_plugin_name() const override { return "OccluderInstance3D"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	OccluderInstance3DEditorPlugin();
};
