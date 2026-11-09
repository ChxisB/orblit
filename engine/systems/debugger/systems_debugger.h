/**************************************************************************/
/*  systems_debugger.h                                                    */

#pragma once

#include "core/containers/list.h"
#include "core/containers/rid.h"
#include "core/containers/vector.h"
#include "core/error/error_list.h"
#include "core/object/ref_counted.h"
#include "core/string/string_name.h"
#include "core/string/ustring.h"
#include "core/variant/array.h"
#include "systems/rendering/rendering_system_types.h"

class SystemsDebugger {
public:
	// Memory usage
	struct ResourceInfo {
		String path;
		String format;
		String type;
		RID id;
		int vram = 0;
		bool operator<(const ResourceInfo &p_img) const { return vram == p_img.vram ? id < p_img.id : vram > p_img.vram; }
	};

	struct ResourceUsage {
		List<ResourceInfo> infos;

		Array serialize();
		bool deserialize(const Array &p_arr);
	};

	// Script Profiler
	struct ScriptFunctionSignature {
		StringName name;
		int id = -1;

		Array serialize();
		bool deserialize(const Array &p_arr);
	};

	struct ScriptFunctionInfo {
		StringName name;
		int sig_id = -1;
		int call_count = 0;
		double self_time = 0;
		double total_time = 0;
		double internal_time = 0;
	};

	// Systems profiler
	struct ServerFunctionInfo {
		StringName name;
		double time = 0;
	};

	struct ServerInfo {
		StringName name;
		List<ServerFunctionInfo> functions;
	};

	struct SystemsProfilerFrame {
		int frame_number = 0;
		double frame_time = 0;
		double process_time = 0;
		double physics_time = 0;
		double physics_frame_time = 0;
		double script_time = 0;
		List<ServerInfo> servers;
		Vector<ScriptFunctionInfo> script_functions;

		Array serialize();
		bool deserialize(const Array &p_arr);
	};

	// Visual Profiler
	struct VisualProfilerFrame {
		uint64_t frame_number = 0;
		Vector<RenderingSystemTypes::FrameProfileArea> areas;

		Array serialize();
		bool deserialize(const Array &p_arr);
	};

private:
	class ScriptsProfiler;
	class SystemsProfiler;
	class VisualProfiler;

	double last_draw_time = 0.0;
	Ref<SystemsProfiler> systems_profiler;
	Ref<VisualProfiler> visual_profiler;

	static SystemsDebugger *singleton;

	static Error _capture(void *p_user, const String &p_cmd, const Array &p_data, bool &r_captured);

	void _send_resource_usage();
	String _get_resource_type_from_path(const String &p_path);

	SystemsDebugger();

public:
	static void initialize();
	static void deinitialize();

	~SystemsDebugger();
};
