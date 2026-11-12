/**************************************************************************/
/*  graph_call.h                                                          */

#pragma once

#include "graph_compiled.h"
#include "graph_script_instance.h"

#include "core/containers/hash_map.h"
#include "core/object/ref_counted.h"

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
// deep loop cannot overflow it and a call can be set aside and carried on later. A call that
// waits is handed to its caller, who may connect to its "completed" signal.
class GraphCall : public RefCounted {
	ORBLIT_CLASS(GraphCall, RefCounted);

	// Both are null once the call is dropped, so no call reaches an instance or a function that
	// is gone.
	GraphScriptInstance *instance = nullptr;
	const GraphCompiledFunction *function = nullptr;
	Vector<Variant> slots;
	HashMap<StringName, Variant> locals;
	Vector<GraphPending> stack;
	Variant result;
	// The step to run next, or -1 when the top frame of the stack resumes instead.
	int current = -1;
	int64_t limit = 0;
	int64_t ran = 0;
	bool returned = false;
	bool failed = false;
	bool suspended = false;
	// What a suspended call waits for.
	ObjectID emitter;
	StringName wake_signal;

	Variant _read_pure(const GraphCompiledStep &p_step, int p_port);
	int _target(int p_step, int p_port) const;
	void _begin(const Vector<Variant> &p_arguments);
	int _resume();
	int _advance(int p_current);
	void _loop();
	void _woken(const Variant **p_args, int p_argcount, Callable::CallError &r_error);

protected:
	static void register_members();

public:
	GraphCall(GraphScriptInstance *p_instance, const GraphCompiledFunction *p_function, int64_t p_step_limit);

	// Follows the flow wires from the start step until the end, a return, a failure or a wait.
	// The caller has checked the arguments against the signature.
	Variant run(const Vector<Variant> &p_arguments);

	// True from the moment a latent step sets the call aside until the signal it waits for.
	bool is_waiting() const { return suspended; }
	// Sets the call aside until the emitter sends the signal. The frame is what resumes it, and
	// the arguments of the signal reach it as an array in its container.
	void wait_for(Object *p_emitter, const StringName &p_signal, const GraphPending &p_frame);
	// Lets go of everything the call holds. The instance calls this when it or its script goes.
	void drop();

	const GraphCompiledStep &get_step(int p_index) const { return function->steps[p_index]; }
	GraphScriptInstance &get_instance() const { return *instance; }
	Object *get_owner() const { return instance->get_owner(); }
	RandomNumberGenerator &get_random() const { return *instance->get_random().ptr(); }
	// The value on a data input. A pure step behind a wire runs now, each time.
	Variant input(const GraphCompiledStep &p_step, int p_port);
	void store(const GraphCompiledStep &p_step, int p_port, const Variant &p_value);

	void push(const GraphPending &p_frame);
	bool has_loop() const;
	void stop_loop();
	// Converts the value to the type the function declares, and fails the call when it cannot.
	void finish(const GraphCompiledStep &p_step, const Variant &p_value);
	void fail(const GraphCompiledStep &p_step, const String &p_message);

	// These four name the variable or local in the "name" parameter of the step.
	Variant read_variable(const GraphCompiledStep &p_step);
	void write_variable(const GraphCompiledStep &p_step, const Variant &p_value);
	Variant read_local(const GraphCompiledStep &p_step);
	void write_local(const GraphCompiledStep &p_step, const Variant &p_value);
};
