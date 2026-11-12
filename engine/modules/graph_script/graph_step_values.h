/**************************************************************************/
/*  graph_step_values.h                                                   */

#pragma once

#include "graph_step_kinds.h"

// Steps that make, read or change a value.
namespace GraphStepValues {

// Gives the value the resolve function of the step stored. Constants, preloads and resource
// paths are worked out once, when the function compiles.
Variant eval_stored(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_make_array(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_make_dictionary(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_compose(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_decompose(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_convert(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_cast(GraphCall &p_call, const GraphCompiledStep &p_step);

String resolve_constant(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_compose(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_decompose(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_convert(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_cast(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);

} // namespace GraphStepValues
