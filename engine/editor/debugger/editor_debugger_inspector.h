/**************************************************************************/
/*  editor_debugger_inspector.h                                           */

#pragma once

#include "editor/inspector/editor_inspector.h"

class SceneDebuggerObject;

template <typename K, typename V>
class TypedDictionary;

class EditorDebuggerRemoteObjects : public Object {
	ORBLIT_CLASS(EditorDebuggerRemoteObjects, Object);

private:
	bool _set_impl(const StringName &p_name, const Variant &p_value, const String &p_field);

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;
	static void register_members();

public:
	TypedArray<uint64_t> remote_object_ids;
	String type_name;
	String node_name; // For human-readable name.
	List<PropertyInfo> prop_list;
	HashMap<StringName, TypedDictionary<uint64_t, Variant>> prop_values;
	int debugger_id = 0;
	bool has_canvas_items = false;

	bool _hide_script_from_inspector() { return true; }
	bool _hide_metadata_from_inspector() { return true; }

	void set_property_field(const StringName &p_property, const Variant &p_value, const String &p_field);
	String get_title();
	Variant get_variant(const StringName &p_name);

	void clear();

	void update() { notify_property_list_changed(); }
};

class EditorDebuggerInspector : public EditorInspector {
	ORBLIT_CLASS(EditorDebuggerInspector, EditorInspector);

private:
	LocalVector<EditorDebuggerRemoteObjects *> remote_objects_list;
	HashSet<Ref<Resource>> remote_dependencies;
	EditorDebuggerRemoteObjects *variables = nullptr;

	void _object_selected(ObjectID p_object);
	void _objects_edited(const String &p_prop, const TypedDictionary<uint64_t, Variant> &p_values, const String &p_field);

protected:
	void _notification(int p_what);
	static void register_members();

public:
	EditorDebuggerInspector();
	~EditorDebuggerInspector();

	// Remote Object cache
	EditorDebuggerRemoteObjects *set_objects(const Array &p_array, int p_debugger_id);
	void add_undo_redo_action(const Array &p_array);
	void clear_remote_inspector();
	void clear_cache();
	void invalidate_selection_from_cache(const TypedArray<uint64_t> &p_ids);

	// Stack Dump variables
	String get_stack_variable(const String &p_var);
	void add_stack_variable(const Array &p_arr, int p_offset = -1);
	void clear_stack_variables();
};
