/**************************************************************************/
/*  graph_step_kinds.h                                                    */

#pragma once

#include "core/containers/vector.h"
#include "core/string/string_name.h"
#include "core/variant/variant.h"

class GraphCall;
struct GraphCompiledStep;

namespace GraphFlow {

// A run function returns the number of the flow output to follow, or this. The chain ends
// and the runner goes back to the loop or sequence on top of its stack, if there is one.
constexpr int END = -1;

} // namespace GraphFlow

enum class GraphPortSort {
	FLOW,
	DATA,
};

struct GraphPortSpec {
	const char *name;
	GraphPortSort sort;
	// Variant::NIL on a data port accepts any type.
	Variant::Type type;
};

struct GraphPortList {
	const GraphPortSpec *items;
	int count;
};

// A pure kind gives its single output when a wire asks for it.
using GraphEvalFn = Variant (*)(GraphCall &p_call, const GraphCompiledStep &p_step);
// A flow kind runs when a flow wire reaches it and says which flow output comes next.
using GraphRunFn = int (*)(GraphCall &p_call, const GraphCompiledStep &p_step);
// Reads the parameters of a step when its function compiles. Returns the problem, or an
// empty string when the step is fine.
using GraphResolveFn = String (*)(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);

struct GraphStepKind {
	const char *name;
	const char *category;
	GraphPortList inputs;
	GraphPortList outputs;
	GraphEvalFn eval;
	GraphRunFn run;
	GraphResolveFn resolve;
	// A cycle of flow wires is allowed when it passes through a loop.
	bool loop;

	bool is_pure() const { return eval != nullptr; }
	// Starts a function. It has no input to run from and nothing to run itself.
	bool is_start() const { return eval == nullptr && run == nullptr; }
};

namespace GraphStepKinds {

// Returns null for a name this build does not know.
const GraphStepKind *find(const StringName &p_name);

} // namespace GraphStepKinds
