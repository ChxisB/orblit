/**************************************************************************/
/*  graph_debug.cpp                                                       */

#include "graph_debug.h"

#include "graph_call.h"
#include "graph_script_language.h"

#include "core/containers/local_vector.h"
#include "core/debugger/script_debugger.h"

namespace {

// The calls running on this thread, outermost first. The debugger's own counts are per thread too.
thread_local LocalVector<GraphCall *> running;

// A script VM does the same on entering and leaving a function. While a step command waits,
// a level below the one that asked does not count steps.
void shift_depth(ScriptDebugger &p_debugger, int p_by) {
	if (p_debugger.get_lines_left() > 0 && p_debugger.get_depth() >= 0) {
		p_debugger.set_depth(p_debugger.get_depth() + p_by);
	}
}

// The line is only looked up in the map of breakpoints when some breakpoint sits on it, because
// building the source costs more than a step.
bool is_breakpoint(const ScriptDebugger &p_debugger, const GraphCall &p_call) {
	const int line = p_call.get_running_step().id;
	return p_debugger.get_breakpoints().has(line) && p_debugger.is_breakpoint(line, GraphDebug::get_source(p_call));
}

Array cut_array(const Array &p_array, int p_max_subitems, int p_max_depth) {
	const int kept = p_max_subitems < 0 ? p_array.size() : MIN(p_array.size(), p_max_subitems);
	Array cut;
	for (int i = 0; i < kept; i++) {
		cut.push_back(GraphDebug::limit(p_array[i], p_max_subitems, p_max_depth));
	}
	if (kept < p_array.size()) {
		cut.push_back(vformat("... %d more", p_array.size() - kept));
	}
	return cut;
}

Dictionary cut_dictionary(const Dictionary &p_dictionary, int p_max_subitems, int p_max_depth) {
	const Array keys = p_dictionary.keys();
	const int kept = p_max_subitems < 0 ? keys.size() : MIN(keys.size(), p_max_subitems);
	Dictionary cut;
	for (int i = 0; i < kept; i++) {
		cut[keys[i]] = GraphDebug::limit(p_dictionary[keys[i]], p_max_subitems, p_max_depth);
	}
	if (kept < keys.size()) {
		cut["..."] = vformat("%d more", keys.size() - kept);
	}
	return cut;
}

GraphCall *at_level(int p_level) {
	return p_level >= 0 && p_level < int(running.size()) ? running[running.size() - 1 - p_level] : nullptr;
}

Error parse_message(void *p_user, const String &p_msg, const Array &p_args, bool &r_captured) {
	r_captured = p_msg == "wire";
	if (!r_captured) {
		return OK;
	}
	ERR_FAIL_COND_V(p_args.size() != 3, ERR_INVALID_DATA);
	const Variant value = GraphDebug::limit(GraphDebug::get_wire_value(p_args[0], p_args[1], p_args[2]), 16, 3);
	EngineDebugger::get_singleton()->send_message("graph_debug:wire", Array{ p_args[0], p_args[1], p_args[2], value.stringify() });
	return OK;
}

} // namespace

GraphDebug::Scope::Scope(GraphCall &p_call) {
	if (!EngineDebugger::is_active()) {
		return;
	}
	listed = true;
	running.push_back(&p_call);
	shift_depth(*EngineDebugger::get_script_debugger(), 1);
}

GraphDebug::Scope::~Scope() {
	if (!listed) {
		return;
	}
	running.remove_at(running.size() - 1);
	// The debugger may have gone since the call began.
	if (EngineDebugger::is_active()) {
		shift_depth(*EngineDebugger::get_script_debugger(), -1);
	}
}

void GraphDebug::_stop_if_due(const GraphCall &p_call) {
	ScriptDebugger *debugger = EngineDebugger::get_script_debugger();
	bool stepped = false;
	if (debugger->get_lines_left() > 0) {
		if (debugger->get_depth() <= 0) {
			debugger->set_lines_left(debugger->get_lines_left() - 1);
		}
		stepped = debugger->get_lines_left() <= 0;
	}
	const bool at_breakpoint = is_breakpoint(*debugger, p_call);
	if (stepped || at_breakpoint) {
		GraphScriptLanguage *language = GraphScriptLanguage::get_singleton();
		language->set_break_reason(at_breakpoint ? "Breakpoint" : "Step");
		debugger->debug(language, true, false);
	}
}

// The error text is already the break reason, because the call reported it before this.
void GraphDebug::on_error() {
	if (!EngineDebugger::is_active()) {
		return;
	}
	ScriptDebugger *debugger = EngineDebugger::get_script_debugger();
	if (!debugger->is_ignoring_error_breaks()) {
		debugger->debug(GraphScriptLanguage::get_singleton(), true, true);
	}
}

int GraphDebug::get_level_count() {
	return running.size();
}

const GraphCall *GraphDebug::get_level(int p_level) {
	return at_level(p_level);
}

Variant GraphDebug::get_wire_value(int p_level, int p_step_id, int p_port) {
	GraphCall *call = at_level(p_level);
	if (!call) {
		return Variant();
	}
	for (const GraphCompiledStep &step : call->get_function().steps) {
		if (step.id == p_step_id) {
			return p_port >= 0 && p_port < step.inputs.size() ? call->peek(step, p_port) : Variant();
		}
	}
	return Variant();
}

void GraphDebug::initialize() {
	EngineDebugger::register_message_capture("graph_debug", EngineDebugger::Capture(nullptr, parse_message));
}

void GraphDebug::deinitialize() {
	EngineDebugger::unregister_message_capture("graph_debug");
}

String GraphDebug::make_source(const String &p_script_path, const StringName &p_function) {
	return p_script_path + "#" + p_function;
}

String GraphDebug::get_source(const GraphCall &p_call) {
	return make_source(p_call.get_instance().get_script()->get_path(), p_call.get_function().name);
}

Variant GraphDebug::limit(const Variant &p_value, int p_max_subitems, int p_max_depth) {
	const Variant::Type type = p_value.get_type();
	if (type != Variant::ARRAY && type != Variant::DICTIONARY) {
		return p_value;
	}
	if (p_max_depth == 0) {
		const int size = type == Variant::ARRAY ? Array(p_value).size() : Dictionary(p_value).size();
		return vformat("%s (%d items)", Variant::get_type_name(type), size);
	}
	const int deeper = p_max_depth < 0 ? -1 : p_max_depth - 1;
	return type == Variant::ARRAY ? Variant(cut_array(p_value, p_max_subitems, deeper)) : Variant(cut_dictionary(p_value, p_max_subitems, deeper));
}
