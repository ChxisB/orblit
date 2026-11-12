/**************************************************************************/
/*  graph_step_kinds.h                                                    */

#pragma once

#include "core/string/string_name.h"
#include "core/variant/variant.h"

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

struct GraphStepKind {
	const char *name;
	const char *category;
	GraphPortList inputs;
	GraphPortList outputs;
};

namespace GraphStepKinds {

// Returns null for a name this build does not know.
const GraphStepKind *find(const StringName &p_name);

} // namespace GraphStepKinds
