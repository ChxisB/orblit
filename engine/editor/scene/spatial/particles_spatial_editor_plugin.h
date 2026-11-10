/**************************************************************************/
/*  particles_spatial_editor_plugin.h                                          */

#pragma once

#include "editor/scene/particles_editor_plugin.h"

class Particles3DEditorPlugin : public ParticlesEditorPlugin {
	ORBLIT_CLASS(Particles3DEditorPlugin, ParticlesEditorPlugin);

	enum {
		MENU_OPTION_GENERATE_AABB = 300,
		MENU_OPTION_CREATE_EMISSION_VOLUME_FROM_NODE,
	};

	ConfirmationDialog *generate_aabb = nullptr;
	SpinBox *generate_seconds = nullptr;

	StageDialog *emission_tree_dialog = nullptr;
	ConfirmationDialog *emission_dialog = nullptr;
	SpinBox *emission_amount = nullptr;
	OptionButton *emission_fill = nullptr;

	void _generate_aabb();
	void _node_selected(const EntityPath &p_path);

protected:
	Vector<Face3> geometry;

	virtual void _menu_callback(int p_idx) override;
	virtual void _add_menu_options(PopupMenu *p_menu) override;

	bool _generate(Vector<Vector3> &r_points, Vector<Vector3> &r_normals);
	virtual bool _can_generate_points() const = 0;
	virtual void _generate_emission_points() = 0;

public:
	Particles3DEditorPlugin();
};

class GPUParticles3DEditorPlugin : public Particles3DEditorPlugin {
	ORBLIT_CLASS(GPUParticles3DEditorPlugin, Particles3DEditorPlugin);

protected:
	Entity *_convert_particles() override;

	bool _can_generate_points() const override;
	void _generate_emission_points() override;

public:
	GPUParticles3DEditorPlugin();
};

class CPUParticles3DEditorPlugin : public Particles3DEditorPlugin {
	ORBLIT_CLASS(CPUParticles3DEditorPlugin, Particles3DEditorPlugin);

protected:
	Entity *_convert_particles() override;

	bool _can_generate_points() const override { return true; }
	void _generate_emission_points() override;

public:
	CPUParticles3DEditorPlugin();
};
