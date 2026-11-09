/**************************************************************************/
/*  editor_performance_profiler.h                                         */

#pragma once

#include "core/containers/hash_map.h"
#include "startup/performance.h"
#include "scene/ui/control.h"
#include "scene/ui/label.h"
#include "scene/ui/split_container.h"
#include "scene/ui/tree.h"

class EditorPerformanceProfiler : public HSplitContainer {
	ORBLIT_CLASS(EditorPerformanceProfiler, HSplitContainer);

private:
	class Monitor {
	public:
		String name;
		String base;
		List<float> history;
		float max = 0.0f;
		TreeItem *item = nullptr;
		Performance::MonitorType type = Performance::MONITOR_TYPE_QUANTITY;
		int frame_index = 0;

		Monitor() {}
		Monitor(const String &p_name, const String &p_base, int p_frame_index, Performance::MonitorType p_type, TreeItem *p_item);
		void reset();
	};

	HashMap<StringName, Monitor> monitors;

	HashMap<StringName, TreeItem *> base_map;
	Tree *monitor_tree = nullptr;
	Control *monitor_draw = nullptr;
	Label *info_message = nullptr;
	StringName marker_key;
	int marker_frame = 0;
	const int MARGIN = 4;
	const int POINT_SEPARATION = 5;
	const int MARKER_MARGIN = 2;

	String _format_label(float p_value, Performance::MonitorType p_type) const;
	void _update_monitor_value(Monitor *p_monitor, float p_value);
	void _monitor_select();
	void _monitor_draw();
	void _build_monitor_tree();
	TreeItem *_get_monitor_base(const StringName &p_base_name);
	TreeItem *_create_monitor_item(const StringName &p_monitor_name, TreeItem *p_base);
	void _marker_input(const Ref<InputEvent> &p_event);

protected:
	void _notification(int p_what);

public:
	void reset();
	void update_monitors(const Vector<StringName> &p_names, const PackedInt32Array &p_types);
	void add_profile_frame(const Vector<float> &p_values);
	List<float> *get_monitor_data(const StringName &p_name);
	EditorPerformanceProfiler();
};
