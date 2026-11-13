/**************************************************************************/
/*  graph_function.cpp                                                    */

#include "graph_function.h"

#include "core/variant/array.h"

int GraphFunction::_find_step_index(int p_id) const {
	for (int i = 0; i < steps.size(); i++) {
		if (steps[i].id == p_id) {
			return i;
		}
	}
	return -1;
}

int GraphFunction::_find_wire_index(const GraphWire &p_wire) const {
	for (int i = 0; i < wires.size(); i++) {
		if (wires[i] == p_wire) {
			return i;
		}
	}
	return -1;
}

bool GraphFunction::_is_input_wired(int p_step, int p_port) const {
	for (const GraphWire &wire : wires) {
		if (wire.to_step == p_step && wire.to_port == p_port) {
			return true;
		}
	}
	return false;
}

void GraphFunction::_remove_wires_from_output(int p_step, int p_port) {
	for (int i = wires.size() - 1; i >= 0; i--) {
		if (wires[i].from_step == p_step && wires[i].from_port == p_port) {
			wires.remove_at(i);
		}
	}
}

const GraphStep *GraphFunction::get_step(int p_id) const {
	const int index = _find_step_index(p_id);
	return index < 0 ? nullptr : &steps[index];
}

int GraphFunction::get_valid_step_id() const {
	int next = 1;
	for (const GraphStep &step : steps) {
		next = MAX(next, step.id + 1);
	}
	return next;
}

bool GraphFunction::has_wire(const GraphWire &p_wire) const {
	return _find_wire_index(p_wire) >= 0;
}

Error GraphFunction::add_step(int p_id, const StringName &p_kind) {
	if (p_id < 1 || !GraphStepKinds::find(p_kind)) {
		return ERR_INVALID_PARAMETER;
	}
	if (_find_step_index(p_id) >= 0) {
		return ERR_ALREADY_EXISTS;
	}
	GraphStep step;
	step.id = p_id;
	step.kind = p_kind;
	steps.push_back(step);
	return OK;
}

Error GraphFunction::remove_step(int p_id) {
	const int index = _find_step_index(p_id);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	steps.remove_at(index);
	for (int i = wires.size() - 1; i >= 0; i--) {
		if (wires[i].from_step == p_id || wires[i].to_step == p_id) {
			wires.remove_at(i);
		}
	}
	return OK;
}

Error GraphFunction::move_step(int p_id, const Vector2 &p_position) {
	const int index = _find_step_index(p_id);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	steps.write[index].position = p_position;
	return OK;
}

Error GraphFunction::set_step_parameter(int p_id, const StringName &p_parameter, const Variant &p_value) {
	const int index = _find_step_index(p_id);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	if (p_parameter == StringName() || p_parameter == StringName("pins")) {
		return ERR_INVALID_PARAMETER;
	}
	steps.write[index].parameters[p_parameter] = p_value;
	_remove_broken_wires(p_id);
	return OK;
}

void GraphFunction::_remove_wires_beyond(int p_step, GraphPinSide p_side, int p_port_count) {
	for (int i = wires.size() - 1; i >= 0; i--) {
		const GraphWire &wire = wires[i];
		const bool beyond = p_side == GraphPinSide::INPUT ? (wire.to_step == p_step && wire.to_port >= p_port_count) : (wire.from_step == p_step && wire.from_port >= p_port_count);
		if (beyond) {
			wires.remove_at(i);
		}
	}
}

Error GraphFunction::set_pin_count(int p_id, int p_count) {
	const int index = _find_step_index(p_id);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	const GraphStepKind *kind = GraphStepKinds::find(steps[index].kind);
	if (!kind) {
		return ERR_UNAVAILABLE;
	}
	if (kind->growth.side == GraphPinSide::NONE || p_count < kind->growth.minimum || p_count > GraphStepKinds::MAX_PINS) {
		return ERR_INVALID_PARAMETER;
	}
	steps.write[index].parameters["pins"] = p_count;
	_remove_wires_beyond(p_id, kind->growth.side, GraphStepKinds::get_port_count(*kind, steps[index].parameters, kind->growth.side));
	return OK;
}

// A step whose kind is unknown or whose parameters name nothing real has an incomplete port list,
// so a wire that does not fit it may still fit once the step is fixed.
bool GraphFunction::_has_complete_ports(const GraphStep &p_step) const {
	const GraphStepKind *kind = GraphStepKinds::find(p_step.kind);
	return kind && GraphStepKinds::get_ports_problem(*kind, p_step.parameters).is_empty();
}

bool GraphFunction::_ports_fit(const GraphStep &p_from, const GraphStep &p_to, const GraphWire &p_wire) const {
	const GraphStepKind &from_kind = *GraphStepKinds::find(p_from.kind);
	const GraphStepKind &to_kind = *GraphStepKinds::find(p_to.kind);
	const int outputs = GraphStepKinds::get_port_count(from_kind, p_from.parameters, GraphPinSide::OUTPUT);
	const int inputs = GraphStepKinds::get_port_count(to_kind, p_to.parameters, GraphPinSide::INPUT);
	if (p_wire.from_port < 0 || p_wire.from_port >= outputs || p_wire.to_port < 0 || p_wire.to_port >= inputs) {
		return false;
	}
	const GraphPort output = GraphStepKinds::get_port(from_kind, p_from.parameters, GraphPinSide::OUTPUT, p_wire.from_port);
	const GraphPort input = GraphStepKinds::get_port(to_kind, p_to.parameters, GraphPinSide::INPUT, p_wire.to_port);
	return output.sort == input.sort;
}

// A parameter may change the ports of its step, such as the method a call step names.
void GraphFunction::_remove_broken_wires(int p_step) {
	for (int i = wires.size() - 1; i >= 0; i--) {
		const GraphStep *from = get_step(wires[i].from_step);
		const GraphStep *to = get_step(wires[i].to_step);
		const bool touches = wires[i].from_step == p_step || wires[i].to_step == p_step;
		if (touches && from && to && _has_complete_ports(*from) && _has_complete_ports(*to) && !_ports_fit(*from, *to, wires[i])) {
			wires.remove_at(i);
		}
	}
}

Error GraphFunction::_check_ports(const GraphStep &p_from, const GraphStep &p_to, const GraphWire &p_wire) const {
	if (!_ports_fit(p_from, p_to, p_wire)) {
		return _has_complete_ports(p_from) && _has_complete_ports(p_to) ? ERR_INVALID_PARAMETER : ERR_UNAVAILABLE;
	}
	const GraphStepKind &to_kind = *GraphStepKinds::find(p_to.kind);
	if (has_wire(p_wire)) {
		return ERR_ALREADY_EXISTS;
	}
	if (GraphStepKinds::get_port(to_kind, p_to.parameters, GraphPinSide::INPUT, p_wire.to_port).sort == GraphPortSort::DATA && _is_input_wired(p_wire.to_step, p_wire.to_port)) {
		return ERR_ALREADY_IN_USE;
	}
	return OK;
}

// A step of an unknown kind has no ports to check, so its wires come back as unavailable.
Error GraphFunction::_check_wire(const GraphWire &p_wire) const {
	const GraphStep *from = get_step(p_wire.from_step);
	const GraphStep *to = get_step(p_wire.to_step);
	if (!from || !to) {
		return ERR_DOES_NOT_EXIST;
	}
	if (p_wire.from_step == p_wire.to_step) {
		return ERR_INVALID_PARAMETER;
	}
	if (!GraphStepKinds::find(from->kind) || !GraphStepKinds::find(to->kind)) {
		return ERR_UNAVAILABLE;
	}
	return _check_ports(*from, *to, p_wire);
}

Error GraphFunction::connect_steps(const GraphWire &p_wire) {
	const Error err = _check_wire(p_wire);
	if (err != OK) {
		return err;
	}
	// A flow output has one wire. A new one replaces it.
	const GraphStep &from = *get_step(p_wire.from_step);
	if (GraphStepKinds::get_port(*GraphStepKinds::find(from.kind), from.parameters, GraphPinSide::OUTPUT, p_wire.from_port).sort == GraphPortSort::FLOW) {
		_remove_wires_from_output(p_wire.from_step, p_wire.from_port);
	}
	wires.push_back(p_wire);
	return OK;
}

Error GraphFunction::disconnect_steps(const GraphWire &p_wire) {
	const int index = _find_wire_index(p_wire);
	if (index < 0) {
		return ERR_DOES_NOT_EXIST;
	}
	wires.remove_at(index);
	return OK;
}

Dictionary GraphCommentFrame::to_dictionary() const {
	Dictionary entry;
	entry["id"] = id;
	entry["title"] = title;
	entry["x"] = position.x;
	entry["y"] = position.y;
	entry["width"] = size.x;
	entry["height"] = size.y;
	entry["collapsed"] = collapsed;
	return entry;
}

namespace {

bool is_number(const Variant &p_value) {
	return p_value.get_type() == Variant::INT || p_value.get_type() == Variant::FLOAT;
}

} // namespace

Error GraphCommentFrame::from_variant(const Variant &p_value, GraphCommentFrame &r_frame) {
	if (p_value.get_type() != Variant::DICTIONARY) {
		return ERR_INVALID_DATA;
	}
	const Dictionary entry = p_value;
	for (const char *key : { "id", "x", "y", "width", "height" }) {
		if (!is_number(entry.get(key, Variant()))) {
			return ERR_INVALID_DATA;
		}
	}
	if (entry.get("title", Variant()).get_type() != Variant::STRING || entry.get("collapsed", Variant()).get_type() != Variant::BOOL) {
		return ERR_INVALID_DATA;
	}
	r_frame.id = int(entry["id"]);
	r_frame.title = entry["title"];
	r_frame.position = Vector2(entry["x"], entry["y"]);
	r_frame.size = Vector2(entry["width"], entry["height"]);
	r_frame.collapsed = entry["collapsed"];
	return OK;
}

Error GraphFunction::set_frame(const GraphCommentFrame &p_frame) {
	// A frame with no area cannot be seen or grabbed.
	if (p_frame.id < 1 || p_frame.size.x <= 0 || p_frame.size.y <= 0) {
		return ERR_INVALID_PARAMETER;
	}
	for (GraphCommentFrame &frame : frames) {
		if (frame.id == p_frame.id) {
			frame = p_frame;
			return OK;
		}
	}
	frames.push_back(p_frame);
	return OK;
}

Error GraphFunction::remove_frame(int p_id) {
	for (int i = 0; i < frames.size(); i++) {
		if (frames[i].id == p_id) {
			frames.remove_at(i);
			return OK;
		}
	}
	return ERR_DOES_NOT_EXIST;
}

Dictionary GraphFunction::to_dictionary() const {
	Array step_list;
	for (const GraphStep &step : steps) {
		Dictionary entry;
		entry["id"] = step.id;
		entry["kind"] = String(step.kind);
		entry["position"] = step.position;
		entry["parameters"] = step.parameters.duplicate(true);
		step_list.push_back(entry);
	}
	Array wire_list;
	for (const GraphWire &wire : wires) {
		wire_list.push_back(Array{ wire.from_step, wire.from_port, wire.to_step, wire.to_port });
	}
	Array frame_list;
	for (const GraphCommentFrame &frame : frames) {
		frame_list.push_back(frame.to_dictionary());
	}
	Dictionary result;
	result["name"] = name;
	result["steps"] = step_list;
	result["wires"] = wire_list;
	result["frames"] = frame_list;
	return result;
}

Error GraphFunction::_parse_step(const Variant &p_value, GraphStep &r_step) {
	if (p_value.get_type() != Variant::DICTIONARY) {
		return ERR_INVALID_DATA;
	}
	const Dictionary entry = p_value;
	if (entry.get("id", Variant()).get_type() != Variant::INT || entry.get("kind", Variant()).get_type() != Variant::STRING) {
		return ERR_INVALID_DATA;
	}
	if (entry.get("position", Variant()).get_type() != Variant::VECTOR2 || entry.get("parameters", Variant()).get_type() != Variant::DICTIONARY) {
		return ERR_INVALID_DATA;
	}
	r_step.id = entry["id"];
	r_step.kind = StringName(String(entry["kind"]));
	r_step.position = entry["position"];
	r_step.parameters = Dictionary(entry["parameters"]).duplicate(true);
	return r_step.id >= 1 && r_step.kind != StringName() ? OK : ERR_INVALID_DATA;
}

Error GraphFunction::_parse_wire(const Variant &p_value, GraphWire &r_wire) {
	if (p_value.get_type() != Variant::ARRAY) {
		return ERR_INVALID_DATA;
	}
	const Array entry = p_value;
	if (entry.size() != 4) {
		return ERR_INVALID_DATA;
	}
	for (int i = 0; i < 4; i++) {
		if (entry[i].get_type() != Variant::INT) {
			return ERR_INVALID_DATA;
		}
	}
	r_wire.from_step = entry[0];
	r_wire.from_port = entry[1];
	r_wire.to_step = entry[2];
	r_wire.to_port = entry[3];
	return OK;
}

Error GraphFunction::_load_steps(const Array &p_steps) {
	for (const Variant &value : p_steps) {
		GraphStep step;
		Error err = _parse_step(value, step);
		if (err == OK && _find_step_index(step.id) >= 0) {
			err = ERR_ALREADY_EXISTS;
		}
		if (err != OK) {
			return err;
		}
		steps.push_back(step);
	}
	return OK;
}

Error GraphFunction::_load_wires(const Array &p_wires) {
	for (const Variant &value : p_wires) {
		GraphWire wire;
		Error err = _parse_wire(value, wire);
		if (err == OK) {
			err = _check_wire(wire);
		}
		// A wire on a step of an unknown kind is kept, so a save does not lose it.
		if (err != OK && err != ERR_UNAVAILABLE) {
			return err;
		}
		wires.push_back(wire);
	}
	return OK;
}

Error GraphFunction::_load_frames(const Array &p_frames) {
	for (const Variant &value : p_frames) {
		GraphCommentFrame frame;
		Error err = GraphCommentFrame::from_variant(value, frame);
		if (err == OK) {
			err = set_frame(frame);
		}
		if (err != OK) {
			return err;
		}
	}
	return OK;
}

Error GraphFunction::from_dictionary(const Dictionary &p_dictionary, GraphFunction &r_function) {
	if (p_dictionary.get("name", Variant()).get_type() != Variant::STRING || p_dictionary.get("steps", Variant()).get_type() != Variant::ARRAY || p_dictionary.get("wires", Variant()).get_type() != Variant::ARRAY) {
		return ERR_INVALID_DATA;
	}
	r_function.name = p_dictionary["name"];
	const Error err = r_function._load_steps(p_dictionary["steps"]);
	if (err != OK) {
		return err;
	}
	const Error wires_err = r_function._load_wires(p_dictionary["wires"]);
	if (wires_err != OK) {
		return wires_err;
	}
	// A function with no frames may leave the key out.
	const Variant frames = p_dictionary.get("frames", Array());
	return frames.get_type() == Variant::ARRAY ? r_function._load_frames(frames) : ERR_INVALID_DATA;
}
