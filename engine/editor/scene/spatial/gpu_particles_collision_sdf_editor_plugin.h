/**************************************************************************/
/*  gpu_particles_collision_sdf_editor_plugin.h                           */

#pragma once

#include "editor/plugins/editor_plugin.h"

struct EditorProgress;
class EditorFileDialog;
class GPUParticlesCollisionSDF3D;
class HBoxContainer;

class GPUParticlesCollisionSDF3DEditorPlugin : public EditorPlugin {
	ORBLIT_CLASS(GPUParticlesCollisionSDF3DEditorPlugin, EditorPlugin);

	GPUParticlesCollisionSDF3D *col_sdf = nullptr;

	HBoxContainer *bake_hb = nullptr;
	Button *bake = nullptr;

	EditorFileDialog *probe_file = nullptr;

	static EditorProgress *tmp_progress;
	static void bake_func_begin(int p_steps);
	static void bake_func_step(int p_step, const String &p_description);
	static void bake_func_end();

	void _bake();
	void _sdf_save_path_and_bake(const String &p_path);

protected:
	void _notification(int p_what);

public:
	virtual String get_plugin_name() const override { return "GPUParticlesCollisionSDF3D"; }
	virtual void edit(Object *p_object) override;
	virtual bool handles(Object *p_object) const override;
	virtual void make_visible(bool p_visible) override;

	GPUParticlesCollisionSDF3DEditorPlugin();
};
