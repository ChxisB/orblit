/**************************************************************************/
/*  scene_debugger_object.h                                               */

#pragma once

#ifdef DEBUG_ENABLED

#include "core/containers/list.h"
#include "core/containers/pair.h"
#include "core/object/object.h"
#include "core/string/ustring.h"

class Entity;
class Script;

class SceneDebuggerObject {
private:
	void _parse_script_properties(Script *p_script, ScriptInstance *p_instance);

public:
	typedef Pair<PropertyInfo, Variant> SceneDebuggerProperty;
	ObjectID id;
	String class_name;
	List<SceneDebuggerProperty> properties;

	SceneDebuggerObject(ObjectID p_id);
	SceneDebuggerObject(Object *p_obj);
	SceneDebuggerObject() {}

	void serialize(Array &r_arr, int p_max_size = 1 << 20);
	void deserialize(const Array &p_arr);
	void deserialize(uint64_t p_id, const String &p_class_name, const Array &p_props);
};

class SceneDebuggerTree {
public:
	struct RemoteNode {
		int child_count = 0;
		String name;
		String type_name;
		ObjectID id;
		String scene_file_path;
		uint8_t view_flags = 0;

		enum ViewFlags {
			VIEW_HAS_VISIBLE_METHOD = 1 << 1,
			VIEW_VISIBLE = 1 << 2,
			VIEW_VISIBLE_IN_TREE = 1 << 3,
		};

		RemoteNode(int p_child, const String &p_name, const String &p_type, ObjectID p_id, const String p_scene_file_path, int p_view_flags) {
			child_count = p_child;
			name = p_name;
			type_name = p_type;
			id = p_id;

			scene_file_path = p_scene_file_path;
			view_flags = p_view_flags;
		}

		RemoteNode() {}
	};

	List<RemoteNode> nodes;

	void serialize(Array &r_arr);
	void deserialize(const Array &p_arr);
	SceneDebuggerTree(Entity *p_root);
	SceneDebuggerTree() {}
};
#endif // DEBUG_ENABLED
