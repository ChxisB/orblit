/**************************************************************************/
/*  local_debugger.h                                                      */

#pragma once

#include "core/debugger/engine_debugger.h"
#include "core/containers/list.h"

class LocalDebugger : public EngineDebugger {
private:
	struct ScriptsProfiler;

	ScriptsProfiler *scripts_profiler = nullptr;

	String target_function;
	HashMap<String, String> options;

	Pair<String, int> to_breakpoint(const String &p_line);
	void print_variables(const List<String> &p_names, const List<Variant> &p_values, const String &p_variable_prefix);

public:
	void debug(bool p_can_continue, bool p_is_error_breakpoint);
	void send_message(const String &p_message, const Array &p_args);
	void send_error(const String &p_func, const String &p_file, int p_line, const String &p_err, const String &p_descr, bool p_editor_notify, ErrorHandlerType p_type);

	LocalDebugger();
	~LocalDebugger();
};
