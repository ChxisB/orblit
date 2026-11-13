/**************************************************************************/
/*  graph_compiler.h                                                      */

#pragma once

#include "graph_compiled.h"
#include "graph_function.h"

namespace GraphCompiler {

// A function that does not compile comes back with its error set and no usable steps.
GraphCompiledFunction compile(const GraphFunction &p_function, const Vector<StringName> &p_variables);

} // namespace GraphCompiler
