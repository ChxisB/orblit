/**************************************************************************/
/*  graph_step_scene.h                                                    */

#pragma once

#include "graph_step_kinds.h"

// Steps that reach the running game: its entities, stage, singletons, resources and input.
namespace GraphStepScene {

Variant eval_self(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_stage(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_singleton(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_autoload(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_entity_at_path(GraphCall &p_call, const GraphCompiledStep &p_step);
Variant eval_input_action(GraphCall &p_call, const GraphCompiledStep &p_step);

int run_instantiate_prefab(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_print(GraphCall &p_call, const GraphCompiledStep &p_step);

String resolve_singleton(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_autoload(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_preload(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_resource_path(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
String resolve_input_action(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);

} // namespace GraphStepScene
