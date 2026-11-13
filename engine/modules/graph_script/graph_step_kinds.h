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
	// An optional input may stay unwired and without a value. It then reads nil.
	bool optional = false;
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

namespace GraphStepKinds {

// The most pins one step may have, unless its kind sets a lower maximum.
constexpr int MAX_PINS = 64;

} // namespace GraphStepKinds

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
	int maximum = GraphStepKinds::MAX_PINS;
};

// One port of a step, fixed, derived or grown.
struct GraphPort {
	String name;
	GraphPortSort sort = GraphPortSort::DATA;
	Variant::Type type = Variant::NIL;
	// The step parameter that holds the value of an input with no wire. It is not always the name,
	// because the name of an argument may be one of the parameters the step already has.
	// The braces give a default member initializer, which keeps -Wmissing-field-initializers quiet
	// for a port written with fewer values.
	String parameter = {};
	// An optional input with no wire and no value reads the fallback.
	bool optional = false;
	Variant fallback = {};
};

// The ports a step gets from what its parameters name, such as the arguments of a method.
// They follow the fixed ports and come before the pins.
struct GraphDerivedPorts {
	Vector<GraphPort> inputs;
	Vector<GraphPort> outputs;
};

// A pure kind gives its output when a wire asks for it. A kind with several outputs gives an
// array with one entry per output.
using GraphEvalFn = Variant (*)(GraphCall &p_call, const GraphCompiledStep &p_step);
// A flow kind runs when a flow wire reaches it and says which flow output comes next.
using GraphRunFn = int (*)(GraphCall &p_call, const GraphCompiledStep &p_step);
// Reads the parameters of a step when its function compiles. Returns the problem, or an
// empty string when the step is fine.
using GraphResolveFn = String (*)(GraphCompiledStep &p_step, const Vector<StringName> &p_variables);
// Works out the derived ports from the parameters. Returns the problem, such as an unknown
// class, or an empty string. A step with a problem has no ports of this kind.
using GraphDeriveFn = String (*)(const Dictionary &p_parameters, GraphDerivedPorts &r_ports);

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
	// A kind with this column mirrors something the engine registers, and the catalog lists it
	// once per method, property or signal.
	GraphDeriveFn derive = nullptr;

	bool is_pure() const { return eval != nullptr; }
	// Starts a function. It has no input to run from and nothing to run itself.
	bool is_start() const { return eval == nullptr && run == nullptr; }
};

namespace GraphStepKinds {

// Returns null for a name this build does not know.
const GraphStepKind *find(const StringName &p_name);

// The table, so the catalog can list it.
int get_kind_count();
const GraphStepKind &get_kind(int p_index);

// How many pins a step of this kind has when it holds these parameters. Never below the minimum.
int get_pin_count(const GraphStepKind &p_kind, const Dictionary &p_parameters);
// What is wrong with the derived ports of a step, or an empty string.
String get_ports_problem(const GraphStepKind &p_kind, const Dictionary &p_parameters);
// The fixed ports, then the derived ones, then the pins, on one side.
int get_port_count(const GraphStepKind &p_kind, const Dictionary &p_parameters, GraphPinSide p_side);
GraphPort get_port(const GraphStepKind &p_kind, const Dictionary &p_parameters, GraphPinSide p_side, int p_index);

} // namespace GraphStepKinds
