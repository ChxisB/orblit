/**************************************************************************/
/*  occluder_instance_spatial_editor_plugin.cpp                                */

#include "occluder_instance_spatial_editor_plugin.h"

#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "editor/editor_node.h"
#include "editor/editor_string_names.h"
#include "editor/widgets/editor_file_dialog.h"
#include "scene/spatial/occluder_instance_spatial.h"
#include "scene/stage/stage.h"

void OccluderInstance3DEditorPlugin::_bake_select_file(const String &p_file) {
	if (occluder_instance) {
		OccluderInstance3D::BakeError err;
		if (get_stage()->get_edited_scene_root() && get_stage()->get_edited_scene_root() == occluder_instance) {
			err = occluder_instance->bake_scene(occluder_instance, p_file);
		} else {
			err = occluder_instance->bake_scene(occluder_instance->get_parent(), p_file);
		}

		switch (err) {
			case OccluderInstance3D::BAKE_ERROR_NO_SAVE_PATH: {
				String scene_path = occluder_instance->get_scene_file_path();
				if (scene_path.is_empty()) {
					scene_path = occluder_instance->get_owner()->get_scene_file_path();
				}
				if (scene_path.is_empty()) {
					EditorNode::get_singleton()->show_warning(TTR("Can't determine a save path for the occluder.\nSave your scene and try again."));
					break;
				}
				scene_path = scene_path.get_basename() + ".occ";

				file_dialog->set_current_path(scene_path);
				file_dialog->popup_file_dialog();

			} break;
			case OccluderInstance3D::BAKE_ERROR_NO_MESHES: {
				EditorNode::get_singleton()->show_warning(TTR("No meshes to bake.\nMake sure there is at least one MeshInstance3D node in the scene whose visual layers are part of the OccluderInstance3D's Bake Mask property."));
				break;
			}
			case OccluderInstance3D::BAKE_ERROR_CANT_SAVE: {
				EditorNode::get_singleton()->show_warning(TTR("Could not save the new occluder at the specified path:") + " " + p_file);
				break;
			}
			default: {
			}
		}
	}
}

void OccluderInstance3DEditorPlugin::_bake() {
	_bake_select_file("");
}

void OccluderInstance3DEditorPlugin::edit(Object *p_object) {
	OccluderInstance3D *s = Object::cast_to<OccluderInstance3D>(p_object);
	if (!s) {
		return;
	}

	occluder_instance = s;
}

bool OccluderInstance3DEditorPlugin::handles(Object *p_object) const {
	return p_object->is_class("OccluderInstance3D");
}

void OccluderInstance3DEditorPlugin::make_visible(bool p_visible) {
	if (p_visible) {
		bake->show();
	} else {
		bake->hide();
	}
}

void OccluderInstance3DEditorPlugin::register_members() {
	ClassRegistry::bind_method("_bake", &OccluderInstance3DEditorPlugin::_bake);
}

OccluderInstance3DEditorPlugin::OccluderInstance3DEditorPlugin() {
	bake = memnew(Button);
	bake->set_theme_type_variation(SceneStringName(FlatButton));
	bake->set_button_icon(EditorNode::get_singleton()->get_editor_theme()->get_icon(SNAME("Bake"), EditorStringName(EditorIcons)));
	bake->set_text(TTR("Bake Occluders"));
	bake->hide();
	bake->connect(SceneStringName(pressed), Callable(this, "_bake"));
	add_control_to_container(CONTAINER_SPATIAL_EDITOR_MENU, bake);
	occluder_instance = nullptr;

	file_dialog = memnew(EditorFileDialog);
	file_dialog->set_file_mode(EditorFileDialog::FILE_MODE_SAVE_FILE);
	file_dialog->add_filter("*.occ", "Occluder3D");
	file_dialog->set_title(TTR("Select occluder bake file:"));
	file_dialog->connect("file_selected", callable_mp(this, &OccluderInstance3DEditorPlugin::_bake_select_file));
	bake->add_child(file_dialog);
}
