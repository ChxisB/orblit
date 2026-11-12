/**************************************************************************/
/*  graph_step_latent.h                                                   */

#pragma once

#include "graph_step_kinds.h"

// Steps that set the call aside until something happens. The call carries on from the same
// step, with its loops and locals as they were.
namespace GraphStepLatent {

int run_wait(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_await_signal(GraphCall &p_call, const GraphCompiledStep &p_step);

String resolve_await_signal(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);

} // namespace GraphStepLatent
