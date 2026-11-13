/**************************************************************************/
/*  graph_step_run.cpp                                                    */

#include "graph_step_run.h"

#include "graph_call.h"

namespace {

// These numbers follow the port lists in graph_step_kinds.cpp.
constexpr int THEN = 0;
constexpr int ELSE = 1;
constexpr int BODY = 0;
constexpr int DONE = 1;
constexpr int VALUE = 1;
constexpr int CONDITION = 1;
constexpr int FROM = 1;
constexpr int TO = 2;
constexpr int COUNT_INDEX = 2;
constexpr int ITEMS = 1;
constexpr int ITEM = 2;
constexpr int EACH_INDEX = 3;
constexpr int OPERAND_A = 0;
constexpr int OPERAND_B = 1;

GraphPending loop_frame(const GraphCompiledStep &p_step, int (*p_resume)(GraphCall &, GraphPending &)) {
	GraphPending frame;
	frame.step = p_step.index;
	frame.loop = true;
	frame.resume = p_resume;
	return frame;
}

// A loop ends when a break marked it or its work is used up. Either way the done output follows.
int end_loop(GraphPending &p_frame) {
	p_frame.done = true;
	return DONE;
}

int resume_while(GraphCall &p_call, GraphPending &p_frame) {
	if (p_frame.stopped || !bool(p_call.input(p_call.get_step(p_frame.step), CONDITION))) {
		return end_loop(p_frame);
	}
	return BODY;
}

int resume_count(GraphCall &p_call, GraphPending &p_frame) {
	if (p_frame.stopped || p_frame.position > p_frame.end) {
		return end_loop(p_frame);
	}
	p_call.store(p_call.get_step(p_frame.step), COUNT_INDEX, p_frame.position);
	// Stepping past the largest integer would wrap round and the loop would never reach its end.
	if (p_frame.position == p_frame.end) {
		p_frame.stopped = true;
	} else {
		p_frame.position++;
	}
	return BODY;
}

int resume_each(GraphCall &p_call, GraphPending &p_frame) {
	if (p_frame.stopped || !p_frame.more) {
		return end_loop(p_frame);
	}
	const GraphCompiledStep &step = p_call.get_step(p_frame.step);
	bool valid = true;
	p_call.store(step, ITEM, p_frame.container.iter_get(p_frame.cursor, valid));
	p_call.store(step, EACH_INDEX, p_frame.position++);
	p_frame.more = p_frame.container.iter_next(p_frame.cursor, valid);
	return BODY;
}

int resume_sequence(GraphCall &p_call, GraphPending &p_frame) {
	const int output = int(p_frame.position++);
	p_frame.done = p_frame.position >= p_frame.end;
	return output;
}

bool is_binary(int p_operator) {
	return p_operator != Variant::OP_NEGATE && p_operator != Variant::OP_POSITIVE && p_operator != Variant::OP_BIT_NEGATE && p_operator != Variant::OP_NOT;
}

String missing_name(const GraphCompiledStep &p_step) {
	const Variant name = p_step.parameters.get("name", Variant());
	return name.get_type() == Variant::NIL || String(name).is_empty() ? String("the step needs a 'name' parameter.") : String();
}

} // namespace

namespace GraphStepRun {

Variant eval_variable(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return p_call.read_variable(p_step);
}

Variant eval_local(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return p_call.read_local(p_step);
}

Variant eval_operator(GraphCall &p_call, const GraphCompiledStep &p_step) {
	const Variant a = p_call.input(p_step, OPERAND_A);
	const Variant b = p_call.input(p_step, OPERAND_B);
	Variant result;
	bool valid = true;
	Variant::evaluate(Variant::Operator(p_step.operation), a, b, result, valid);
	if (!valid) {
		p_call.fail(p_step, vformat("cannot apply '%s' to a %s and a %s.", Variant::get_operator_name(Variant::Operator(p_step.operation)), Variant::get_type_name(a.get_type()), Variant::get_type_name(b.get_type())));
		return Variant();
	}
	return result;
}

int run_return(GraphCall &p_call, const GraphCompiledStep &p_step) {
	p_call.finish(p_step, p_call.input(p_step, VALUE));
	return GraphFlow::END;
}

int run_branch(GraphCall &p_call, const GraphCompiledStep &p_step) {
	return bool(p_call.input(p_step, CONDITION)) ? THEN : ELSE;
}

int run_while(GraphCall &p_call, const GraphCompiledStep &p_step) {
	p_call.push(loop_frame(p_step, resume_while));
	return GraphFlow::END;
}

int run_count_loop(GraphCall &p_call, const GraphCompiledStep &p_step) {
	GraphPending frame = loop_frame(p_step, resume_count);
	frame.position = p_call.input(p_step, FROM);
	frame.end = p_call.input(p_step, TO);
	p_call.push(frame);
	return GraphFlow::END;
}

int run_each(GraphCall &p_call, const GraphCompiledStep &p_step) {
	GraphPending frame = loop_frame(p_step, resume_each);
	frame.container = p_call.input(p_step, ITEMS);
	bool valid = true;
	frame.more = frame.container.iter_init(frame.cursor, valid);
	if (!valid) {
		p_call.fail(p_step, vformat("cannot go through a %s. Give it an array, a dictionary, a packed array, a string or a number.", Variant::get_type_name(frame.container.get_type())));
		return GraphFlow::END;
	}
	p_call.push(frame);
	return GraphFlow::END;
}

int run_sequence(GraphCall &p_call, const GraphCompiledStep &p_step) {
	GraphPending frame;
	frame.step = p_step.index;
	frame.position = 1;
	frame.end = p_step.next.size();
	frame.resume = resume_sequence;
	p_call.push(frame);
	return 0;
}

int run_break(GraphCall &p_call, const GraphCompiledStep &p_step) {
	if (!p_call.has_loop()) {
		p_call.fail(p_step, "there is no loop to leave.");
		return GraphFlow::END;
	}
	p_call.stop_loop();
	return GraphFlow::END;
}

int run_set_variable(GraphCall &p_call, const GraphCompiledStep &p_step) {
	p_call.write_variable(p_step, p_call.input(p_step, VALUE));
	return THEN;
}

int run_set_local(GraphCall &p_call, const GraphCompiledStep &p_step) {
	p_call.write_local(p_step, p_call.input(p_step, VALUE));
	return THEN;
}

String resolve_variable(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	const String problem = missing_name(p_step);
	if (!problem.is_empty()) {
		return problem;
	}
	const String name = p_step.parameters["name"];
	return p_variables.has(StringName(name)) ? String() : vformat("there is no variable named '%s'.", name);
}

String resolve_local(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	return missing_name(p_step);
}

String resolve_operator(GraphCompiledStep &p_step, const Vector<StringName> &p_variables) {
	const String symbol = p_step.parameters.get("operator", String());
	for (int op = Variant::OP_EQUAL; op < Variant::OP_MAX; op++) {
		if (is_binary(op) && Variant::get_operator_name(Variant::Operator(op)) == symbol) {
			p_step.operation = op;
			return String();
		}
	}
	return vformat("the operator '%s' is not known.", symbol);
}

} // namespace GraphStepRun
