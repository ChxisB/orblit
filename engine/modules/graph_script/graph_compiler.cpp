/**************************************************************************/
/*  graph_compiler.cpp                                                    */

#include "graph_compiler.h"

#include "core/containers/hash_map.h"

namespace {

// A depth first walk that finds a cycle in the edges of a graph.
class CycleFinder {
	enum Mark : uint8_t {
		UNSEEN,
		ON_PATH,
		DONE,
	};

	const Vector<Vector<int>> &edges;
	Vector<uint8_t> marks;
	int found = -1;

	// shortcut: the walk recurses, so a chain of tens of thousands of steps would overflow the
	// stack. Switch to an explicit stack when a graph that long shows up.
	bool _visit(int p_node) {
		if (marks[p_node] == ON_PATH) {
			found = p_node;
			return true;
		}
		if (marks[p_node] == DONE) {
			return false;
		}
		marks.write[p_node] = ON_PATH;
		for (const int next : edges[p_node]) {
			if (_visit(next)) {
				return true;
			}
		}
		marks.write[p_node] = DONE;
		return false;
	}

public:
	explicit CycleFinder(const Vector<Vector<int>> &p_edges) :
			edges(p_edges) {
		marks.resize_initialized(p_edges.size());
	}

	// Returns an index that lies on a cycle, or -1.
	int find() {
		for (int i = 0; i < edges.size(); i++) {
			if (_visit(i)) {
				return found;
			}
		}
		return -1;
	}
};

class Compiler {
	const GraphFunction &source;
	const Vector<StringName> &variables;
	GraphCompiledFunction out;
	HashMap<int, int> index_of_id;

	String _fail(int p_step_id, const String &p_message) const {
		return vformat("Function '%s', step %d: %s", out.name, p_step_id, p_message);
	}

	GraphPort _input(int p_step, int p_port) const {
		const GraphCompiledStep &step = out.steps[p_step];
		return GraphStepKinds::get_port(*step.kind, step.parameters, GraphPinSide::INPUT, p_port);
	}

	GraphPort _output(int p_step, int p_port) const {
		const GraphCompiledStep &step = out.steps[p_step];
		return GraphStepKinds::get_port(*step.kind, step.parameters, GraphPinSide::OUTPUT, p_port);
	}

	String _add_steps();
	String _add_wires();
	String _check_wire_types();
	String _fill_default(GraphCompiledStep &p_step, int p_port, const GraphPort &p_spec);
	String _fill_defaults();
	String _resolve_steps();
	String _find_start();
	String _check_flow_cycles();
	String _check_data_cycles();

public:
	Compiler(const GraphFunction &p_source, const Vector<StringName> &p_variables) :
			source(p_source), variables(p_variables) {}

	GraphCompiledFunction compile() {
		out.name = source.get_name();
		String (Compiler::*phases[])() = {
			&Compiler::_add_steps,
			&Compiler::_find_start,
			&Compiler::_add_wires,
			&Compiler::_check_wire_types,
			&Compiler::_resolve_steps,
			&Compiler::_fill_defaults,
			&Compiler::_check_flow_cycles,
			&Compiler::_check_data_cycles,
		};
		for (String (Compiler::*phase)() : phases) {
			out.error = (this->*phase)();
			if (!out.error.is_empty()) {
				out.steps.clear();
				out.start = -1;
				break;
			}
		}
		return out;
	}
};

String Compiler::_add_steps() {
	for (int i = 0; i < source.get_step_count(); i++) {
		const GraphStep &step = source.get_step_at(i);
		const GraphStepKind *kind = GraphStepKinds::find(step.kind);
		if (!kind) {
			return _fail(step.id, vformat("this build does not know the step kind '%s'.", step.kind));
		}
		const String problem = GraphStepKinds::get_ports_problem(*kind, step.parameters);
		if (!problem.is_empty()) {
			return _fail(step.id, problem);
		}
		GraphCompiledStep compiled;
		compiled.index = i;
		compiled.id = step.id;
		compiled.kind = kind;
		compiled.parameters = step.parameters;
		compiled.inputs.resize(GraphStepKinds::get_port_count(*kind, step.parameters, GraphPinSide::INPUT));
		compiled.next.resize(GraphStepKinds::get_port_count(*kind, step.parameters, GraphPinSide::OUTPUT));
		compiled.next.fill(-1);
		if (!kind->is_pure()) {
			compiled.slot = out.slot_count;
			out.slot_count += compiled.next.size();
		}
		index_of_id[step.id] = i;
		out.steps.push_back(compiled);
	}
	return String();
}

String Compiler::_add_wires() {
	for (int i = 0; i < source.get_wire_count(); i++) {
		const GraphWire &wire = source.get_wire_at(i);
		const int from = index_of_id[wire.from_step];
		const int to = index_of_id[wire.to_step];
		if (_output(from, wire.from_port).sort == GraphPortSort::FLOW) {
			out.steps.write[from].next.write[wire.from_port] = to;
		} else {
			out.steps.write[to].inputs.write[wire.to_port] = { from, wire.from_port, Variant() };
		}
	}
	return String();
}

// A wire may join two types only when a value of the first can become the second.
String Compiler::_check_wire_types() {
	for (const GraphCompiledStep &step : out.steps) {
		for (int port = 0; port < step.inputs.size(); port++) {
			const GraphCompiledInput &wired = step.inputs[port];
			if (wired.step < 0) {
				continue;
			}
			const GraphPort from = _output(wired.step, wired.port);
			const GraphPort to = _input(step.index, port);
			if (from.type != Variant::NIL && to.type != Variant::NIL && !Variant::can_convert_strict(from.type, to.type)) {
				return _fail(step.id, vformat("the wire from step %d, output '%s' gives a %s, and the input '%s' needs a %s.", out.steps[wired.step].id, from.name, Variant::get_type_name(from.type), to.name, Variant::get_type_name(to.type)));
			}
		}
	}
	return String();
}

// A data input with no wire takes the step parameter its port names. An optional port with no
// value reads its fallback.
String Compiler::_fill_default(GraphCompiledStep &p_step, int p_port, const GraphPort &p_spec) {
	GraphCompiledInput &input = p_step.inputs.write[p_port];
	if (!p_step.parameters.has(p_spec.parameter)) {
		input.value = p_spec.fallback;
		return p_spec.optional ? String() : _fail(p_step.id, vformat("the input '%s' has no wire and no value.", p_spec.name));
	}
	input.value = p_step.parameters[p_spec.parameter];
	const Variant::Type given = input.value.get_type();
	if (given != Variant::NIL && p_spec.type != Variant::NIL && !Variant::can_convert_strict(given, p_spec.type)) {
		return _fail(p_step.id, vformat("the input '%s' needs a %s, and the value is a %s.", p_spec.name, Variant::get_type_name(p_spec.type), Variant::get_type_name(given)));
	}
	return String();
}

String Compiler::_fill_defaults() {
	for (GraphCompiledStep &step : out.steps) {
		for (int port = 0; port < step.inputs.size(); port++) {
			const GraphPort spec = _input(step.index, port);
			if (spec.sort == GraphPortSort::FLOW || step.inputs[port].step >= 0) {
				continue;
			}
			const String problem = _fill_default(step, port, spec);
			if (!problem.is_empty()) {
				return problem;
			}
		}
	}
	return String();
}

String Compiler::_resolve_steps() {
	for (GraphCompiledStep &step : out.steps) {
		if (!step.kind->resolve) {
			continue;
		}
		const String problem = step.kind->resolve(step, variables);
		if (!problem.is_empty()) {
			return _fail(step.id, problem);
		}
	}
	return String();
}

// Runs before the wires, so a function that fails later still lists its arguments.
String Compiler::_find_start() {
	for (const GraphCompiledStep &step : out.steps) {
		if (!step.kind->is_start()) {
			continue;
		}
		if (out.start >= 0) {
			return _fail(step.id, vformat("the function has a second start step. The first is step %d.", out.steps[out.start].id));
		}
		out.start = step.index;
		const String problem = GraphSignatures::read(*step.kind, step.parameters, out.signature);
		if (!problem.is_empty()) {
			return _fail(step.id, problem);
		}
	}
	return String();
}

// Loops may be part of a cycle, so the walk skips them.
String Compiler::_check_flow_cycles() {
	Vector<Vector<int>> edges;
	edges.resize(out.steps.size());
	for (const GraphCompiledStep &step : out.steps) {
		for (const int next : step.next) {
			if (next >= 0 && !step.kind->loop && !out.steps[next].kind->loop) {
				edges.write[step.index].push_back(next);
			}
		}
	}
	const int on_cycle = CycleFinder(edges).find();
	return on_cycle < 0 ? String() : _fail(out.steps[on_cycle].id, "the flow wires form a cycle that does not pass through a loop.");
}

// Only pure steps are walked when a value is asked for, so only they can loop back on themselves.
String Compiler::_check_data_cycles() {
	Vector<Vector<int>> edges;
	edges.resize(out.steps.size());
	for (const GraphCompiledStep &step : out.steps) {
		for (const GraphCompiledInput &input : step.inputs) {
			if (step.kind->is_pure() && input.step >= 0 && out.steps[input.step].kind->is_pure()) {
				edges.write[step.index].push_back(input.step);
			}
		}
	}
	const int on_cycle = CycleFinder(edges).find();
	return on_cycle < 0 ? String() : _fail(out.steps[on_cycle].id, "the data wires form a cycle.");
}

} // namespace

GraphCompiledFunction GraphCompiler::compile(const GraphFunction &p_function, const Vector<StringName> &p_variables) {
	return Compiler(p_function, p_variables).compile();
}
