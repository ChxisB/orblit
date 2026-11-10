/**************************************************************************/
/*  noise_editor_plugin.cpp                                               */

#include "noise_editor_plugin.h"

#include "../noise.h"
#include "../noise_texture_planar.h"

#include "core/object/callable_mp.h"
#include "editor/inspector/editor_inspector.h"
#include "editor/themes/editor_scale.h"
#include "scene/ui/button.h"
#include "scene/ui/texture_rect.h"

class NoisePreview : public Control {
	ORBLIT_CLASS(NoisePreview, Control)

	static const int PREVIEW_HEIGHT = 150;
	static const int PADDING_3D_SPACE_SWITCH = 2;

	Ref<Noise> _noise;
	Size2i _preview_texture_size;

	TextureRect *_texture_rect = nullptr;
	Button *_3d_space_switch = nullptr;

public:
	NoisePreview() {
		set_custom_minimum_size(Size2(0, EDSCALE * PREVIEW_HEIGHT));

		_texture_rect = memnew(TextureRect);
		_texture_rect->set_anchors_and_offsets_preset(Control::PRESET_FULL_RECT);
		_texture_rect->set_stretch_mode(TextureRect::STRETCH_KEEP_ASPECT_COVERED);
		add_child(_texture_rect);

		_3d_space_switch = memnew(Button);
		_3d_space_switch->set_text(TTR("3D"));
		_3d_space_switch->set_tooltip_text(TTR("Toggles whether the noise preview is computed in 3D space."));
		_3d_space_switch->set_toggle_mode(true);
		_3d_space_switch->set_offset(SIDE_LEFT, PADDING_3D_SPACE_SWITCH);
		_3d_space_switch->set_offset(SIDE_TOP, PADDING_3D_SPACE_SWITCH);
		_3d_space_switch->connect(SceneStringName(pressed), callable_mp(this, &NoisePreview::_on_3d_button_pressed));
		add_child(_3d_space_switch);
	}

	void set_noise(Ref<Noise> noise) {
		if (_noise == noise) {
			return;
		}
		_noise = noise;
		if (_noise.is_valid()) {
			if (_noise->has_meta("_preview_in_3d_space_")) {
				_3d_space_switch->set_pressed(true);
			}

			update_preview();
		}
	}

private:
	void _on_3d_button_pressed() {
		if (_3d_space_switch->is_pressed()) {
			_noise->set_meta("_preview_in_3d_space_", true);
		} else {
			_noise->remove_meta("_preview_in_3d_space_");
		}
		update_preview();
	}

	void _notification(int p_what) {
		switch (p_what) {
			case NOTIFICATION_RESIZED: {
				_preview_texture_size = get_size();
				update_preview();
			} break;
		}
	}

	void update_preview() {
		if (MIN(_preview_texture_size.width, _preview_texture_size.height) > 0) {
			Ref<NoiseTexture2D> tex;
			tex.instantiate();
			tex->set_width(_preview_texture_size.width);
			tex->set_height(_preview_texture_size.height);
			tex->set_in_3d_space(_3d_space_switch->is_pressed());
			tex->set_noise(_noise);
			_texture_rect->set_texture(tex);
		}
	}
};

/////////////////////////////////////////////////////////////////////////////////

class NoiseEditorInspectorPlugin : public EditorInspectorPlugin {
	ORBLIT_CLASS(NoiseEditorInspectorPlugin, EditorInspectorPlugin)
public:
	bool can_handle(Object *p_object) override {
		return Object::cast_to<Noise>(p_object) != nullptr;
	}

	void parse_begin(Object *p_object) override {
		Noise *noise_ptr = Object::cast_to<Noise>(p_object);
		if (noise_ptr) {
			Ref<Noise> noise(noise_ptr);

			NoisePreview *viewer = memnew(NoisePreview);
			viewer->set_noise(noise);
			add_custom_control(viewer);
		}
	}
};

/////////////////////////////////////////////////////////////////////////////////

String NoiseEditorPlugin::get_plugin_name() const {
	return Noise::get_static_class_name();
}

NoiseEditorPlugin::NoiseEditorPlugin() {
	Ref<NoiseEditorInspectorPlugin> plugin;
	plugin.instantiate();
	add_inspector_plugin(plugin);
}
