/**************************************************************************/
/*  graph_step_flow.h                                                     */

#pragma once

#include "graph_step_kinds.h"

// Steps that choose a path or a value by chance or by comparison.
namespace GraphStepFlow {

Variant eval_pick_random(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_select(GraphCall &p_call, const GraphCompiledStep &p_step);

int run_chance(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_switch(GraphCall &p_call, const GraphCompiledStep &p_step);

String resolve_switch(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);

} // namespace GraphStepFlow
