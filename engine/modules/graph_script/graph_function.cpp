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
	if (p_parameter == StringName()) {
		return ERR_INVALID_PARAMETER;
	}
	steps.write[index].parameters[p_parameter] = p_value;
	return OK;
}

Error GraphFunction::_check_ports(const GraphStepKind &p_from, const GraphStepKind &p_to, const GraphWire &p_wire) const {
	if (p_wire.from_port < 0 || p_wire.from_port >= p_from.outputs.count || p_wire.to_port < 0 || p_wire.to_port >= p_to.inputs.count) {
		return ERR_INVALID_PARAMETER;
	}
	const GraphPortSpec &output = p_from.outputs.items[p_wire.from_port];
	const GraphPortSpec &input = p_to.inputs.items[p_wire.to_port];
	if (output.sort != input.sort) {
		return ERR_INVALID_PARAMETER;
	}
	if (has_wire(p_wire)) {
		return ERR_ALREADY_EXISTS;
	}
	if (input.sort == GraphPortSort::DATA && _is_input_wired(p_wire.to_step, p_wire.to_port)) {
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
	const GraphStepKind *from_kind = GraphStepKinds::find(from->kind);
	const GraphStepKind *to_kind = GraphStepKinds::find(to->kind);
	if (!from_kind || !to_kind) {
		return ERR_UNAVAILABLE;
	}
	return _check_ports(*from_kind, *to_kind, p_wire);
}

Error GraphFunction::connect_steps(const GraphWire &p_wire) {
	const Error err = _check_wire(p_wire);
	if (err != OK) {
		return err;
	}
	// A flow output has one wire. A new one replaces it.
	const GraphStepKind *from_kind = GraphStepKinds::find(get_step(p_wire.from_step)->kind);
	if (from_kind->outputs.items[p_wire.from_port].sort == GraphPortSort::FLOW) {
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
	Dictionary result;
	result["name"] = name;
	result["steps"] = step_list;
	result["wires"] = wire_list;
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

Error GraphFunction::from_dictionary(const Dictionary &p_dictionary, GraphFunction &r_function) {
	if (p_dictionary.get("name", Variant()).get_type() != Variant::STRING || p_dictionary.get("steps", Variant()).get_type() != Variant::ARRAY || p_dictionary.get("wires", Variant()).get_type() != Variant::ARRAY) {
		return ERR_INVALID_DATA;
	}
	r_function.name = p_dictionary["name"];
	const Error err = r_function._load_steps(p_dictionary["steps"]);
	if (err != OK) {
		return err;
	}
	return r_function._load_wires(p_dictionary["wires"]);
}
