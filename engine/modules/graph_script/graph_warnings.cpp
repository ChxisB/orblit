/**************************************************************************/
/*  graph_warnings.cpp                                                    */

#include "graph_warnings.h"

#include "graph_script.h"

#include "core/containers/hash_set.h"

namespace {

constexpr const char *UNUSED_VARIABLE = "unused_variable";
constexpr const char *UNUSED_LOCAL = "unused_local";
constexpr const char *UNREACHABLE_STEP = "unreachable_step";
constexpr const char *SHADOWED_NAME = "shadowed_name";

// A name that starts with an underscore says it is meant to go unused.
bool is_quiet(const StringName &p_name) {
	return String(p_name).begins_with("_");
}

bool is_ignored(const GraphCompiledFunction &p_function, const char *p_kind) {
	return p_function.signature.ignored_warnings.has(StringName(p_kind));
}

// Adds the "name" parameter of every step of the two kinds.
void collect_names(const GraphFunction &p_function, const char *p_read_kind, const char *p_write_kind, HashSet<StringName> &r_names) {
	for (int i = 0; i < p_function.get_step_count(); i++) {
		const GraphStep &step = p_function.get_step_at(i);
		if (step.kind == p_read_kind || step.kind == p_write_kind) {
			r_names.insert(StringName(String(step.parameters.get("name", String()))));
		}
	}
}

void find_unused_locals(const GraphCompiledFunction &p_function, const GraphFunction &p_source, Vector<GraphWarning> &r_found) {
	if (is_ignored(p_function, UNUSED_LOCAL)) {
		return;
	}
	HashSet<StringName> used;
	collect_names(p_source, "local", "set_local", used);
	for (const GraphTyped &local : p_function.signature.locals) {
		if (!used.has(local.name) && !is_quiet(local.name)) {
			r_found.push_back({ UNUSED_LOCAL, p_function.name, -1, local.name, vformat("The local '%s' of '%s' is never used.", local.name, p_function.name) });
		}
	}
}

// The steps a run can reach from the start by following flow wires.
HashSet<int> find_reached(const GraphCompiledFunction &p_function) {
	HashSet<int> reached;
	Vector<int> pending;
	pending.push_back(p_function.start);
	reached.insert(p_function.start);
	while (!pending.is_empty()) {
		const int index = pending[pending.size() - 1];
		pending.remove_at(pending.size() - 1);
		for (const int next : p_function.steps[index].next) {
			if (next >= 0 && !reached.has(next)) {
				reached.insert(next);
				pending.push_back(next);
			}
		}
	}
	return reached;
}

// A step that only gives a value runs when something asks for it, so only steps that run count.
void find_unreachable_steps(const GraphCompiledFunction &p_function, Vector<GraphWarning> &r_found) {
	if (is_ignored(p_function, UNREACHABLE_STEP)) {
		return;
	}
	const HashSet<int> reached = find_reached(p_function);
	for (const GraphCompiledStep &step : p_function.steps) {
		if (step.kind->run && !reached.has(step.index)) {
			r_found.push_back({ UNREACHABLE_STEP, p_function.name, step.id, String(), vformat("Step %d of '%s' cannot be reached from the start.", step.id, p_function.name) });
		}
	}
}

void add_shadowed(const GraphCompiledFunction &p_function, const StringName &p_name, Vector<GraphWarning> &r_found) {
	r_found.push_back({ SHADOWED_NAME, p_function.name, -1, p_name, vformat("The name '%s' in '%s' hides another of the same name.", p_name, p_function.name) });
}

// An argument hides a script variable. A local hides a script variable or an argument.
void find_shadowed_names(const GraphScript &p_script, const GraphCompiledFunction &p_function, Vector<GraphWarning> &r_found) {
	if (is_ignored(p_function, SHADOWED_NAME)) {
		return;
	}
	const GraphSignature &signature = p_function.signature;
	for (const GraphTyped &argument : signature.arguments) {
		if (p_script.get_variable(argument.name)) {
			add_shadowed(p_function, argument.name, r_found);
		}
	}
	for (const GraphTyped &local : signature.locals) {
		bool is_argument = false;
		for (const GraphTyped &argument : signature.arguments) {
			is_argument = is_argument || argument.name == local.name;
		}
		if (p_script.get_variable(local.name) || is_argument) {
			add_shadowed(p_function, local.name, r_found);
		}
	}
}

void find_unused_variables(const GraphScript &p_script, const HashSet<StringName> &p_used, Vector<GraphWarning> &r_found) {
	for (const GraphVariable &variable : p_script.get_declared_variables()) {
		// An exported variable is read by the inspector and the scene file.
		if (!variable.exported && !is_quiet(variable.name) && !p_used.has(variable.name)) {
			r_found.push_back({ UNUSED_VARIABLE, String(), -1, variable.name, vformat("The variable '%s' is never used.", variable.name) });
		}
	}
}

} // namespace

// shortcut: a variable has no function to hold a flag, so listing unused_variable on any function
// turns it off for the whole script. Give a variable its own flag when someone needs one.
Vector<GraphWarning> GraphWarnings::find(const GraphScript &p_script) {
	Vector<GraphWarning> found;
	HashSet<StringName> used_variables;
	bool variables_ignored = false;
	for (const GraphCompiledFunction &function : p_script.get_compiled_functions()) {
		const GraphFunction *source = p_script.get_function(function.name);
		ERR_CONTINUE(!source);
		// The steps count even when the function does not compile, so its error is the only noise.
		collect_names(*source, "get", "set", used_variables);
		variables_ignored = variables_ignored || is_ignored(function, UNUSED_VARIABLE);
		if (function.error.is_empty() && function.start >= 0) {
			find_unused_locals(function, *source, found);
			find_unreachable_steps(function, found);
			find_shadowed_names(p_script, function, found);
		}
	}
	if (!variables_ignored) {
		find_unused_variables(p_script, used_variables, found);
	}
	return found;
}
