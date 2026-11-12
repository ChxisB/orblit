/**************************************************************************/
/*  graph_function.h                                                      */

#pragma once

#include "graph_step_kinds.h"

#include "core/containers/vector.h"
#include "core/math/vector2.h"
#include "core/string/ustring.h"
#include "core/variant/dictionary.h"

struct GraphStep {
	int id = 0;
	// Kept as read from a file when this build does not know it. The compile reports it.
	StringName kind;
	Vector2 position;
	Dictionary parameters;
};

struct GraphWire {
	int from_step = 0;
	int from_port = 0;
	int to_step = 0;
	int to_port = 0;

	bool operator==(const GraphWire &p_other) const {
		return from_step == p_other.from_step && from_port == p_other.from_port && to_step == p_other.to_step && to_port == p_other.to_port;
	}
};

// The steps and wires of one function. Every change goes through a method here, and a
// method that returns an error leaves the function as it was.
class GraphFunction {
	String name;
	Vector<GraphStep> steps;
	Vector<GraphWire> wires;

	int _find_step_index(int p_id) const;
	int _find_wire_index(const GraphWire &p_wire) const;
	bool _is_input_wired(int p_step, int p_port) const;
	void _remove_wires_from_output(int p_step, int p_port);
	Error _check_wire(const GraphWire &p_wire) const;
	Error _check_ports(const GraphStepKind &p_from, const GraphStepKind &p_to, const GraphWire &p_wire) const;

	Error _load_steps(const Array &p_steps);
	Error _load_wires(const Array &p_wires);

	static Error _parse_step(const Variant &p_value, GraphStep &r_step);
	static Error _parse_wire(const Variant &p_value, GraphWire &r_wire);

public:
	GraphFunction() = default;
	explicit GraphFunction(const String &p_name) :
			name(p_name) {}

	const String &get_name() const { return name; }
	int get_step_count() const { return steps.size(); }
	int get_wire_count() const { return wires.size(); }
	const GraphStep *get_step(int p_id) const;
	const GraphStep &get_step_at(int p_index) const { return steps[p_index]; }
	const GraphWire &get_wire_at(int p_index) const { return wires[p_index]; }
	int get_valid_step_id() const;
	bool has_wire(const GraphWire &p_wire) const;

	Error add_step(int p_id, const StringName &p_kind);
	Error remove_step(int p_id);
	Error move_step(int p_id, const Vector2 &p_position);
	Error set_step_parameter(int p_id, const StringName &p_parameter, const Variant &p_value);
	Error connect_steps(const GraphWire &p_wire);
	Error disconnect_steps(const GraphWire &p_wire);

	Dictionary to_dictionary() const;
	// Leaves r_function half filled on an error. The caller drops it then.
	static Error from_dictionary(const Dictionary &p_dictionary, GraphFunction &r_function);
};
