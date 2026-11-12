/**************************************************************************/
/*  graph_debug.h                                                         */

#pragma once

#include "core/debugger/engine_debugger.h"
#include "core/string/string_name.h"
#include "core/string/ustring.h"
#include "core/variant/variant.h"

class GraphCall;

// Ties the interpreter to the engine's script debugger. A step stop follows the same rules as
// any other language: lines left count the steps to run, and depth says which level counts them.
// With no debugger attached, nothing here runs.
//
// The debugger knows a place by a line and a source. A graph has no lines, so the line is the id
// of a step and the source names the script and the function. Step ids repeat from one function
// to the next, and the function in the source keeps them apart.
class GraphDebug {
	static void _stop_if_due(const GraphCall &p_call);

public:
	// Lists a call as running for as long as it lives, so the debugger can read the stack, and
	// counts it into the depth that step over and step out measure.
	class Scope {
		bool listed = false;

	public:
		explicit Scope(GraphCall &p_call);
		~Scope();
		Scope(const Scope &) = delete;
		Scope &operator=(const Scope &) = delete;
	};

	// Stops when a breakpoint sits on the step the call is about to run or a step command is due.
	// A call with no debugger attached pays this one check.
	static void before_step(const GraphCall &p_call) {
		if (unlikely(EngineDebugger::is_active())) {
			_stop_if_due(p_call);
		}
	}
	// Stops on a runtime error, unless the debugger ignores errors.
	static void on_error();

	// Level 0 is the innermost call. Null when the level is past the outermost.
	static int get_level_count();
	static const GraphCall *get_level(int p_level);

	static String make_source(const String &p_script_path, const StringName &p_function);
	// The source of the function a call runs, as a breakpoint names it.
	static String get_source(const GraphCall &p_call);
	// Cuts a value for display. A container deeper than max_depth shows as a short text, and one
	// with more than max_subitems items shows the first of them and a count of the rest. A
	// negative limit cuts nothing.
	static Variant limit(const Variant &p_value, int p_max_subitems, int p_max_depth);
};
