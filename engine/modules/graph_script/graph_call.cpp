/**************************************************************************/
/*  graph_call.cpp                                                        */

#include "graph_call.h"

#include "graph_script_language.h"

GraphCall::GraphCall(GraphScriptInstance &p_instance, const GraphCompiledFunction &p_function, int64_t p_step_limit) :
		instance(p_instance), function(p_function), limit(p_step_limit) {
	slots.resize(function.slot_count);
}

int GraphCall::_target(int p_step, int p_port) const {
	return p_port == GraphFlow::END ? -1 : function.steps[p_step].next[p_port];
}

int GraphCall::_resume() {
	GraphPending &frame = stack.write[stack.size() - 1];
	const int step = frame.step;
	const int port = frame.resume(*this, frame);
	if (frame.done) {
		stack.remove_at(stack.size() - 1);
	}
	return _target(step, port);
}

int GraphCall::_advance(int p_current) {
	if (p_current < 0) {
		return _resume();
	}
	const GraphCompiledStep &step = get_step(p_current);
	return _target(p_current, step.kind->run(*this, step));
}

Variant GraphCall::run() {
	int current = function.start < 0 ? -1 : _target(function.start, 0);
	while (!returned && !failed && (current >= 0 || !stack.is_empty())) {
		// With no step to run, the step on top of the stack is the one that resumes.
		const GraphCompiledStep &at = get_step(current >= 0 ? current : stack[stack.size() - 1].step);
		if (++ran > limit) {
			fail(at, vformat("stopped after %d steps. A loop may never end.", limit));
			break;
		}
		current = _advance(current);
	}
	return result;
}

Variant GraphCall::input(const GraphCompiledStep &p_step, int p_port) {
	const GraphCompiledInput &source = p_step.inputs[p_port];
	if (source.step < 0) {
		return source.value;
	}
	const GraphCompiledStep &from = get_step(source.step);
	// shortcut: a pure step is not cached, so a value read twice runs twice. Cache per call if
	// a graph shows it costs time.
	return from.kind->is_pure() ? from.kind->eval(*this, from) : slots[from.slot + source.port];
}

void GraphCall::store(const GraphCompiledStep &p_step, int p_port, const Variant &p_value) {
	slots.write[p_step.slot + p_port] = p_value;
}

void GraphCall::push(const GraphPending &p_frame) {
	stack.push_back(p_frame);
}

bool GraphCall::has_loop() const {
	for (const GraphPending &frame : stack) {
		if (frame.loop) {
			return true;
		}
	}
	return false;
}

// Drops the frames above the nearest loop. Its next resume ends it.
void GraphCall::stop_loop() {
	while (!stack.is_empty() && !stack[stack.size() - 1].loop) {
		stack.remove_at(stack.size() - 1);
	}
	if (!stack.is_empty()) {
		stack.write[stack.size() - 1].stopped = true;
	}
}

void GraphCall::finish(const Variant &p_value) {
	result = p_value;
	returned = true;
}

void GraphCall::fail(const GraphCompiledStep &p_step, const String &p_message) {
	if (failed) {
		return;
	}
	failed = true;
	GraphScriptLanguage::get_singleton()->report_error(vformat("Function '%s', step %d: %s", function.name, p_step.id, p_message));
}

Variant GraphCall::read_variable(const GraphCompiledStep &p_step) {
	Variant value;
	instance.get(p_step.parameters["name"], value);
	return value;
}

void GraphCall::write_variable(const GraphCompiledStep &p_step, const Variant &p_value) {
	if (!instance.set(p_step.parameters["name"], p_value)) {
		fail(p_step, vformat("cannot store a %s in the variable '%s'.", Variant::get_type_name(p_value.get_type()), String(p_step.parameters["name"])));
	}
}

Variant GraphCall::read_local(const GraphCompiledStep &p_step) {
	const StringName name = p_step.parameters["name"];
	const HashMap<StringName, Variant>::ConstIterator found = locals.find(name);
	if (!found) {
		fail(p_step, vformat("the local '%s' has no value yet.", name));
		return Variant();
	}
	return found->value;
}

void GraphCall::write_local(const GraphCompiledStep &p_step, const Variant &p_value) {
	locals[p_step.parameters["name"]] = p_value;
}
