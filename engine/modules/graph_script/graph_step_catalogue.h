/**************************************************************************/
/*  graph_step_catalogue.h                                                */

#pragma once

#include "graph_step_kinds.h"

#include "core/containers/vector.h"
#include "core/string/ustring.h"
#include "core/variant/dictionary.h"

// One step a palette can offer. Adding it to a function means add_step with the kind, then
// set_step_parameter for each of the parameters.
struct GraphStepEntry {
	StringName kind;
	// What the palette sorts it under: a step category or the class the member belongs to.
	String group;
	String label;
	Dictionary parameters;
};

// What a wire dragged from a port needs of the new step. With no dragged side every step fits.
struct GraphWireFilter {
	GraphPinSide dragged_from = GraphPinSide::NONE;
	// The type the dragged data wire carries. NIL fits any.
	Variant::Type type = Variant::NIL;
	GraphPortSort sort = GraphPortSort::DATA;
};

namespace GraphStepCatalogue {

// With no text this lists the steps of the kind table. With text it lists the ones that match,
// then every method, property, signal and class of the engine that matches.
Vector<GraphStepEntry> find(const String &p_text, const GraphWireFilter &p_filter);

// The functions a script can override on a class and the classes above it, as entries of the
// event kind. A build without debug features registers no virtual methods, so this is empty there.
Vector<GraphStepEntry> find_overrides(const StringName &p_class);

} // namespace GraphStepCatalogue
