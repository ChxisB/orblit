/**************************************************************************/
/*  particles_planar_editor_plugin.h                                          */

#pragma once

#include "editor/scene/particles_editor_plugin.h"

class EditorFileDialog;
class MarginContainer;
class LineEdit;

class Particles2DEditorPlugin : public ParticlesEditorPlugin {
	ORBLIT_CLASS(Particles2DEditorPlugin, ParticlesEditorPlugin);

protected:
	enum {
		MENU_LOAD_EMISSION_MASK = 100,
	};

	HashSet<ObjectID> selected_particles;

	enum EmissionMode {
		EMISSION_MODE_SOLID,
		EMISSION_MODE_BORDER,
		EMISSION_MODE_BORDER_DIRECTED
	};

	enum MaskMode {
		MASK_MODE_SOLID,
		MASK_MODE_BORDER,
	};

	enum DirectionMode {
		DIRECTION_MODE_NONE,
		DIRECTION_MODE_GENERATE,
		DIRECTION_MODE_TEXTURE,
	};

	enum TextureType {
		TEXTURE_TYPE_MASK,
		TEXTURE_TYPE_DIRECTION,
	};

	EditorFileDialog *file_dialog = nullptr;
	ConfirmationDialog *emission_mask_dialog = nullptr;
	OptionButton *emission_mask_mode = nullptr;
	OptionButton *emission_direction_mode = nullptr;
	CheckBox *emission_mask_centered = nullptr;
	CheckBox *emission_mask_colors = nullptr;
	LineEdit *mask_img_path_line_edit = nullptr;
	LineEdit *direction_img_path_line_edit = nullptr;
	HBoxContainer *direction_img_hbox = nullptr;
	Label *direction_img_label = nullptr;
	Button *mask_browse_button = nullptr;
	Button *direction_browse_button = nullptr;
	Label *error_message = nullptr;
	TextureType browsing_texture_type = TEXTURE_TYPE_MASK;

	virtual void _menu_callback(int p_idx) override;
	virtual void _add_menu_options(PopupMenu *p_menu) override;

	void _validate_textures();
	void _mask_img_path_line_edit_text_changed(const String &p_text);
	void _direction_img_path_line_edit_text_changed(const String &p_text);
	void _emission_mask_mode_item_changed(int p_idx) const;
	void _emission_direction_mode_item_changed(int p_idx);
	void _browse_mask_texture_pressed();
	void _browse_direction_texture_pressed();
	void _file_selected(const String &p_file);
	void _process_emission_masks(PackedVector2Array &r_valid_positions, PackedVector2Array &r_valid_normals, PackedByteArray &r_valid_colors, Vector2i &r_image_size);
	virtual void _generate_emission_mask() = 0;
	void _notification(int p_what);
	void _theme_changed();
	void _set_show_gizmos(Entity *p_node, bool p_show);
	void _selection_changed();
	void _node_removed(Entity *p_node);

public:
	Particles2DEditorPlugin();
};

class GPUParticles2DEditorPlugin : public Particles2DEditorPlugin {
	ORBLIT_CLASS(GPUParticles2DEditorPlugin, Particles2DEditorPlugin);

	enum {
		MENU_GENERATE_VISIBILITY_RECT = 200,
	};

	ConfirmationDialog *generate_visibility_rect = nullptr;
	SpinBox *generate_seconds = nullptr;

	void _generate_visibility_rect();

protected:
	void _menu_callback(int p_idx) override;
	void _add_menu_options(PopupMenu *p_menu) override;

	Entity *_convert_particles() override;

	void _generate_emission_mask() override;

public:
	GPUParticles2DEditorPlugin();
};

class CPUParticles2DEditorPlugin : public Particles2DEditorPlugin {
	ORBLIT_CLASS(CPUParticles2DEditorPlugin, Particles2DEditorPlugin);

protected:
	Entity *_convert_particles() override;

	void _generate_emission_mask() override;

public:
	CPUParticles2DEditorPlugin();
};
