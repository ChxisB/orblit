/**************************************************************************/
/*  graph_compiled.h                                                      */

#pragma once

#include "graph_signature.h"
#include "graph_step_kinds.h"

#include "core/containers/vector.h"
#include "core/variant/dictionary.h"

// A function after the compile. The wires are resolved to indices, so a run follows numbers
// and never searches.

struct GraphCompiledInput {
	// Index of the step that gives the value, or -1 to use the value below.
	int step = -1;
	int port = 0;
	Variant value;
};

struct GraphCompiledStep {
	int index = 0;
	int id = 0;
	const GraphStepKind *kind = nullptr;
	Dictionary parameters;
	// What the kind's resolve function worked out from the parameters.
	int operation = 0;
	Vector<Variant> values;
	// One per input port. A flow port has an empty entry.
	Vector<GraphCompiledInput> inputs;
	// One per output port: the step a flow wire leads to, or -1. A data port holds -1.
	Vector<int> next;
	// The first of this step's stored outputs in the slots of a call. Only steps that run have them.
	int slot = 0;
};

struct GraphCompiledFunction {
	String name;
	// Empty when the function compiles.
	String error;
	Vector<GraphCompiledStep> steps;
	// Index of the start step, or -1 for a function that has none and does nothing.
	int start = -1;
	int slot_count = 0;
	// What the start step declares. Empty when the function has no start step or a bad one.
	GraphSignature signature;
};
