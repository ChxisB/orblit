/**************************************************************************/
/*  snapshot_collector.h                                                  */

#pragma once

#include "core/variant/dictionary.h"
#include "scene/debugger/scene_debugger_object.h"

struct SnapshotDataTransportObject : public SceneDebuggerObject {
	SnapshotDataTransportObject() {}
	SnapshotDataTransportObject(Object *p_obj) :
			SceneDebuggerObject(p_obj) {}

	Dictionary extra_debug_data;
};

class SnapshotCollector {
	inline static HashMap<int, Vector<uint8_t>> pending_snapshots;

public:
	static void snapshot_objects(Array *p_arr, Dictionary &p_snapshot_context);
	static Error parse_message(void *p_user, const String &p_msg, const Array &p_args, bool &r_captured);
	static void initialize();
	static void deinitialize();
	static String get_orblit_version_string();
};
