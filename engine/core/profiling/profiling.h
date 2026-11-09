/**************************************************************************/
/*  profiling.h                                                           */

#pragma once

#include "core/profiling/profiling.gen.h" // IWYU pragma: keep.

// This header provides profiling primitives (implemented as macros) for various backends.
// See the "No profiling" branch at the bottom for a short description of the functions.

// To configure / use the profiler, use the --profiler_path and other --profiler_* arguments
// when compiling Orblit. You can also find details in the SCSub file (in this folder).

// Note: It is highly recommended to avoid including this header in other header files.
//       Prefer including it in .cpp files only. The reason is that we want to keep
//       the recompile cost of changing the profiler as low as possible.

#if defined(ORBLIT_USE_TRACY)
// Use the tracy profiler.

#include "core/string/string_name.h"

#define TRACY_ENABLE

ORBLIT_MSVC_WARNING_PUSH_AND_IGNORE(4366) // The result of the unary operator may be unaligned.
#include <tracy/Tracy.hpp>
ORBLIT_MSVC_WARNING_POP

// Hijacking the tracy namespace so we can use their macros.
namespace tracy {
const SourceLocationData *intern_source_location(const void *p_function_ptr, const StringName &p_file, const StringName &p_function, const StringName &p_name, uint32_t p_line, bool p_is_script);
} //namespace tracy

// Define tracing macros.
#define OrblitProfileFrameMark FrameMark
#define OrblitProfileZone(m_zone_name) ZoneNamedN(ORBLIT_UNIQUE_NAME(__orblit_tracy_szone_), m_zone_name, true)
#define OrblitProfileZoneGroupedFirst(m_group_name, m_zone_name) ZoneNamedN(__orblit_tracy_zone_##m_group_name, m_zone_name, true)
#define OrblitProfileZoneGroupedEndEarly(m_group_name, m_zone_name) __orblit_tracy_zone_##m_group_name.~ScopedZone();
#ifndef TRACY_CALLSTACK
#define OrblitProfileZoneGrouped(m_group_name, m_zone_name) \
	OrblitProfileZoneGroupedEndEarly(m_group_name, m_zone_name); \
	static constexpr tracy::SourceLocationData TracyConcat(__tracy_source_location, TracyLine){ m_zone_name, TracyFunction, TracyFile, (uint32_t)TracyLine, 0 }; \
	new (&__orblit_tracy_zone_##m_group_name) tracy::ScopedZone(&TracyConcat(__tracy_source_location, TracyLine), true)
#else
#define OrblitProfileZoneGrouped(m_group_name, m_zone_name) \
	OrblitProfileZoneGroupedEndEarly(m_group_name, m_zone_name); \
	static constexpr tracy::SourceLocationData TracyConcat(__tracy_source_location, TracyLine){ m_zone_name, TracyFunction, TracyFile, (uint32_t)TracyLine, 0 }; \
	new (&__orblit_tracy_zone_##m_group_name) tracy::ScopedZone(&TracyConcat(__tracy_source_location, TracyLine), TRACY_CALLSTACK, true)
#endif

#define OrblitProfileZoneScript(m_ptr, m_file, m_function, m_name, m_line) \
	tracy::ScopedZone __orblit_tracy_script(tracy::intern_source_location(m_ptr, m_file, m_function, m_name, m_line, true))
#define OrblitProfileZoneScriptSystemCall(m_ptr, m_file, m_function, m_name, m_line) \
	tracy::ScopedZone __orblit_tracy_zone_system_call(tracy::intern_source_location(m_ptr, m_file, m_function, m_name, m_line, false))

// Memory allocation
#ifdef ORBLIT_PROFILER_TRACK_MEMORY
#define OrblitProfileAlloc(m_ptr, m_size) \
	ORBLIT_GCC_WARNING_PUSH_AND_IGNORE("-Wmaybe-uninitialized") \
	TracyAlloc(m_ptr, m_size); \
	ORBLIT_GCC_WARNING_POP
#define OrblitProfileFree(m_ptr) TracyFree(m_ptr)
#else
#define OrblitProfileAlloc(m_ptr, m_size)
#define OrblitProfileFree(m_ptr)
#endif

void init_profiler();
void cleanup_profiler();

#elif defined(ORBLIT_USE_PERFETTO)
// Use the perfetto profiler.

#include "core/typedefs.h"
#include "startup/performance.h"

#include <perfetto.h>

PERFETTO_DEFINE_CATEGORIES(
		perfetto::Category("orblit")
				.SetDescription("Orblit Engine Events"),
		perfetto::Category("orblit_scripting")
				.SetDescription("Orblit Scripting Events"), );

// See PERFETTO_INTERNAL_SCOPED_EVENT_FINALIZER
struct PerfettoGroupedEventEnder {
	_FORCE_INLINE_ void _end_now() {
		TRACE_EVENT_END("orblit");
	}

	_FORCE_INLINE_ ~PerfettoGroupedEventEnder() {
		_end_now();
	}
};

#define OrblitProfileFrameMark \
	perfetto::CounterTrack __frame_time_track = perfetto::CounterTrack("Frame time", "ms").set_unit_multiplier(1000); \
	TRACE_COUNTER("orblit", __frame_time_track, Performance::get_singleton()->get_monitor(Performance::Monitor::TIME_PROCESS));
#define OrblitProfileZone(m_zone_name) TRACE_EVENT("orblit", m_zone_name);
#define OrblitProfileZoneGroupedFirst(m_group_name, m_zone_name) \
	TRACE_EVENT_BEGIN("orblit", m_zone_name); \
	PerfettoGroupedEventEnder __orblit_perfetto_zone_##m_group_name
#define OrblitProfileZoneGroupedEndEarly(m_group_name, m_zone_name) __orblit_perfetto_zone_##m_group_name.~PerfettoGroupedEventEnder()
#define OrblitProfileZoneGrouped(m_group_name, m_zone_name) \
	__orblit_perfetto_zone_##m_group_name._end_now(); \
	TRACE_EVENT_BEGIN("orblit", m_zone_name);

static thread_local HashSet<StringName> __tracing_system_call;

/**
 * Script tracing may cross function boundaries (tracing started in the caller script), so the logic below only triggers
 * a TRACE_EVENT_BEGIN for `OrblitProfileZoneScript` if tracing hasn't already been initiated by
 * `OrblitProfileZoneScriptSystemCall` in the caller.
 */
struct PerfettoScriptTracer {
	StringName name;
	bool is_system_call;
	bool tracing;

	PerfettoScriptTracer(const StringName &p_file, const StringName &p_function, const StringName &p_name, int p_line, bool p_system_call) : name(p_name), is_system_call(p_system_call) {
		if (is_system_call || !__tracing_system_call.erase(name)) {
			TRACE_EVENT_BEGIN("orblit_scripting", perfetto::DynamicString(p_name.string().utf8().get_data()), "source file", p_file.string().utf8().get_data(), "line number", p_line);
			tracing = true;
		}

		if (is_system_call) {
			__tracing_system_call.insert(name);
		}
	}

	_FORCE_INLINE_ void _end_now() {
		if (tracing) {
			TRACE_EVENT_END("orblit_scripting");
		}
	}

	_FORCE_INLINE_ ~PerfettoScriptTracer() {
		_end_now();
		if (is_system_call) {
			__tracing_system_call.erase(name);
		}
	}
};

#define OrblitProfileZoneScript(m_ptr, m_file, m_function, m_name, m_line) PerfettoScriptTracer __orblit_perfetto_script_tracer(m_file, m_function, m_name, m_line, false);
#define OrblitProfileZoneScriptSystemCall(m_ptr, m_file, m_function, m_name, m_line) PerfettoScriptTracer __orblit_perfetto_script_system_call_tracer(m_file, m_function, m_name, m_line, true);

#define OrblitProfileAlloc(m_ptr, m_size)
#define OrblitProfileFree(m_ptr)

void init_profiler();
void cleanup_profiler();

#elif defined(ORBLIT_USE_INSTRUMENTS)

#include <os/log.h>
#include <os/signpost.h>

namespace apple::instruments {

extern os_log_t LOG;
extern os_log_t LOG_TRACING;

class Zone {
public:
	os_signpost_id_t id;
	Zone() :
			id(os_signpost_id_generate(LOG_TRACING)) {}
	~Zone() {
		end();
	}
	void end() {
		if (id != OS_SIGNPOST_ID_NULL) {
			os_signpost_interval_end(LOG_TRACING, id, "Zone");
			id = OS_SIGNPOST_ID_NULL;
		}
	}
};

} // namespace apple::instruments

#define OrblitProfileFrameMark \
	os_signpost_event_emit(apple::instruments::LOG, OS_SIGNPOST_ID_EXCLUSIVE, "Frame");

#define OrblitProfileZoneGroupedFirst(m_group_name, m_zone_name) \
	apple::instruments::Zone _ORBLIT_VARNAME_CONCAT_(__instruments_zone__, _, m_group_name); \
	os_signpost_interval_begin(apple::instruments::LOG_TRACING, _ORBLIT_VARNAME_CONCAT_(__instruments_zone__, _, m_group_name).id, "Zone", "%{public}s", m_zone_name);

#define OrblitProfileZoneGroupedEndEarly(m_group_name, m_zone_name) \
	_ORBLIT_VARNAME_CONCAT_(__instruments_zone__, _, m_group_name).end();

#define OrblitProfileZoneGrouped(m_group_name, m_zone_name) \
	OrblitProfileZoneGroupedEndEarly(m_group_name, m_zone_name); \
	_ORBLIT_VARNAME_CONCAT_(__instruments_zone__, _, m_group_name).id = os_signpost_id_generate(apple::instruments::LOG_TRACING); \
	os_signpost_interval_begin(apple::instruments::LOG_TRACING, _ORBLIT_VARNAME_CONCAT_(__instruments_zone__, _, m_group_name).id, "Zone", "%{public}s", m_zone_name);

#define OrblitProfileZone(m_zone_name) \
	OrblitProfileZoneGroupedFirst(__COUNTER__, m_zone_name)

#define OrblitProfileZoneScript(m_ptr, m_file, m_function, m_name, m_line)
#define OrblitProfileZoneScriptSystemCall(m_ptr, m_file, m_function, m_name, m_line)

// Instruments has its own memory profiling, so these are no-ops.
#define OrblitProfileAlloc(m_ptr, m_size)
#define OrblitProfileFree(m_ptr)

void init_profiler();
void cleanup_profiler();

#else
// No profiling; all macros are stubs.

void init_profiler();
void cleanup_profiler();

// Tell the profiling backend that a new frame has started.
#define OrblitProfileFrameMark
// Defines a profile zone from here to the end of the scope.
#define OrblitProfileZone(m_zone_name)
// Defines a profile zone group. The first profile zone starts immediately,
// and ends either when the next zone starts, or when the scope ends.
#define OrblitProfileZoneGroupedFirst(m_group_name, m_zone_name)
// End the profile zone group's current profile zone now.
#define OrblitProfileZoneGroupedEndEarly(m_group_name, m_zone_name)
// Replace the profile zone group's current profile zone.
// The new zone ends either when the next zone starts, or when the scope ends.
#define OrblitProfileZoneGrouped(m_group_name, m_zone_name)
// Tell the profiling backend that an allocation happened, with its location and size.
#define OrblitProfileAlloc(m_ptr, m_size)
// Tell the profiling backend that an allocation was freed.
// There must be a one to one correspondence of OrblitProfileAlloc and OrblitProfileFree calls.
#define OrblitProfileFree(m_ptr)

// Define a zone for a script call (dynamic source location).
// m_ptr is a pointer to the function instance, which will be used for the lookup.
// m_file, m_function, m_name are StringNames, and m_line is uint32_t
#define OrblitProfileZoneScript(m_ptr, m_file, m_function, m_name, m_line)
// Define a zone for a system call from a script (dynamic source location).
#define OrblitProfileZoneScriptSystemCall(m_ptr, m_file, m_function, m_name, m_line)

#endif
