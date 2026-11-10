/**************************************************************************/
/*  multi_entity_edit.cpp                                                 */

#include "multi_entity_edit.h"

#include "core/math/math_fieldwise.h"
#include "core/object/callable_mp.h"
#include "core/object/class_registry.h"
#include "editor/editor_node.h"
#include "editor/editor_undo_redo_manager.h"
#include "scene/property_utils.h"

bool MultiEntityEdit::_set(const StringName &p_name, const Variant &p_value) {
	return _set_impl(p_name, p_value, "");
}

bool MultiEntityEdit::_set_impl(const StringName &p_name, const Variant &p_value, const String &p_field, bool p_undo_redo) {
	Entity *es = EditorNode::get_singleton()->get_edited_scene();
	if (!es) {
		return false;
	}

	String name = p_name;

	if (name == "scripts") { // Script set is intercepted at object level (check Variant Object::get()), so use a different name.
		name = "script";
	} else if (name.begins_with("Metadata/")) {
		name = name.replace_first("Metadata/", "metadata/");
	}

	Entity *node_path_target = nullptr;
	if (p_value.get_type() == Variant::ENTITY_PATH && p_value != EntityPath()) {
		node_path_target = es->get_entity(p_value);
	}

	EditorUndoRedoManager *ur = EditorUndoRedoManager::get_singleton();

	if (p_undo_redo) {
		ur->create_action(vformat(TTR("Set %s on %d nodes"), name, get_entity_count()), UndoRedo::MERGE_ENDS);
	}

	for (const EntityPath &E : nodes) {
		Entity *n = es->get_entity_or_null(E);
		if (!n) {
			continue;
		}

		Variant new_value;
		if (p_value.get_type() == Variant::ENTITY_PATH) {
			EntityPath path;
			if (node_path_target) {
				path = n->get_path_to(node_path_target);
			}

			if (p_undo_redo) {
				ur->add_do_property(n, name, path);
			} else {
				n->set(name, path);
			}
		} else {
			if (p_field.is_empty()) {
				// whole value
				new_value = p_value;
			} else {
				// only one field
				new_value = fieldwise_assign(n->get(name), p_value, p_field);
			}

			if (p_undo_redo) {
				ur->add_do_property(n, name, new_value);
			} else {
				n->set(name, new_value);
			}
		}

		if (p_undo_redo) {
			ur->add_undo_property(n, name, n->get(name));
			Variant old_value = n->get(p_name);
			Variant::Type type = old_value.get_type();
			if ((type == Variant::OBJECT || type == Variant::ARRAY || type == Variant::DICTIONARY) && old_value != new_value) {
				ur->add_do_method(EditorNode::get_singleton(), "update_node_reference", old_value, n, true);
				ur->add_do_method(EditorNode::get_singleton(), "update_node_reference", new_value, n, false);
				// Perhaps an inefficient way of updating the resource count.
				// We could go in depth and check which Resource values changed/got removed and which ones stayed the same, but this is more readable at the moment.
				ur->add_undo_method(EditorNode::get_singleton(), "update_node_reference", new_value, n, true);
				ur->add_undo_method(EditorNode::get_singleton(), "update_node_reference", old_value, n, false);
			}
		}
	}

	if (p_undo_redo) {
		ur->commit_action();
	}
	return true;
}

bool MultiEntityEdit::_get(const StringName &p_name, Variant &r_ret) const {
	Entity *es = EditorNode::get_singleton()->get_edited_scene();
	if (!es) {
		return false;
	}

	String name = p_name;
	if (name == "scripts") { // Script set is intercepted at object level (check Variant Object::get()), so use a different name.
		name = "script";
	} else if (name.begins_with("Metadata/")) {
		name = name.replace_first("Metadata/", "metadata/");
	}

	for (const EntityPath &E : nodes) {
		const Entity *n = es->get_entity_or_null(E);
		if (!n) {
			continue;
		}

		bool found;
		r_ret = n->get(name, &found);
		if (found) {
			return true;
		}
	}

	return false;
}

void MultiEntityEdit::_get_property_list(List<PropertyInfo> *p_list) const {
	HashMap<String, PLData> usage;

	Entity *es = EditorNode::get_singleton()->get_edited_scene();
	if (!es) {
		return;
	}

	int nc = 0;

	List<PLData *> data_list;

	for (const EntityPath &E : nodes) {
		Entity *n = es->get_entity_or_null(E);
		if (!n) {
			continue;
		}

		List<PropertyInfo> plist;
		n->get_property_list(&plist, true);

		for (PropertyInfo F : plist) {
			if (F.name == "script") {
				continue; // Added later manually, since this is intercepted before being set (check Variant Object::get()).
			} else if (F.name.begins_with("metadata/")) {
				F.name = F.name.replace_first("metadata/", "Metadata/"); // Trick to not get actual metadata edited from MultiEntityEdit.
			}

			PLData *usage_data = usage.getptr(F.name);
			if (!usage_data) {
				PLData pld;
				pld.uses = 0;
				pld.info = F;
				pld.info.name = F.name;
				HashMap<String, MultiEntityEdit::PLData>::Iterator I = usage.insert(F.name, pld);
				usage_data = &I->value;
				data_list.push_back(usage_data);
			}

			// Make sure only properties with matching PropertyInfo data will appear.
			if (usage_data->info.name == F.name &&
					usage_data->info.type == F.type &&
					usage_data->info.class_name == F.class_name &&
					usage_data->info.hint == F.hint &&
					usage_data->info.hint_string == F.hint_string) {
				if (usage_data->info.usage != F.usage) {
					// Checkable properties (mostly theme items) need special treatment.
					if (usage_data->info.usage & PROPERTY_USAGE_CHECKABLE && F.usage & PROPERTY_USAGE_CHECKABLE) {
						if (usage_data->info.usage & PROPERTY_USAGE_CHECKED) {
							F.usage |= PROPERTY_USAGE_STORAGE | PROPERTY_USAGE_CHECKED;
						} else {
							F.usage &= ~(PROPERTY_USAGE_STORAGE | PROPERTY_USAGE_CHECKED);
						}

						if (usage_data->info.usage != F.usage) {
							continue;
						}
					}
				}

				usage_data->uses++;
			}
		}

		nc++;
	}

	for (const PLData *E : data_list) {
		if (nc == E->uses) {
			p_list->push_back(E->info);
		}
	}

	p_list->push_back(PropertyInfo(Variant::OBJECT, "scripts", PROPERTY_HINT_RESOURCE_TYPE, Script::get_static_class_name()));
}

String MultiEntityEdit::_get_editor_name() const {
	return vformat(TTR("%s (%d Selected)"), get_edited_class_name(), get_entity_count());
}

bool MultiEntityEdit::_property_can_revert(const StringName &p_name) const {
	Entity *es = EditorNode::get_singleton()->get_edited_scene();
	if (!es) {
		return false;
	}

	if (ClassRegistry::has_property(get_edited_class_name(), p_name)) {
		for (const EntityPath &E : nodes) {
			Entity *node = es->get_entity_or_null(E);
			if (node) {
				return true;
			}
		}

		return false;
	}

	// Don't show the revert button if the edited class doesn't have the property.
	return false;
}

bool MultiEntityEdit::_property_get_revert(const StringName &p_name, Variant &r_property) const {
	Entity *es = EditorNode::get_singleton()->get_edited_scene();
	if (!es) {
		return false;
	}

	for (const EntityPath &E : nodes) {
		Entity *node = es->get_entity_or_null(E);
		if (!node) {
			continue;
		}

		// Check if property can revert before using class default.
		if (node->property_can_revert(p_name)) {
			r_property = node->property_get_revert(p_name);
		} else {
			bool is_valid = false;
			r_property = PropertyUtils::get_property_default_value(node, p_name, &is_valid);
			if (!is_valid) {
				r_property = ClassRegistry::class_get_default_property_value(node->get_class_name(), p_name);
			}
		}
		return true;
	}

	return false;
}

void MultiEntityEdit::_queue_notify_property_list_changed() {
	if (notify_property_list_changed_pending) {
		return;
	}
	notify_property_list_changed_pending = true;
	callable_mp(this, &MultiEntityEdit::_notify_property_list_changed).call_deferred();
}

void MultiEntityEdit::_notify_property_list_changed() {
	notify_property_list_changed_pending = false;
	notify_property_list_changed();
}

void MultiEntityEdit::add_node(const EntityPath &p_node) {
	nodes.push_back(p_node);

	Entity *es = EditorNode::get_singleton()->get_edited_scene();
	if (es) {
		Entity *node = es->get_entity_or_null(p_node);
		if (node) {
			node->connect(CoreStringName(property_list_changed), callable_mp(this, &MultiEntityEdit::_queue_notify_property_list_changed));
		}
	}
}

int MultiEntityEdit::get_entity_count() const {
	return nodes.size();
}

EntityPath MultiEntityEdit::get_entity(int p_index) const {
	ERR_FAIL_INDEX_V(p_index, get_entity_count(), EntityPath());
	return nodes[p_index];
}

StringName MultiEntityEdit::get_edited_class_name() const {
	Entity *es = EditorNode::get_singleton()->get_edited_scene();
	if (!es) {
		return SNAME("Entity");
	}

	// Get the class name of the first node.
	StringName class_name;
	for (const EntityPath &E : nodes) {
		Entity *node = es->get_entity_or_null(E);
		if (!node) {
			continue;
		}

		class_name = node->get_class_name();
		break;
	}

	if (class_name == StringName()) {
		return SNAME("Entity");
	}

	bool check_again = true;
	while (check_again) {
		check_again = false;

		if (class_name == SNAME("Entity") || class_name == StringName()) {
			// All nodes inherit from Entity, so no need to continue checking.
			return SNAME("Entity");
		}

		// Check that all nodes inherit from class_name.
		for (const EntityPath &E : nodes) {
			Entity *node = es->get_entity_or_null(E);
			if (!node) {
				continue;
			}

			const StringName node_class_name = node->get_class_name();
			if (class_name == node_class_name || ClassRegistry::is_parent_class(node_class_name, class_name)) {
				// class_name is the same or a parent of the node's class.
				continue;
			}

			// class_name is not a parent of the node's class, so check again with the parent class.
			class_name = ClassRegistry::get_parent_class(class_name);
			check_again = true;
			break;
		}
	}

	return class_name;
}

void MultiEntityEdit::set_property_field(const StringName &p_property, const Variant &p_value, const String &p_field) {
	_set_impl(p_property, p_value, p_field);
}

void MultiEntityEdit::register_members() {
	ClassRegistry::bind_method("_hide_script_from_inspector", &MultiEntityEdit::_hide_script_from_inspector);
	ClassRegistry::bind_method("_hide_metadata_from_inspector", &MultiEntityEdit::_hide_metadata_from_inspector);
	ClassRegistry::bind_method("_get_editor_name", &MultiEntityEdit::_get_editor_name);
}
