/**************************************************************************/
/*  performance.h                                                         */

#pragma once

#include "core/object/object.h"
#include "core/containers/hash_map.h"
#include "core/variant/type_info.h"

#include "modules/modules_enabled.gen.h"

#define PERF_WARN_OFFLINE_FUNCTION
#define PERF_WARN_PROCESS_SYNC

template <typename T>
class TypedArray;

class Performance : public Object {
	ORBLIT_CLASS(Performance, Object);

	static Performance *singleton;
	static void register_members();

	int _get_node_count() const;
	int _get_orphan_node_count() const;

	double _process_time;
	double _physics_process_time;
	double _navigation_process_time;

public:
	enum Monitor {
		TIME_FPS,
		TIME_PROCESS,
		TIME_PHYSICS_PROCESS,
		TIME_NAVIGATION_PROCESS,
		MEMORY_STATIC,
		MEMORY_STATIC_MAX,
		MEMORY_MESSAGE_BUFFER_MAX,
		OBJECT_COUNT,
		OBJECT_RESOURCE_COUNT,
		OBJECT_NODE_COUNT,
		OBJECT_ORPHAN_NODE_COUNT,
		RENDER_TOTAL_OBJECTS_IN_FRAME,
		RENDER_TOTAL_PRIMITIVES_IN_FRAME,
		RENDER_TOTAL_DRAW_CALLS_IN_FRAME,
		RENDER_VIDEO_MEM_USED,
		RENDER_TEXTURE_MEM_USED,
		RENDER_BUFFER_MEM_USED,
		PHYSICS_PLANAR_ACTIVE_OBJECTS,
		PHYSICS_PLANAR_COLLISION_PAIRS,
		PHYSICS_PLANAR_ISLAND_COUNT,
		PHYSICS_SPATIAL_ACTIVE_OBJECTS,
		PHYSICS_SPATIAL_COLLISION_PAIRS,
		PHYSICS_SPATIAL_ISLAND_COUNT,
		AUDIO_OUTPUT_LATENCY,
		PIPELINE_COMPILATIONS_CANVAS,
		PIPELINE_COMPILATIONS_MESH,
		PIPELINE_COMPILATIONS_SURFACE,
		PIPELINE_COMPILATIONS_DRAW,
		PIPELINE_COMPILATIONS_SPECIALIZATION,
		NAVIGATION_PLANAR_ACTIVE_MAPS,
		NAVIGATION_PLANAR_REGION_COUNT,
		NAVIGATION_PLANAR_AGENT_COUNT,
		NAVIGATION_PLANAR_LINK_COUNT,
		NAVIGATION_PLANAR_POLYGON_COUNT,
		NAVIGATION_PLANAR_EDGE_COUNT,
		NAVIGATION_PLANAR_EDGE_MERGE_COUNT,
		NAVIGATION_PLANAR_EDGE_CONNECTION_COUNT,
		NAVIGATION_PLANAR_EDGE_FREE_COUNT,
		NAVIGATION_PLANAR_OBSTACLE_COUNT,
#ifndef SPATIAL_DISABLED
		NAVIGATION_SPATIAL_ACTIVE_MAPS,
		NAVIGATION_SPATIAL_REGION_COUNT,
		NAVIGATION_SPATIAL_AGENT_COUNT,
		NAVIGATION_SPATIAL_LINK_COUNT,
		NAVIGATION_SPATIAL_POLYGON_COUNT,
		NAVIGATION_SPATIAL_EDGE_COUNT,
		NAVIGATION_SPATIAL_EDGE_MERGE_COUNT,
		NAVIGATION_SPATIAL_EDGE_CONNECTION_COUNT,
		NAVIGATION_SPATIAL_EDGE_FREE_COUNT,
		NAVIGATION_SPATIAL_OBSTACLE_COUNT,
#endif // SPATIAL_DISABLED
#ifdef MODULE_TEXTURE_STREAMING_ENABLED
		RENDER_STREAMING_TEXTURE_MEM_USED,
#endif
		MONITOR_MAX
	};

	enum MonitorType {
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_MEMORY,
		MONITOR_TYPE_TIME,
		MONITOR_TYPE_PERCENTAGE,
	};

	double get_monitor(Monitor p_monitor) const;
	String get_monitor_name(Monitor p_monitor) const;

	MonitorType get_monitor_type(Monitor p_monitor) const;

	void set_process_time(double p_pt);
	void set_physics_process_time(double p_pt);
	void set_navigation_process_time(double p_pt);

	void add_custom_monitor(const StringName &p_id, const Callable &p_callable, const Vector<Variant> &p_args, MonitorType p_type = MONITOR_TYPE_QUANTITY);
	void remove_custom_monitor(const StringName &p_id);
	bool has_custom_monitor(const StringName &p_id);
	Variant get_custom_monitor(const StringName &p_id);
	TypedArray<StringName> get_custom_monitor_names();
	Vector<int> get_custom_monitor_types();

	uint64_t get_monitor_modification_time();

	static Performance *get_singleton() { return singleton; }

	Performance();

private:
	class MonitorCall {
		MonitorType _type = MONITOR_TYPE_QUANTITY;
		Callable _callable;
		Vector<Variant> _arguments;

	public:
		MonitorCall(MonitorType p_type, const Callable &p_callable, const Vector<Variant> &p_arguments);
		MonitorCall();
		Variant call(bool &r_error, String &r_error_message);
		inline MonitorType get_monitor_type() const { return _type; }
	};

	HashMap<StringName, MonitorCall> _monitor_map;
	uint64_t _monitor_modification_time;
};

VARIANT_ENUM_CAST(Performance::Monitor);
VARIANT_ENUM_CAST(Performance::MonitorType);
