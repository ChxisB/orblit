/**************************************************************************/
/*  multi_entity_edit.h                                                   */

#pragma once

#include "core/object/ref_counted.h"

class MultiEntityEdit : public RefCounted {
	ORBLIT_CLASS(MultiEntityEdit, RefCounted);

	friend class EditorQuickOpenDialog;

	LocalVector<EntityPath> nodes;
	bool notify_property_list_changed_pending = false;
	struct PLData {
		int uses = 0;
		PropertyInfo info;
	};

	bool _set_impl(const StringName &p_name, const Variant &p_value, const String &p_field, bool p_undo_redo = true);
	void _queue_notify_property_list_changed();
	void _notify_property_list_changed();

protected:
	static void register_members();

	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

public:
	bool _hide_script_from_inspector() { return true; }
	bool _hide_metadata_from_inspector() { return true; }

	bool _property_can_revert(const StringName &p_name) const;
	bool _property_get_revert(const StringName &p_name, Variant &r_property) const;
	String _get_editor_name() const;

	void add_node(const EntityPath &p_node);

	int get_entity_count() const;
	EntityPath get_entity(int p_index) const;
	StringName get_edited_class_name() const;

	void set_property_field(const StringName &p_property, const Variant &p_value, const String &p_field);

	// If the nodes selected are the same independently of order then return true.
	bool is_same_selection(const MultiEntityEdit *p_other) const {
		if (get_entity_count() != p_other->get_entity_count()) {
			return false;
		}
		HashSet<EntityPath> nodes_in_selection;
		for (const EntityPath &node : p_other->nodes) {
			nodes_in_selection.insert(node);
		}
		for (const EntityPath &node : nodes) {
			if (!nodes_in_selection.has(node)) {
				return false;
			}
		}

		return true;
	}
};
