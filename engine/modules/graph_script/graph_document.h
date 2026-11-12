/**************************************************************************/
/*  graph_document.h                                                      */

#pragma once

#include "graph_step_catalogue.h"

#include "core/containers/vector.h"
#include "core/string/ustring.h"

class GraphScript;

// What an editor draws, as JSON text. The editor bridge marshals strings and not variants, and
// one string holds a whole script, so the editor reads it in one call after every change.
// Every value a step or variable holds is Variant text, the same text the edit methods take.
namespace GraphDocument {

// The script with its functions, steps, ports, wires, variables, signals, errors and warnings.
// A step of a kind this build does not know has no ports. Answers only, changes nothing.
String write(const GraphScript &p_script);

// The palette entries, each with the parameters to set after adding the step.
String write_entries(const Vector<GraphStepEntry> &p_entries);

} // namespace GraphDocument
