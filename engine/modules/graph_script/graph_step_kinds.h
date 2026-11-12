/**************************************************************************/
/*  graph_step_kinds.h                                                    */

#pragma once

#include "core/containers/vector.h"
#include "core/string/string_name.h"
#include "core/variant/dictionary.h"
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

enum class GraphPinSide {
	NONE,
	INPUT,
	OUTPUT,
};

// Ports a step adds after its fixed ones. The "pins" parameter of the step says how many.
// A pin is one port named by the name below and its number. With a second name a pin is two
// ports, such as a key and a value.
struct GraphPinGrowth {
	GraphPinSide side = GraphPinSide::NONE;
	const char *name = "";
	const char *second_name = nullptr;
	GraphPortSort sort = GraphPortSort::DATA;
	Variant::Type type = Variant::NIL;
	int minimum = 0;
};

// One port of a step, fixed or grown.
struct GraphPort {
	String name;
	GraphPortSort sort = GraphPortSort::DATA;
	Variant::Type type = Variant::NIL;
};

// A pure kind gives its output when a wire asks for it. A kind with several outputs gives an
// array with one entry per output.
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
	GraphPinGrowth growth = {};

	bool is_pure() const { return eval != nullptr; }
	// Starts a function. It has no input to run from and nothing to run itself.
	bool is_start() const { return eval == nullptr && run == nullptr; }
};

namespace GraphStepKinds {

// Returns null for a name this build does not know.
const GraphStepKind *find(const StringName &p_name);

// The most pins one step may have.
constexpr int MAX_PINS = 64;

// How many pins a step of this kind has when it holds these parameters. Never below the minimum.
int get_pin_count(const GraphStepKind &p_kind, const Dictionary &p_parameters);
// The fixed ports plus the pins, on one side.
int get_port_count(const GraphStepKind &p_kind, const Dictionary &p_parameters, GraphPinSide p_side);
GraphPort get_port(const GraphStepKind &p_kind, const Dictionary &p_parameters, GraphPinSide p_side, int p_index);

} // namespace GraphStepKinds
