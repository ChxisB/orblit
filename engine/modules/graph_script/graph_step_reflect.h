/**************************************************************************/
/*  graph_step_reflect.h                                                  */

#pragma once

#include "graph_step_kinds.h"

#include "core/object/method_info.h"

// The steps that mirror what the engine registers: methods, properties, signals and the
// builtin and utility functions. Their ports come from reflection, so no step is written by
// hand for one method.

namespace GraphStepReflect {

// A method with no declared return type may still give a value, if it says so.
bool returns_value(const MethodInfo &p_info);

String derive_method(const Dictionary &p_parameters, GraphDerivedPorts &r_ports);
String derive_static(const Dictionary &p_parameters, GraphDerivedPorts &r_ports);
String derive_builtin(const Dictionary &p_parameters, GraphDerivedPorts &r_ports);
String derive_utility(const Dictionary &p_parameters, GraphDerivedPorts &r_ports);
String derive_get_property(const Dictionary &p_parameters, GraphDerivedPorts &r_ports);
String derive_set_property(const Dictionary &p_parameters, GraphDerivedPorts &r_ports);
String derive_emit_on(const Dictionary &p_parameters, GraphDerivedPorts &r_ports);
String derive_new(const Dictionary &p_parameters, GraphDerivedPorts &r_ports);

String resolve_method(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_static(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_builtin(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_utility(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_emit(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_connect(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_call_function(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);

Variant eval_call_method(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_call_builtin(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_call_utility(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_get_property(GraphCall &p_call, const GraphCompiledStep &p_step);

int run_call_method(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_call_static(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_call_builtin(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_call_utility(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_call_function(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_set_property(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_new(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_free(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_emit(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_emit_on(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_connect(GraphCall &p_call, const GraphCompiledStep &p_step);

} // namespace GraphStepReflect
