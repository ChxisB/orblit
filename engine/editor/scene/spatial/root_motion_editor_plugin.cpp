/**************************************************************************/
/*  root_motion_editor_plugin.cpp                                         */

#include "root_motion_editor_plugin.h"

#include "core/object/callable_mp.h"
#include "editor/editor_node.h"
#include "editor/themes/editor_scale.h"
#include "scene/animation/animation_mixer.h"
#include "scene/spatial/skeleton_spatial.h"
#include "scene/ui/button.h"
#include "scene/ui/dialogs.h"
#include "scene/ui/tree.h"

void EditorPropertyRootMotion::_confirmed() {
	TreeItem *ti = filters->get_selected();
	if (!ti) {
		return;
	}

	EntityPath path = ti->get_metadata(0);
	emit_changed(get_edited_property(), path);
	update_property();
	filter_dialog->hide(); //may come from activated
}

void EditorPropertyRootMotion::_node_assign() {
	AnimationMixer *mixer = Object::cast_to<AnimationMixer>(get_edited_object());
	if (!mixer) {
		EditorNode::get_singleton()->show_warning(TTR("Path to AnimationMixer is invalid"));
		return;
	}

	Entity *base = mixer->get_entity(mixer->get_root_node());

	if (!base) {
		EditorNode::get_singleton()->show_warning(TTR("AnimationMixer has no valid root node path, so unable to retrieve track names."));
		return;
	}

	HashSet<String> paths;
	{
		for (const StringName &E : mixer->get_sorted_animation_list()) {
			Ref<Animation> anim = mixer->get_animation(E);
			for (int i = 0; i < anim->get_track_count(); i++) {
				String pathname = anim->track_get_path(i).get_concatenated_names();
				if (!paths.has(pathname)) {
					paths.insert(pathname);
				}
			}
		}
	}

	filters->clear();
	TreeItem *root = filters->create_item();

	HashMap<String, TreeItem *> parenthood;

	for (const String &E : paths) {
		EntityPath path = E;
		TreeItem *ti = nullptr;
		String accum;
		for (int i = 0; i < path.get_name_count(); i++) {
			String name = path.get_name(i);
			if (!accum.is_empty()) {
				accum += "/";
			}
			accum += name;
			if (!parenthood.has(accum)) {
				if (ti) {
					ti = filters->create_item(ti);
				} else {
					ti = filters->create_item(root);
				}
				parenthood[accum] = ti;
				ti->set_text(0, name);
				ti->set_selectable(0, false);
				ti->set_editable(0, false);

				if (base->has_entity(accum)) {
					Entity *node = base->get_entity(accum);
					ti->set_icon(0, EditorNode::get_singleton()->get_object_icon(node));
				}

			} else {
				ti = parenthood[accum];
			}
		}

		Entity *node = nullptr;
		if (base->has_entity(accum)) {
			node = base->get_entity(accum);
		}
		if (!node) {
			continue; //no node, can't edit
		}

		Skeleton3D *skeleton = Object::cast_to<Skeleton3D>(node);
		if (skeleton) {
			HashMap<int, TreeItem *> items;
			items.insert(-1, ti);
			Ref<Texture> bone_icon = get_editor_theme_icon(SNAME("Bone"));
			Vector<int> bones_to_process = skeleton->get_parentless_bones();
			while (bones_to_process.size() > 0) {
				int current_bone_idx = bones_to_process[0];
				bones_to_process.erase(current_bone_idx);

				Vector<int> current_bone_child_bones = skeleton->get_bone_children(current_bone_idx);
				int child_bone_size = current_bone_child_bones.size();
				for (int i = 0; i < child_bone_size; i++) {
					bones_to_process.push_back(current_bone_child_bones[i]);
				}

				const int parent_idx = skeleton->get_bone_parent(current_bone_idx);
				TreeItem *parent_item = items.find(parent_idx)->value;

				TreeItem *joint_item = filters->create_item(parent_item);
				items.insert(current_bone_idx, joint_item);

				joint_item->set_text(0, skeleton->get_bone_name(current_bone_idx));
				joint_item->set_icon(0, bone_icon);
				joint_item->set_selectable(0, true);
				joint_item->set_metadata(0, accum + ":" + skeleton->get_bone_name(current_bone_idx));
				joint_item->set_collapsed(true);
			}
		}
	}

	filters->ensure_cursor_is_visible();
	filter_dialog->popup_centered(Size2(500, 500) * EDSCALE);
}

void EditorPropertyRootMotion::_node_clear() {
	emit_changed(get_edited_property(), EntityPath());
	update_property();
}

void EditorPropertyRootMotion::update_property() {
	EntityPath p = get_edited_property_value();
	assign->set_tooltip_text(String(p));
	if (p == EntityPath()) {
		assign->set_button_icon(Ref<Texture2D>());
		assign->set_text(TTR("Assign..."));
		assign->set_flat(false);
		return;
	}

	assign->set_button_icon(Ref<Texture2D>());
	assign->set_text(String(p));
}

void EditorPropertyRootMotion::setup(const EntityPath &p_base_hint) {
	base_hint = p_base_hint;
}

void EditorPropertyRootMotion::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_THEME_CHANGED: {
			Ref<Texture2D> t = get_editor_theme_icon(SNAME("Clear"));
			clear->set_button_icon(t);
		} break;
	}
}

EditorPropertyRootMotion::EditorPropertyRootMotion() {
	HBoxContainer *hbc = memnew(HBoxContainer);
	add_child(hbc);
	assign = memnew(Button);
	assign->set_accessibility_name(TTRC("Assign"));
	assign->set_h_size_flags(SIZE_EXPAND_FILL);
	assign->set_clip_text(true);
	assign->connect(SceneStringName(pressed), callable_mp(this, &EditorPropertyRootMotion::_node_assign));
	hbc->add_child(assign);

	clear = memnew(Button);
	clear->set_accessibility_name(TTRC("Clear"));
	clear->connect(SceneStringName(pressed), callable_mp(this, &EditorPropertyRootMotion::_node_clear));
	hbc->add_child(clear);

	filter_dialog = memnew(ConfirmationDialog);
	add_child(filter_dialog);
	filter_dialog->set_title(TTR("Edit Filtered Tracks:"));
	filter_dialog->connect(SceneStringName(confirmed), callable_mp(this, &EditorPropertyRootMotion::_confirmed));

	filters = memnew(Tree);
	filter_dialog->add_child(filters);
	filters->set_auto_translate_mode(AUTO_TRANSLATE_MODE_DISABLED);
	filters->set_v_size_flags(SIZE_EXPAND_FILL);
	filters->set_hide_root(true);
	filters->connect("item_activated", callable_mp(this, &EditorPropertyRootMotion::_confirmed));
	//filters->connect("item_edited", this, "_filter_edited");
}

//////////////////////////

bool EditorInspectorRootMotionPlugin::can_handle(Object *p_object) {
	return true; // Can handle everything.
}

bool EditorInspectorRootMotionPlugin::parse_property(Object *p_object, const Variant::Type p_type, const String &p_path, const PropertyHint p_hint, const String &p_hint_text, const BitField<PropertyUsageFlags> p_usage, const bool p_wide) {
	if (p_path == "root_motion_track" && p_object->is_class("AnimationMixer") && p_type == Variant::ENTITY_PATH) {
		EditorPropertyRootMotion *editor = memnew(EditorPropertyRootMotion);
		add_property_editor(p_path, editor);
		return true;
	}

	return false;
}
