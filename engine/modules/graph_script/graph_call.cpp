/**************************************************************************/
/*  graph_call.cpp                                                        */

#include "graph_call.h"

#include "graph_debug.h"
#include "graph_script_language.h"

#include "core/object/class_registry.h"

GraphCall::GraphCall(GraphScriptInstance *p_instance, const GraphCompiledFunction *p_function, int64_t p_step_limit) :
		instance(p_instance), function(p_function), limit(p_step_limit) {
	slots.resize(function->slot_count);
}

void GraphCall::register_members() {
	ClassRegistry::bind_vararg_method(METHOD_FLAGS_DEFAULT, "_woken", &GraphCall::_woken, MethodInfo("_woken"));
	ADD_SIGNAL(MethodInfo("completed", PropertyInfo(Variant::NIL, "result", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_NIL_IS_VARIANT)));
}

int GraphCall::_target(int p_step, int p_port) const {
	return p_port == GraphFlow::END ? -1 : function->steps[p_step].next[p_port];
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

// An argument fills the output after "then" that its name sits on. A declared local starts as
// the default of its type.
void GraphCall::_begin(const Vector<Variant> &p_arguments) {
	if (function->start >= 0) {
		const int first = get_step(function->start).slot + 1;
		for (int i = 0; i < p_arguments.size(); i++) {
			slots.write[first + i] = p_arguments[i];
		}
	}
	for (const GraphTyped &local : function->signature.locals) {
		locals[local.name] = GraphSignatures::make_default(local.type);
	}
}

Variant GraphCall::run(const Vector<Variant> &p_arguments) {
	_begin(p_arguments);
	current = function->start < 0 ? -1 : _target(function->start, 0);
	_loop();
	return result;
}

// The step budget counts from each resume, so a loop that waits on every round is not cut off
// by the rounds before it.
void GraphCall::_loop() {
	const GraphDebug::Scope listed(*this);
	ran = 0;
	while (!suspended && !returned && !failed && (current >= 0 || !stack.is_empty())) {
		// A resume has no step of its own to stop at, so only a step the call moves to can stop.
		if (current >= 0) {
			GraphDebug::before_step(*this);
		}
		const GraphCompiledStep &at = get_running_step();
		if (++ran > limit) {
			fail(at, vformat("stopped after %d steps. A loop may never end.", limit));
			break;
		}
		current = _advance(current);
	}
}

void GraphCall::wait_for(Object *p_emitter, const StringName &p_signal, const GraphPending &p_frame) {
	stack.push_back(p_frame);
	emitter = p_emitter->get_instance_id();
	wake_signal = p_signal;
	suspended = true;
	instance->add_waiting(Ref<GraphCall>(this));
	p_emitter->connect(p_signal, Callable(this, "_woken"), CONNECT_ONE_SHOT);
}

// What the signal sent goes to the frame on top, which is the step that waited.
void GraphCall::_woken(const Variant **p_args, int p_argcount, Callable::CallError &r_error) {
	// The instance held the last reference, and it lets go of it here.
	const Ref<GraphCall> keep(this);
	emitter = ObjectID();
	if (!suspended) {
		return;
	}
	Array heard;
	for (int i = 0; i < p_argcount; i++) {
		heard.push_back(*p_args[i]);
	}
	stack.write[stack.size() - 1].container = heard;
	instance->remove_waiting(this);
	suspended = false;
	_loop();
	if (!suspended) {
		emit_signal("completed", result);
	}
}

void GraphCall::drop() {
	if (Object *source = ObjectRegistry::get_instance(emitter)) {
		const Callable wake(this, "_woken");
		if (source->is_connected(wake_signal, wake)) {
			source->disconnect(wake_signal, wake);
		}
	}
	emitter = ObjectID();
	suspended = false;
	instance = nullptr;
	function = nullptr;
	slots.clear();
	locals.clear();
	stack.clear();
}

Variant GraphCall::_read_pure(const GraphCompiledStep &p_step, int p_port) {
	const Variant value = p_step.kind->eval(*this, p_step);
	if (p_step.next.size() <= 1) {
		return value;
	}
	const Array outputs = value;
	return p_port < outputs.size() ? outputs[p_port] : Variant();
}

Variant GraphCall::input(const GraphCompiledStep &p_step, int p_port) {
	const GraphCompiledInput &source = p_step.inputs[p_port];
	if (source.step < 0) {
		// The same reason as a stored constant: a step may change an array or dictionary it is given.
		return source.value.duplicate(true);
	}
	const GraphCompiledStep &from = get_step(source.step);
	// shortcut: a pure step is not cached, so a value read twice runs twice. Cache per call if
	// a graph shows it costs time.
	return from.kind->is_pure() ? _read_pure(from, source.port) : slots[from.slot + source.port];
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

void GraphCall::finish(const GraphCompiledStep &p_step, const Variant &p_value) {
	returned = true;
	if (!GraphSignatures::convert(function->signature.returns, p_value, result)) {
		fail(p_step, vformat("cannot return a %s from a function that gives a %s.", Variant::get_type_name(p_value.get_type()), Variant::get_type_name(function->signature.returns)));
	}
}

void GraphCall::fail(const GraphCompiledStep &p_step, const String &p_message) {
	if (failed) {
		return;
	}
	failed = true;
	GraphScriptLanguage::get_singleton()->report_error(vformat("Function '%s', step %d: %s", function->name, p_step.id, p_message));
	GraphDebug::on_error();
}

void GraphCall::list_locals(List<String> *r_names, List<Variant> *r_values) const {
	if (function->start >= 0) {
		const int first = get_step(function->start).slot + 1;
		for (int i = 0; i < function->signature.arguments.size(); i++) {
			r_names->push_back(function->signature.arguments[i].name);
			r_values->push_back(slots[first + i]);
		}
	}
	for (const GraphTyped &local : function->signature.locals) {
		const HashMap<StringName, Variant>::ConstIterator found = locals.find(local.name);
		r_names->push_back(local.name);
		r_values->push_back(found ? found->value : Variant());
	}
}

Variant GraphCall::read_variable(const GraphCompiledStep &p_step) {
	Variant value;
	instance->get(p_step.parameters["name"], value);
	return value;
}

void GraphCall::write_variable(const GraphCompiledStep &p_step, const Variant &p_value) {
	if (!instance->set(p_step.parameters["name"], p_value)) {
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
	const StringName name = p_step.parameters["name"];
	const GraphTyped *declared = function->signature.find_local(name);
	Variant stored = p_value;
	if (declared && !GraphSignatures::convert(declared->type, p_value, stored)) {
		fail(p_step, vformat("cannot store a %s in the local '%s', which holds a %s.", Variant::get_type_name(p_value.get_type()), name, Variant::get_type_name(declared->type)));
		return;
	}
	locals[name] = stored;
}
