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

// A step parameter that names something the script declares, such as the variable a Get step reads.
struct GraphReference {
	const char *kind;
	const char *parameter;
	// The input that takes the object the step acts on, or -1 for a step with none. A step that acts
	// on another object holds a name that is not one of this script's.
	int target_port;
};

// A labeled rectangle on the canvas. It holds no steps and does not affect how the function
// runs. The editor moves the steps inside it along with it.
struct GraphCommentFrame {
	int id = 0;
	String title;
	Vector2 position;
	Vector2 size;
	bool collapsed = false;

	Dictionary to_dictionary() const;
	// Reads the shape to_dictionary writes. A number may be an int or a float, because JSON has both.
	static Error from_variant(const Variant &p_value, GraphCommentFrame &r_frame);
};

// The steps and wires of one function. Every change goes through a method here, and a
// method that returns an error leaves the function as it was.
class GraphFunction {
	String name;
	Vector<GraphStep> steps;
	Vector<GraphWire> wires;
	Vector<GraphCommentFrame> frames;

	int _find_step_index(int p_id) const;
	int _find_wire_index(const GraphWire &p_wire) const;
	bool _is_input_wired(int p_step, int p_port) const;
	void _remove_wires_from_output(int p_step, int p_port);
	Error _check_wire(const GraphWire &p_wire) const;
	bool _has_complete_ports(const GraphStep &p_step) const;
	bool _ports_fit(const GraphStep &p_from, const GraphStep &p_to, const GraphWire &p_wire) const;
	void _remove_broken_wires(int p_step);
	Error _check_ports(const GraphStep &p_from, const GraphStep &p_to, const GraphWire &p_wire) const;
	bool _acts_on_owner(const GraphStep &p_step, int p_target_port) const;
	void _remove_wires_beyond(int p_step, GraphPinSide p_side, int p_port_count);

	Error _load_steps(const Array &p_steps);
	Error _load_wires(const Array &p_wires);
	Error _load_frames(const Array &p_frames);

	static Error _parse_step(const Variant &p_value, GraphStep &r_step);
	static Error _parse_wire(const Variant &p_value, GraphWire &r_wire);

public:
	GraphFunction() = default;
	explicit GraphFunction(const String &p_name) :
			name(p_name) {}

	const String &get_name() const { return name; }
	// The script checks the name and the calls to the function, so it is the one caller.
	void set_name(const String &p_name) { name = p_name; }
	int get_step_count() const { return steps.size(); }
	int get_wire_count() const { return wires.size(); }
	int get_frame_count() const { return frames.size(); }
	const GraphStep *get_step(int p_id) const;
	const GraphStep &get_step_at(int p_index) const { return steps[p_index]; }
	const GraphWire &get_wire_at(int p_index) const { return wires[p_index]; }
	const GraphCommentFrame &get_frame_at(int p_index) const { return frames[p_index]; }
	int get_valid_step_id() const;
	bool has_wire(const GraphWire &p_wire) const;

	Error add_step(int p_id, const StringName &p_kind);
	Error remove_step(int p_id);
	Error move_step(int p_id, const Vector2 &p_position);
	// The "pins" parameter changes only here, because wires on a removed pin go with it.
	Error set_step_parameter(int p_id, const StringName &p_parameter, const Variant &p_value);
	Error set_pin_count(int p_id, int p_count);
	// Writes p_new into the parameter of every step the reference lists that holds p_old and acts
	// on the owner of the script.
	void rename_references(const GraphReference &p_reference, const StringName &p_old, const StringName &p_new);
	Error connect_steps(const GraphWire &p_wire);
	Error disconnect_steps(const GraphWire &p_wire);
	// Adds the frame, or replaces the frame with the same id.
	Error set_frame(const GraphCommentFrame &p_frame);
	Error remove_frame(int p_id);

	Dictionary to_dictionary() const;
	// Leaves r_function half filled on an error. The caller drops it then.
	static Error from_dictionary(const Dictionary &p_dictionary, GraphFunction &r_function);
};
