/**************************************************************************/
/*  graph_step_run.h                                                      */

#pragma once

#include "graph_step_kinds.h"

// What each step kind does. The table in graph_step_kinds.cpp points at these.
namespace GraphStepRun {

Variant eval_variable(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_local(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_operator(GraphCall &p_call, const GraphCompiledStep &p_step);

int run_return(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_branch(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_while(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_count_loop(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_each(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_sequence(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_break(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_set_variable(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_set_local(GraphCall &p_call, const GraphCompiledStep &p_step);

String resolve_variable(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_local(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_operator(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);

} // namespace GraphStepRun
