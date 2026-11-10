/**************************************************************************/
/*  particles_editor_plugin.cpp                                           */

#include "particles_editor_plugin.h"

#include "core/object/callable_mp.h"
#include "editor/docks/stage_dock.h"
#include "editor/editor_undo_redo_manager.h"
#include "editor/settings/editor_settings.h"
#include "scene/ui/box_container.h"
#include "scene/ui/menu_button.h"
#include "scene/ui/spin_box.h"

void ParticlesEditorPlugin::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_ENTER_STAGE: {
			if (handled_type.ends_with("2D")) {
				add_control_to_container(CONTAINER_CANVAS_EDITOR_MENU, toolbar);
			} else if (handled_type.ends_with("3D")) {
				add_control_to_container(CONTAINER_SPATIAL_EDITOR_MENU, toolbar);
			} else {
				DEV_ASSERT(false);
			}

			menu->set_button_icon(menu->get_editor_theme_icon(handled_type));
			menu->set_text(handled_type);

			PopupMenu *popup = menu->get_popup();
			popup->add_shortcut(ED_SHORTCUT("particles/restart_emission", TTRC("Restart Emission"), KeyModifierMask::CTRL | Key::R), MENU_RESTART);
			_add_menu_options(popup);
			popup->add_item(conversion_option_name, MENU_OPTION_CONVERT);
		} break;
	}
}

bool ParticlesEditorPlugin::need_show_lifetime_dialog(SpinBox *p_seconds) {
	// Add one second to the default generation lifetime, since the progress is updated every second.
	p_seconds->set_value(MAX(1.0, std::trunc(edited_node->get("lifetime").operator double()) + 1.0));

	if (p_seconds->get_value() >= 11.0 + CMP_EPSILON) {
		// Only pop up the time dialog if the particle's lifetime is long enough to warrant shortening it.
		return true;
	} else {
		// Generate the visibility rect/AABB immediately.
		return false;
	}
}

void ParticlesEditorPlugin::_menu_callback(int p_idx) {
	switch (p_idx) {
		case MENU_OPTION_CONVERT: {
			Entity *converted_node = _convert_particles();

			EditorUndoRedoManager *ur = EditorUndoRedoManager::get_singleton();
			ur->create_action(conversion_option_name, UndoRedo::MERGE_DISABLE, edited_node);
			StageDock::get_singleton()->replace_node(edited_node, converted_node);
			ur->commit_action(false);
		} break;

		case MENU_RESTART: {
			edited_node->call("restart");
		}
	}
}

void ParticlesEditorPlugin::edit(Object *p_object) {
	edited_node = Object::cast_to<Entity>(p_object);
}

bool ParticlesEditorPlugin::handles(Object *p_object) const {
	return p_object->is_class(handled_type);
}

void ParticlesEditorPlugin::make_visible(bool p_visible) {
	toolbar->set_visible(p_visible);
}

ParticlesEditorPlugin::ParticlesEditorPlugin() {
	toolbar = memnew(HBoxContainer);
	toolbar->hide();

	menu = memnew(MenuButton);
	menu->set_switch_on_hover(true);
	menu->set_flat(false);
	menu->set_theme_type_variation("FlatMenuButtonNoIconTint");
	toolbar->add_child(menu);
	menu->get_popup()->connect(SceneStringName(id_pressed), callable_mp(this, &ParticlesEditorPlugin::_menu_callback));
}
