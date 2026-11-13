/**************************************************************************/
/*  graph_step_latent.cpp                                                 */

#include "graph_step_latent.h"

#include "graph_call.h"
#include "graph_step_reflect.h"

#include "scene/stage/stage.h"

namespace {

// These numbers follow the port lists in graph_step_kinds.cpp.
constexpr int THEN = 0;
constexpr int SECONDS = 1;
constexpr int ARGUMENTS = 1;
constexpr int AWAIT_TARGET = 1;

int resume_then(GraphCall &p_call, GraphPending &p_frame) {
	p_frame.done = true;
	return THEN;
}

// The step puts what the signal sent on its second output, as one array.
// shortcut: one array for any signal, so a signal with three arguments is read by index. Give
// the step a pin per argument when a graph needs it.
int resume_arguments(GraphCall &p_call, GraphPending &p_frame) {
	p_call.store(p_call.get_step(p_frame.step), ARGUMENTS, p_frame.container);
	p_frame.done = true;
	return THEN;
}

} // namespace

namespace GraphStepLatent {

GraphPending make_frame(const GraphCompiledStep &p_step, int (*p_resume)(GraphCall &, GraphPending &)) {
	GraphPending frame;
	frame.step = p_step.index;
	frame.resume = p_resume;
	return frame;
}

int suspend(GraphCall &p_call, const GraphPending &p_frame, Object *p_emitter, const StringName &p_signal) {
	p_call.wait_for(p_emitter, p_signal, p_frame);
	return GraphFlow::END;
}

int run_wait(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Stage *stage = Stage::get_singleton();
	if (!stage) {
		p_call.fail(p_step, "needs a stage to keep the time.");
		return GraphFlow::END;
	}
	// The timer stops with the stage, so a paused game also pauses every wait.
	const Ref<StageTimer> timer = stage->create_timer(p_call.input(p_step, SECONDS), false);
	return suspend(p_call, make_frame(p_step, resume_then), timer.ptr(), SNAME("timeout"));
}

int run_await_signal(GraphCall &p_call, const GraphCompiledStep &p_step) {
	Object *emitter = GraphStepReflect::read_target(p_call, p_step, AWAIT_TARGET);
	if (!emitter) {
		return GraphFlow::END;
	}
	const StringName signal = p_step.parameters["signal"];
	if (!emitter->has_signal(signal)) {
		p_call.fail(p_step, vformat("the object has no signal named '%s'.", signal));
		return GraphFlow::END;
	}
	return suspend(p_call, make_frame(p_step, resume_arguments), emitter, signal);
}

String resolve_await_signal(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	const Variant signal = p_step.parameters.get("signal", Variant());
	const bool is_text = signal.get_type() == Variant::STRING || signal.get_type() == Variant::STRING_NAME;
	return is_text && !String(signal).is_empty() ? String() : "the step needs a 'signal' parameter.";
}

} // namespace GraphStepLatent
