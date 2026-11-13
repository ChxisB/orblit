/**************************************************************************/
/*  graph_step_latent.h                                                   */

#pragma once

#include "graph_call.h"
#include "graph_step_kinds.h"

// Steps that set the call aside until something happens. The call carries on from the same
// step, with its loops and locals as they were.
namespace GraphStepLatent {

// A frame for the step. The call carries on from it through p_resume.
GraphPending make_frame(const GraphCompiledStep &p_step, int (*p_resume)(GraphCall &, GraphPending &));
// Sets the call aside until the emitter sends the signal, and returns where the flow goes in the
// meantime, which is nowhere. The signal arrives in the container of the frame as an array.
int suspend(GraphCall &p_call, const GraphPending &p_frame, Object *p_emitter, const StringName &p_signal);

int run_wait(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_await_signal(GraphCall &p_call, const GraphCompiledStep &p_step);

String resolve_await_signal(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);

} // namespace GraphStepLatent
