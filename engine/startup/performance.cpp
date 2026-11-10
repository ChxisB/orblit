/**************************************************************************/
/*  performance.cpp                                                       */

#include "performance.h"

#include "core/config/engine.h"
#include "core/object/class_registry.h"
#include "core/os/os.h"
#include "core/variant/typed_array.h"
#include "scene/stage/entity.h"
#include "scene/stage/stage.h"
#include "systems/audio/audio_system.h"
#include "systems/rendering/rendering_system.h"

#ifndef NAVIGATION_PLANAR_DISABLED
#include "systems/navigation_planar/navigation_system_planar.h"
#endif // NAVIGATION_PLANAR_DISABLED

#ifndef NAVIGATION_SPATIAL_DISABLED
#include "systems/navigation_spatial/navigation_system_spatial.h"
#endif // NAVIGATION_SPATIAL_DISABLED

#ifndef PHYSICS_PLANAR_DISABLED
#include "systems/physics_planar/physics_system_planar.h"
#endif // PHYSICS_PLANAR_DISABLED

#ifndef PHYSICS_SPATIAL_DISABLED
#include "systems/physics_spatial/physics_system_spatial.h"
#endif // PHYSICS_SPATIAL_DISABLED

#include "modules/modules_enabled.gen.h"

#ifdef MODULE_TEXTURE_STREAMING_ENABLED
#include "modules/texture_streaming/texture_streaming.h"
#endif

Performance *Performance::singleton = nullptr;

void Performance::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_monitor", "monitor"), &Performance::get_monitor);
	ClassRegistry::bind_method(D_METHOD("add_custom_monitor", "id", "callable", "arguments", "type"), &Performance::add_custom_monitor, DEFVAL(Array()), DEFVAL(MONITOR_TYPE_QUANTITY));
	ClassRegistry::bind_method(D_METHOD("remove_custom_monitor", "id"), &Performance::remove_custom_monitor);
	ClassRegistry::bind_method(D_METHOD("has_custom_monitor", "id"), &Performance::has_custom_monitor);
	ClassRegistry::bind_method(D_METHOD("get_custom_monitor", "id"), &Performance::get_custom_monitor);
	ClassRegistry::bind_method(D_METHOD("get_monitor_modification_time"), &Performance::get_monitor_modification_time);
	ClassRegistry::bind_method(D_METHOD("get_custom_monitor_names"), &Performance::get_custom_monitor_names);
	ClassRegistry::bind_method(D_METHOD("get_custom_monitor_types"), &Performance::get_custom_monitor_types);

	BIND_ENUM_CONSTANT(TIME_FPS);
	BIND_ENUM_CONSTANT(TIME_PROCESS);
	BIND_ENUM_CONSTANT(TIME_PHYSICS_PROCESS);
	BIND_ENUM_CONSTANT(TIME_NAVIGATION_PROCESS);
	BIND_ENUM_CONSTANT(MEMORY_STATIC);
	BIND_ENUM_CONSTANT(MEMORY_STATIC_MAX);
	BIND_ENUM_CONSTANT(MEMORY_MESSAGE_BUFFER_MAX);
	BIND_ENUM_CONSTANT(OBJECT_COUNT);
	BIND_ENUM_CONSTANT(OBJECT_RESOURCE_COUNT);
	BIND_ENUM_CONSTANT(OBJECT_NODE_COUNT);
	BIND_ENUM_CONSTANT(OBJECT_ORPHAN_NODE_COUNT);
	BIND_ENUM_CONSTANT(RENDER_TOTAL_OBJECTS_IN_FRAME);
	BIND_ENUM_CONSTANT(RENDER_TOTAL_PRIMITIVES_IN_FRAME);
	BIND_ENUM_CONSTANT(RENDER_TOTAL_DRAW_CALLS_IN_FRAME);
	BIND_ENUM_CONSTANT(RENDER_VIDEO_MEM_USED);
	BIND_ENUM_CONSTANT(RENDER_TEXTURE_MEM_USED);
	BIND_ENUM_CONSTANT(RENDER_BUFFER_MEM_USED);
	BIND_ENUM_CONSTANT(PHYSICS_PLANAR_ACTIVE_OBJECTS);
	BIND_ENUM_CONSTANT(PHYSICS_PLANAR_COLLISION_PAIRS);
	BIND_ENUM_CONSTANT(PHYSICS_PLANAR_ISLAND_COUNT);
#ifndef SPATIAL_DISABLED
	BIND_ENUM_CONSTANT(PHYSICS_SPATIAL_ACTIVE_OBJECTS);
	BIND_ENUM_CONSTANT(PHYSICS_SPATIAL_COLLISION_PAIRS);
	BIND_ENUM_CONSTANT(PHYSICS_SPATIAL_ISLAND_COUNT);
#endif // SPATIAL_DISABLED
	BIND_ENUM_CONSTANT(AUDIO_OUTPUT_LATENCY);
	BIND_ENUM_CONSTANT(PIPELINE_COMPILATIONS_CANVAS);
	BIND_ENUM_CONSTANT(PIPELINE_COMPILATIONS_MESH);
	BIND_ENUM_CONSTANT(PIPELINE_COMPILATIONS_SURFACE);
	BIND_ENUM_CONSTANT(PIPELINE_COMPILATIONS_DRAW);
	BIND_ENUM_CONSTANT(PIPELINE_COMPILATIONS_SPECIALIZATION);
#ifndef NAVIGATION_PLANAR_DISABLED
	BIND_ENUM_CONSTANT(NAVIGATION_PLANAR_ACTIVE_MAPS);
	BIND_ENUM_CONSTANT(NAVIGATION_PLANAR_REGION_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_PLANAR_AGENT_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_PLANAR_LINK_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_PLANAR_POLYGON_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_PLANAR_EDGE_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_PLANAR_EDGE_MERGE_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_PLANAR_EDGE_CONNECTION_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_PLANAR_EDGE_FREE_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_PLANAR_OBSTACLE_COUNT);
#endif // NAVIGATION_PLANAR_DISABLED
#ifndef NAVIGATION_SPATIAL_DISABLED
	BIND_ENUM_CONSTANT(NAVIGATION_SPATIAL_ACTIVE_MAPS);
	BIND_ENUM_CONSTANT(NAVIGATION_SPATIAL_REGION_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_SPATIAL_AGENT_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_SPATIAL_LINK_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_SPATIAL_POLYGON_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_SPATIAL_EDGE_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_SPATIAL_EDGE_MERGE_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_SPATIAL_EDGE_CONNECTION_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_SPATIAL_EDGE_FREE_COUNT);
	BIND_ENUM_CONSTANT(NAVIGATION_SPATIAL_OBSTACLE_COUNT);
#endif // NAVIGATION_SPATIAL_DISABLED
#ifdef MODULE_TEXTURE_STREAMING_ENABLED
	BIND_ENUM_CONSTANT(RENDER_STREAMING_TEXTURE_MEM_USED);
#endif
	BIND_ENUM_CONSTANT(MONITOR_MAX);

	BIND_ENUM_CONSTANT(MONITOR_TYPE_QUANTITY);
	BIND_ENUM_CONSTANT(MONITOR_TYPE_MEMORY);
	BIND_ENUM_CONSTANT(MONITOR_TYPE_TIME);
	BIND_ENUM_CONSTANT(MONITOR_TYPE_PERCENTAGE);
}

int Performance::_get_node_count() const {
	MainLoop *ml = OS::get_singleton()->get_main_loop();
	Stage *sml = Object::cast_to<Stage>(ml);
	if (!sml) {
		return 0;
	}
	return sml->get_entity_count();
}

int Performance::_get_orphan_node_count() const {
#ifdef DEBUG_ENABLED
	const int total_node_count = Entity::total_node_count.get();
	const int orphan_node_count = total_node_count - _get_node_count();
	return orphan_node_count;
#else
	return 0;
#endif
}

String Performance::get_monitor_name(Monitor p_monitor) const {
	ERR_FAIL_INDEX_V(p_monitor, MONITOR_MAX, String());
	static const char *names[MONITOR_MAX] = {
		PNAME("time/fps"),
		PNAME("time/process"),
		PNAME("time/physics_process"),
		PNAME("time/navigation_process"),
		PNAME("memory/static"),
		PNAME("memory/static_max"),
		PNAME("memory/msg_buf_max"),
		PNAME("object/objects"),
		PNAME("object/resources"),
		PNAME("object/nodes"),
		PNAME("object/orphan_nodes"),
		PNAME("raster/total_objects_drawn"),
		PNAME("raster/total_primitives_drawn"),
		PNAME("raster/total_draw_calls"),
		PNAME("video/video_mem"),
		PNAME("video/texture_mem"),
		PNAME("video/buffer_mem"),
		PNAME("physics_planar/active_objects"),
		PNAME("physics_planar/collision_pairs"),
		PNAME("physics_planar/islands"),
		PNAME("physics_spatial/active_objects"),
		PNAME("physics_spatial/collision_pairs"),
		PNAME("physics_spatial/islands"),
		PNAME("audio/driver/output_latency"),
		PNAME("pipeline/compilations_canvas"),
		PNAME("pipeline/compilations_mesh"),
		PNAME("pipeline/compilations_surface"),
		PNAME("pipeline/compilations_draw"),
		PNAME("pipeline/compilations_specialization"),
#ifndef NAVIGATION_PLANAR_DISABLED
		PNAME("navigation_planar/active_maps"),
		PNAME("navigation_planar/regions"),
		PNAME("navigation_planar/agents"),
		PNAME("navigation_planar/links"),
		PNAME("navigation_planar/polygons"),
		PNAME("navigation_planar/edges"),
		PNAME("navigation_planar/edges_merged"),
		PNAME("navigation_planar/edges_connected"),
		PNAME("navigation_planar/edges_free"),
		PNAME("navigation_planar/obstacles"),
#endif // NAVIGATION_PLANAR_DISABLED
#ifndef NAVIGATION_SPATIAL_DISABLED
		PNAME("navigation_spatial/active_maps"),
		PNAME("navigation_spatial/regions"),
		PNAME("navigation_spatial/agents"),
		PNAME("navigation_spatial/links"),
		PNAME("navigation_spatial/polygons"),
		PNAME("navigation_spatial/edges"),
		PNAME("navigation_spatial/edges_merged"),
		PNAME("navigation_spatial/edges_connected"),
		PNAME("navigation_spatial/edges_free"),
		PNAME("navigation_spatial/obstacles"),
#endif // NAVIGATION_SPATIAL_DISABLED
#ifdef MODULE_TEXTURE_STREAMING_ENABLED
		PNAME("video/streaming_texture_mem_used"),
#endif
	};
	static_assert(std_size(names) == MONITOR_MAX);

	return names[p_monitor];
}

double Performance::get_monitor(Monitor p_monitor) const {
	switch (p_monitor) {
		case TIME_FPS:
			return Engine::get_singleton()->get_frames_per_second();
		case TIME_PROCESS:
			return _process_time;
		case TIME_PHYSICS_PROCESS:
			return _physics_process_time;
		case TIME_NAVIGATION_PROCESS:
			return _navigation_process_time;
		case MEMORY_STATIC:
			return Memory::get_mem_usage();
		case MEMORY_STATIC_MAX:
			return Memory::get_mem_max_usage();
		case MEMORY_MESSAGE_BUFFER_MAX:
			return MessageQueue::get_singleton()->get_max_buffer_usage();
		case OBJECT_COUNT:
			return ObjectRegistry::get_object_count();
		case OBJECT_RESOURCE_COUNT:
			return ResourceCache::get_cached_resource_count();
		case OBJECT_NODE_COUNT:
			return _get_node_count();
		case OBJECT_ORPHAN_NODE_COUNT:
			return _get_orphan_node_count();
		case RENDER_TOTAL_OBJECTS_IN_FRAME:
			return RS::get_singleton()->get_rendering_info(RSE::RENDERING_INFO_TOTAL_OBJECTS_IN_FRAME);
		case RENDER_TOTAL_PRIMITIVES_IN_FRAME:
			return RS::get_singleton()->get_rendering_info(RSE::RENDERING_INFO_TOTAL_PRIMITIVES_IN_FRAME);
		case RENDER_TOTAL_DRAW_CALLS_IN_FRAME:
			return RS::get_singleton()->get_rendering_info(RSE::RENDERING_INFO_TOTAL_DRAW_CALLS_IN_FRAME);
		case RENDER_VIDEO_MEM_USED:
			return RS::get_singleton()->get_rendering_info(RSE::RENDERING_INFO_VIDEO_MEM_USED);
		case RENDER_TEXTURE_MEM_USED:
			return RS::get_singleton()->get_rendering_info(RSE::RENDERING_INFO_TEXTURE_MEM_USED);
		case RENDER_BUFFER_MEM_USED:
			return RS::get_singleton()->get_rendering_info(RSE::RENDERING_INFO_BUFFER_MEM_USED);
		case PIPELINE_COMPILATIONS_CANVAS:
			return RS::get_singleton()->get_rendering_info(RSE::RENDERING_INFO_PIPELINE_COMPILATIONS_CANVAS);
		case PIPELINE_COMPILATIONS_MESH:
			return RS::get_singleton()->get_rendering_info(RSE::RENDERING_INFO_PIPELINE_COMPILATIONS_MESH);
		case PIPELINE_COMPILATIONS_SURFACE:
			return RS::get_singleton()->get_rendering_info(RSE::RENDERING_INFO_PIPELINE_COMPILATIONS_SURFACE);
		case PIPELINE_COMPILATIONS_DRAW:
			return RS::get_singleton()->get_rendering_info(RSE::RENDERING_INFO_PIPELINE_COMPILATIONS_DRAW);
		case PIPELINE_COMPILATIONS_SPECIALIZATION:
			return RS::get_singleton()->get_rendering_info(RSE::RENDERING_INFO_PIPELINE_COMPILATIONS_SPECIALIZATION);
#ifndef PHYSICS_PLANAR_DISABLED
		case PHYSICS_PLANAR_ACTIVE_OBJECTS:
			return PhysicsSystem2D::get_singleton()->get_process_info(PS2DE::INFO_ACTIVE_OBJECTS);
		case PHYSICS_PLANAR_COLLISION_PAIRS:
			return PhysicsSystem2D::get_singleton()->get_process_info(PS2DE::INFO_COLLISION_PAIRS);
		case PHYSICS_PLANAR_ISLAND_COUNT:
			return PhysicsSystem2D::get_singleton()->get_process_info(PS2DE::INFO_ISLAND_COUNT);
#else
		case PHYSICS_PLANAR_ACTIVE_OBJECTS:
			return 0;
		case PHYSICS_PLANAR_COLLISION_PAIRS:
			return 0;
		case PHYSICS_PLANAR_ISLAND_COUNT:
			return 0;
#endif // PHYSICS_PLANAR_DISABLED
#ifndef PHYSICS_SPATIAL_DISABLED
		case PHYSICS_SPATIAL_ACTIVE_OBJECTS:
			return PhysicsSystem3D::get_singleton()->get_process_info(PS3DE::INFO_ACTIVE_OBJECTS);
		case PHYSICS_SPATIAL_COLLISION_PAIRS:
			return PhysicsSystem3D::get_singleton()->get_process_info(PS3DE::INFO_COLLISION_PAIRS);
		case PHYSICS_SPATIAL_ISLAND_COUNT:
			return PhysicsSystem3D::get_singleton()->get_process_info(PS3DE::INFO_ISLAND_COUNT);
#else
		case PHYSICS_SPATIAL_ACTIVE_OBJECTS:
			return 0;
		case PHYSICS_SPATIAL_COLLISION_PAIRS:
			return 0;
		case PHYSICS_SPATIAL_ISLAND_COUNT:
			return 0;
#endif // PHYSICS_SPATIAL_DISABLED

		case AUDIO_OUTPUT_LATENCY:
			return AudioSystem::get_singleton()->get_output_latency();

#ifndef NAVIGATION_PLANAR_DISABLED
		case NAVIGATION_PLANAR_ACTIVE_MAPS:
			return NavigationSystem2D::get_singleton()->get_process_info(NavigationSystem2D::INFO_ACTIVE_MAPS);
		case NAVIGATION_PLANAR_REGION_COUNT:
			return NavigationSystem2D::get_singleton()->get_process_info(NavigationSystem2D::INFO_REGION_COUNT);
		case NAVIGATION_PLANAR_AGENT_COUNT:
			return NavigationSystem2D::get_singleton()->get_process_info(NavigationSystem2D::INFO_AGENT_COUNT);
		case NAVIGATION_PLANAR_LINK_COUNT:
			return NavigationSystem2D::get_singleton()->get_process_info(NavigationSystem2D::INFO_LINK_COUNT);
		case NAVIGATION_PLANAR_POLYGON_COUNT:
			return NavigationSystem2D::get_singleton()->get_process_info(NavigationSystem2D::INFO_POLYGON_COUNT);
		case NAVIGATION_PLANAR_EDGE_COUNT:
			return NavigationSystem2D::get_singleton()->get_process_info(NavigationSystem2D::INFO_EDGE_COUNT);
		case NAVIGATION_PLANAR_EDGE_MERGE_COUNT:
			return NavigationSystem2D::get_singleton()->get_process_info(NavigationSystem2D::INFO_EDGE_MERGE_COUNT);
		case NAVIGATION_PLANAR_EDGE_CONNECTION_COUNT:
			return NavigationSystem2D::get_singleton()->get_process_info(NavigationSystem2D::INFO_EDGE_CONNECTION_COUNT);
		case NAVIGATION_PLANAR_EDGE_FREE_COUNT:
			return NavigationSystem2D::get_singleton()->get_process_info(NavigationSystem2D::INFO_EDGE_FREE_COUNT);
		case NAVIGATION_PLANAR_OBSTACLE_COUNT:
			return NavigationSystem2D::get_singleton()->get_process_info(NavigationSystem2D::INFO_OBSTACLE_COUNT);
#endif // NAVIGATION_PLANAR_DISABLED

#ifndef NAVIGATION_SPATIAL_DISABLED
		case NAVIGATION_SPATIAL_ACTIVE_MAPS:
			return NavigationSystem3D::get_singleton()->get_process_info(NavigationSystem3D::INFO_ACTIVE_MAPS);
		case NAVIGATION_SPATIAL_REGION_COUNT:
			return NavigationSystem3D::get_singleton()->get_process_info(NavigationSystem3D::INFO_REGION_COUNT);
		case NAVIGATION_SPATIAL_AGENT_COUNT:
			return NavigationSystem3D::get_singleton()->get_process_info(NavigationSystem3D::INFO_AGENT_COUNT);
		case NAVIGATION_SPATIAL_LINK_COUNT:
			return NavigationSystem3D::get_singleton()->get_process_info(NavigationSystem3D::INFO_LINK_COUNT);
		case NAVIGATION_SPATIAL_POLYGON_COUNT:
			return NavigationSystem3D::get_singleton()->get_process_info(NavigationSystem3D::INFO_POLYGON_COUNT);
		case NAVIGATION_SPATIAL_EDGE_COUNT:
			return NavigationSystem3D::get_singleton()->get_process_info(NavigationSystem3D::INFO_EDGE_COUNT);
		case NAVIGATION_SPATIAL_EDGE_MERGE_COUNT:
			return NavigationSystem3D::get_singleton()->get_process_info(NavigationSystem3D::INFO_EDGE_MERGE_COUNT);
		case NAVIGATION_SPATIAL_EDGE_CONNECTION_COUNT:
			return NavigationSystem3D::get_singleton()->get_process_info(NavigationSystem3D::INFO_EDGE_CONNECTION_COUNT);
		case NAVIGATION_SPATIAL_EDGE_FREE_COUNT:
			return NavigationSystem3D::get_singleton()->get_process_info(NavigationSystem3D::INFO_EDGE_FREE_COUNT);
		case NAVIGATION_SPATIAL_OBSTACLE_COUNT:
			return NavigationSystem3D::get_singleton()->get_process_info(NavigationSystem3D::INFO_OBSTACLE_COUNT);
#endif // NAVIGATION_SPATIAL_DISABLED
#ifdef MODULE_TEXTURE_STREAMING_ENABLED
		case RENDER_STREAMING_TEXTURE_MEM_USED: {
			TextureStreaming *texture_streaming = TextureStreaming::get_singleton();
			return texture_streaming ? texture_streaming->get_memory_budget_bytes_used() : 0;
		}
#endif // MODULE_TEXTURE_STREAMING_ENABLED
		default: {
		}
	}

	return 0;
}

Performance::MonitorType Performance::get_monitor_type(Monitor p_monitor) const {
	ERR_FAIL_INDEX_V(p_monitor, MONITOR_MAX, MONITOR_TYPE_QUANTITY);
	// ugly
	static const MonitorType types[MONITOR_MAX] = {
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_TIME,
		MONITOR_TYPE_TIME,
		MONITOR_TYPE_TIME,
		MONITOR_TYPE_MEMORY,
		MONITOR_TYPE_MEMORY,
		MONITOR_TYPE_MEMORY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_MEMORY,
		MONITOR_TYPE_MEMORY,
		MONITOR_TYPE_MEMORY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_TIME,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
#ifndef SPATIAL_DISABLED
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
		MONITOR_TYPE_QUANTITY,
#endif // SPATIAL_DISABLED
#ifdef MODULE_TEXTURE_STREAMING_ENABLED
		MONITOR_TYPE_MEMORY,
#endif // MODULE_TEXTURE_STREAMING_ENABLED
	};
	static_assert((sizeof(types) / sizeof(MonitorType)) == MONITOR_MAX);

	return types[p_monitor];
}

void Performance::set_process_time(double p_pt) {
	_process_time = p_pt;
}

void Performance::set_physics_process_time(double p_pt) {
	_physics_process_time = p_pt;
}

void Performance::set_navigation_process_time(double p_pt) {
	_navigation_process_time = p_pt;
}

void Performance::add_custom_monitor(const StringName &p_id, const Callable &p_callable, const Vector<Variant> &p_args, MonitorType p_type) {
	ERR_FAIL_COND_MSG(has_custom_monitor(p_id), "Custom monitor with id '" + String(p_id) + "' already exists.");
	_monitor_map.insert(p_id, MonitorCall(p_type, p_callable, p_args));
	_monitor_modification_time = OS::get_singleton()->get_ticks_usec();
}

void Performance::remove_custom_monitor(const StringName &p_id) {
	ERR_FAIL_COND_MSG(!has_custom_monitor(p_id), "Custom monitor with id '" + String(p_id) + "' doesn't exist.");
	_monitor_map.erase(p_id);
	_monitor_modification_time = OS::get_singleton()->get_ticks_usec();
}

bool Performance::has_custom_monitor(const StringName &p_id) {
	return _monitor_map.has(p_id);
}

Variant Performance::get_custom_monitor(const StringName &p_id) {
	ERR_FAIL_COND_V_MSG(!has_custom_monitor(p_id), Variant(), "Custom monitor with id '" + String(p_id) + "' doesn't exist.");
	bool error;
	String error_message;
	Variant return_value = _monitor_map[p_id].call(error, error_message);
	ERR_FAIL_COND_V_MSG(error, return_value, "Error calling from custom monitor '" + String(p_id) + "' to callable: " + error_message);
	return return_value;
}

TypedArray<StringName> Performance::get_custom_monitor_names() {
	if (!_monitor_map.size()) {
		return TypedArray<StringName>();
	}
	TypedArray<StringName> return_array;
	return_array.resize(_monitor_map.size());
	int index = 0;
	for (KeyValue<StringName, MonitorCall> i : _monitor_map) {
		return_array.set(index, i.key);
		index++;
	}
	return return_array;
}

Vector<int> Performance::get_custom_monitor_types() {
	if (_monitor_map.is_empty()) {
		return Vector<int>();
	}
	Vector<int> ret;
	ret.resize(_monitor_map.size());
	int index = 0;
	for (const KeyValue<StringName, MonitorCall> &i : _monitor_map) {
		ret.set(index, (int)i.value.get_monitor_type());
		index++;
	}
	return ret;
}

uint64_t Performance::get_monitor_modification_time() {
	return _monitor_modification_time;
}

Performance::Performance() {
	_process_time = 0;
	_physics_process_time = 0;
	_navigation_process_time = 0;
	_monitor_modification_time = 0;
	singleton = this;
}

Performance::MonitorCall::MonitorCall(Performance::MonitorType p_type, const Callable &p_callable, const Vector<Variant> &p_arguments) {
	_type = p_type;
	_callable = p_callable;
	_arguments = p_arguments;
}

Performance::MonitorCall::MonitorCall() {
}

Variant Performance::MonitorCall::call(bool &r_error, String &r_error_message) {
	Vector<const Variant *> arguments_mem;
	arguments_mem.resize(_arguments.size());
	for (int i = 0; i < _arguments.size(); i++) {
		arguments_mem.write[i] = &_arguments[i];
	}
	const Variant **args = (const Variant **)arguments_mem.ptr();
	int argc = _arguments.size();
	Variant return_value;
	Callable::CallError error;
	_callable.callp(args, argc, return_value, error);
	r_error = (error.error != Callable::CallError::CALL_OK);
	if (r_error) {
		r_error_message = Variant::get_callable_error_text(_callable, args, argc, error);
	}
	return return_value;
}
