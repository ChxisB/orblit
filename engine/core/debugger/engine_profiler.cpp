/**************************************************************************/
/*  engine_profiler.cpp                                                   */

#include "engine_profiler.h"

#include "core/debugger/engine_debugger.h"
#include "core/object/class_registry.h" // IWYU pragma: keep. `ORBLIT_VIRTUAL_BIND` macro.

void EngineProfiler::register_members() {
	ORBLIT_VIRTUAL_BIND(_toggle, "enable", "options");
	ORBLIT_VIRTUAL_BIND(_add_frame, "data");
	ORBLIT_VIRTUAL_BIND(_tick, "frame_time", "process_time", "physics_time", "physics_frame_time");
}

void EngineProfiler::toggle(bool p_enable, const Array &p_array) {
	ORBLIT_VIRTUAL_CALL(_toggle, p_enable, p_array);
}

void EngineProfiler::add(const Array &p_data) {
	ORBLIT_VIRTUAL_CALL(_add_frame, p_data);
}

void EngineProfiler::tick(double p_frame_time, double p_process_time, double p_physics_time, double p_physics_frame_time) {
	ORBLIT_VIRTUAL_CALL(_tick, p_frame_time, p_process_time, p_physics_time, p_physics_frame_time);
}

Error EngineProfiler::bind(const String &p_name) {
	ERR_FAIL_COND_V(is_bound(), ERR_ALREADY_IN_USE);
	EngineDebugger::Profiler prof(
			this,
			[](void *p_user, bool p_enable, const Array &p_opts) {
				static_cast<EngineProfiler *>(p_user)->toggle(p_enable, p_opts);
			},
			[](void *p_user, const Array &p_data) {
				static_cast<EngineProfiler *>(p_user)->add(p_data);
			},
			[](void *p_user, double p_frame_time, double p_process_time, double p_physics_time, double p_physics_frame_time) {
				static_cast<EngineProfiler *>(p_user)->tick(p_frame_time, p_process_time, p_physics_time, p_physics_frame_time);
			});
	registration = p_name;
	EngineDebugger::register_profiler(p_name, prof);
	return OK;
}

Error EngineProfiler::unbind() {
	ERR_FAIL_COND_V(!is_bound(), ERR_UNCONFIGURED);
	EngineDebugger::unregister_profiler(registration);
	registration.clear();
	return OK;
}

EngineProfiler::~EngineProfiler() {
	if (is_bound()) {
		unbind();
	}
}
