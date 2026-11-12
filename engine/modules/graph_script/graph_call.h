/**************************************************************************/
/*  graph_call.h                                                          */

#pragma once

#include "graph_compiled.h"
#include "graph_script_instance.h"

#include "core/containers/hash_map.h"

// One place a call comes back to: the next output of a sequence, or the next round of a loop.
struct GraphPending {
	// Index of the step that pushed it.
	int step = -1;
	int64_t position = 0;
	int64_t end = 0;
	// What an Each goes through, and where it stands in it.
	Variant container;
	Variant cursor;
	bool more = false;
	bool loop = false;
	// Set by a break. The next resume then ends the loop.
	bool stopped = false;
	// Set by resume when nothing is left to do. The runner then drops the frame.
	bool done = false;
	// Returns the flow output of the step to follow.
	int (*resume)(GraphCall &p_call, GraphPending &p_frame) = nullptr;
};

// One run of one function. The frames live in this object and not on the C++ stack, so a
// deep loop cannot overflow it and a call can be set aside and carried on later.
class GraphCall {
	GraphScriptInstance &instance;
	const GraphCompiledFunction &function;
	Vector<Variant> slots;
	HashMap<StringName, Variant> locals;
	Vector<GraphPending> stack;
	Variant result;
	int64_t limit = 0;
	int64_t ran = 0;
	bool returned = false;
	bool failed = false;

	Variant _read_pure(const GraphCompiledStep &p_step, int p_port);
	int _target(int p_step, int p_port) const;
	int _resume();
	int _advance(int p_current);

public:
	GraphCall(GraphScriptInstance &p_instance, const GraphCompiledFunction &p_function, int64_t p_step_limit);

	// Follows the flow wires from the start step until the end, a return or a failure.
	Variant run();

	const GraphCompiledStep &get_step(int p_index) const { return function.steps[p_index]; }
	Object *get_owner() const { return instance.get_owner(); }
	RandomNumberGenerator &get_random() const { return *instance.get_random().ptr(); }
	// The value on a data input. A pure step behind a wire runs now, each time.
	Variant input(const GraphCompiledStep &p_step, int p_port);
	void store(const GraphCompiledStep &p_step, int p_port, const Variant &p_value);

	void push(const GraphPending &p_frame);
	bool has_loop() const;
	void stop_loop();
	void finish(const Variant &p_value);
	void fail(const GraphCompiledStep &p_step, const String &p_message);

	// These four name the variable or local in the "name" parameter of the step.
	Variant read_variable(const GraphCompiledStep &p_step);
	void write_variable(const GraphCompiledStep &p_step, const Variant &p_value);
	Variant read_local(const GraphCompiledStep &p_step);
	void write_local(const GraphCompiledStep &p_step, const Variant &p_value);
};
