/**************************************************************************/
/*  graph_signature.h                                                     */

#pragma once

#include "graph_step_kinds.h"

#include "core/containers/vector.h"
#include "core/string/ustring.h"
#include "core/variant/array.h"
#include "core/variant/dictionary.h"

// A name with a type: an argument, a local or a field of a signal.
struct GraphTyped {
	StringName name;
	Variant::Type type = Variant::NIL;
};

// A signal the script declares, with the types of what it sends.
struct GraphSignal {
	StringName name;
	Vector<GraphTyped> arguments;
};

// What the start step of a function declares. The compile reads it once, so a call and the
// method list never parse parameters.
struct GraphSignature {
	Vector<GraphTyped> arguments;
	Vector<GraphTyped> locals;
	// Variant::NIL for a function that gives any value, or none.
	Variant::Type returns = Variant::NIL;
	String description;
	// The kinds of warning this function turns off.
	Vector<StringName> ignored_warnings;
	// Empty for a function that only this script calls. Otherwise the settings of the remote
	// call, with every key filled in, as the multiplayer reads them.
	Dictionary rpc;
	// Only a signal received step sets these: the signal, and the path of the entity that emits
	// it. An empty path means the owner.
	StringName signal;
	String target;

	const GraphTyped *find_local(const StringName &p_name) const;
};

namespace GraphSignatures {

// Reads a list of {name, type} entries. A missing list is an empty one. Returns the problem,
// or an empty string.
String parse_typed_list(const Variant &p_value, Vector<GraphTyped> &r_list);
Array typed_list_to_array(const Vector<GraphTyped> &p_list);

// Reads the parameters of a start step. Leaves r_signature alone when it returns a problem.
String read(const GraphStepKind &p_kind, const Dictionary &p_parameters, GraphSignature &r_signature);

// The derive function of the start kinds. Each argument is a data output after "then".
String derive_start(const Dictionary &p_parameters, GraphDerivedPorts &r_ports);

// A float may go into an int, a string may not. The type nil takes any value.
bool convert(Variant::Type p_type, const Variant &p_value, Variant &r_converted);
Variant make_default(Variant::Type p_type);

} // namespace GraphSignatures
