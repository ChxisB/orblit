/**************************************************************************/
/*  graph_warnings.h                                                      */

#pragma once

#include "core/containers/vector.h"
#include "core/string/ustring.h"

class GraphScript;

struct GraphWarning {
	// One of the names a function can list in the ignored_warnings of its start step.
	String kind;
	// Empty for a warning about the whole script.
	String function;
	// The step it is about, or -1.
	int step = -1;
	String name;
	String text;
};

namespace GraphWarnings {

// What is likely a mistake in a script that compiles. A function that does not compile adds
// none of its own, because its error is the first thing to fix. This answers a question and
// changes nothing.
Vector<GraphWarning> find(const GraphScript &p_script);

} // namespace GraphWarnings
