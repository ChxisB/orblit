/**************************************************************************/
/*  graph_step_dialogue.h                                                 */

#pragma once

#include "graph_step_kinds.h"

// Steps that show a line of dialogue and follow the flow wire of the choice a player picks.
namespace GraphStepDialogue {

// The project setting that names the prefab to show a message in. Empty means a DialogueBox.
constexpr const char *PREFAB_SETTING = "graph_script/dialogue/prefab";
constexpr int MAX_CHOICES = 10;

Variant eval_choice(GraphCall &p_call, const GraphCompiledStep &p_step);
int run_message(GraphCall &p_call, const GraphCompiledStep &p_step);

String derive_message(const Dictionary &p_parameters, GraphDerivedPorts &r_ports);

} // namespace GraphStepDialogue
